using System.Text.Json;
using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Recipes;
using Kotlet.Application.VideoTranscripts;
using Kotlet.Domain.Tools;

namespace Kotlet.Application.Tools;

public sealed class VideoAnalysisService(
    IVideoAnalysisJobRepository jobs,
    VideoTranscriptService transcripts,
    AiRecipeExtractionService extraction,
    IngredientResolutionService ingredientResolution,
    RecipeDuplicateDetectionService duplicateDetection,
    RecipeImportService recipeImports,
    IVideoAnalysisSignal signal)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VideoAnalysisOperationResult> CreateJobAsync(
        Guid houseId,
        Guid userId,
        string url,
        CancellationToken cancellationToken)
    {
        if (!TryGetSupportedVideoUri(url, out var uri))
        {
            return Validation("url", "Only YouTube and TikTok video URLs are supported.");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new VideoAnalysisJob
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Url = uri.ToString(),
            Status = VideoAnalysisJobStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        jobs.Add(job);
        await jobs.SaveChangesAsync(cancellationToken);
        signal.Enqueue(job.Id);
        return new(VideoAnalysisOperationStatus.Success, job.Id);
    }

    public async Task<VideoAnalysisJobResponse?> GetJobAsync(
        Guid id,
        Guid houseId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, false, cancellationToken);
        if (job is null || job.UserId != userId)
        {
            return null;
        }

        return new VideoAnalysisJobResponse(
            job.Id,
            job.Status,
            job.Transcript,
            job.Title,
            job.Author,
            job.Platform,
            job.Language,
            job.SourceUrl,
            DeserializeDetectedIngredients(job.DetectedIngredientsJson),
            job.ErrorReason,
            job.RecipeImportJobId);
    }

    public async Task ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is null || job.Status is VideoAnalysisJobStatus.Ready or VideoAnalysisJobStatus.Failed)
        {
            return;
        }

        await SetStatusAsync(job, VideoAnalysisJobStatus.Transcribing, cancellationToken);
        var transcript = await transcripts.GetAsync(new Uri(job.Url), cancellationToken);
        if (transcript.Status != VideoTranscriptStatus.Success || transcript.Content is null)
        {
            await FailAsync(job, transcript.Message ?? TranscriptError(transcript.Status), cancellationToken);
            return;
        }

        var content = transcript.Content;
        job.Transcript = content.Transcript;
        job.Title = content.Title;
        job.Author = content.Author;
        job.Platform = content.Platform.ToString();
        job.Language = content.Language;
        job.SourceUrl = content.SourceUrl?.ToString() ?? job.Url;
        await SetStatusAsync(job, VideoAnalysisJobStatus.DetectingIngredients, cancellationToken);

        var extracted = await extraction.ExtractAsync(job.UserId, content, cancellationToken);
        if (extracted.Status != RecipeExtractionStatus.Extracted || extracted.Draft is null)
        {
            await FailAsync(job, extracted.Message ?? ExtractionError(extracted.Status), cancellationToken);
            return;
        }

        job.DraftJson = JsonSerializer.Serialize(extracted.Draft, JsonOptions);
        await SetStatusAsync(job, VideoAnalysisJobStatus.MatchingIngredients, cancellationToken);
        var resolutions = await ingredientResolution.ResolveAsync(
            extracted.Draft.Ingredients.Select(ingredient => ingredient.Name).ToArray(),
            cancellationToken);
        var detectedIngredients = extracted.Draft.Ingredients.Zip(
            resolutions,
            (ingredient, resolution) => new DetectedIngredient(
                ingredient.Name,
                ingredient.Quantity,
                ingredient.Unit,
                ingredient.Note,
                resolution.MatchedIngredientId,
                resolution.MatchedIngredientName,
                resolution.MatchScore,
                resolution.IsProposedNew)).ToArray();
        job.DetectedIngredientsJson = JsonSerializer.Serialize(detectedIngredients, JsonOptions);
        job.Status = VideoAnalysisJobStatus.Ready;
        job.ErrorReason = null;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    public async Task<VideoAnalysisOperationResult> ContinueAsRecipeAsync(
        Guid id,
        Guid houseId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, true, cancellationToken);
        if (job is null || job.UserId != userId)
        {
            return new(VideoAnalysisOperationStatus.NotFound);
        }

        if (job.Status != VideoAnalysisJobStatus.Ready || job.DraftJson is null)
        {
            return new(VideoAnalysisOperationStatus.InvalidState);
        }

        if (job.RecipeImportJobId is { } existingId)
        {
            return new(VideoAnalysisOperationStatus.Success, existingId);
        }

        var extractedDraft = JsonSerializer.Deserialize<RecipeDraft>(job.DraftJson, JsonOptions);
        if (extractedDraft is null)
        {
            return new(VideoAnalysisOperationStatus.InvalidState);
        }

        var detectedIngredients = DeserializeDetectedIngredients(job.DetectedIngredientsJson);
        if (detectedIngredients.Count != extractedDraft.Ingredients.Count)
        {
            return new(VideoAnalysisOperationStatus.InvalidState);
        }

        var sourceUrl = job.SourceUrl ?? job.Url;
        var duplicates = await duplicateDetection.CheckExistsAsync(
            job.HouseId,
            extractedDraft.Title,
            sourceUrl,
            cancellationToken);
        var reviewDraft = new RecipeImportDraft(
            extractedDraft.Title,
            extractedDraft.Servings,
            extractedDraft.InstructionsMarkdown,
            extractedDraft.Gaps,
            detectedIngredients.Select(ingredient => new RecipeImportIngredient(
                ingredient.SourceName,
                ingredient.Quantity,
                ingredient.Unit,
                ingredient.Note,
                ingredient.MatchedIngredientId,
                ingredient.MatchedIngredientName,
                ingredient.MatchScore,
                ingredient.IsProposedNew)).ToArray(),
            duplicates.Matches);
        var result = await recipeImports.CreateReviewJobAsync(
            job.HouseId,
            job.UserId,
            sourceUrl,
            reviewDraft,
            cancellationToken);
        if (result.Status != RecipeImportOperationStatus.Success || result.Id is null)
        {
            return new(VideoAnalysisOperationStatus.InvalidState);
        }

        job.RecipeImportJobId = result.Id;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
        return new(VideoAnalysisOperationStatus.Success, result.Id);
    }

    public async Task FailAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is not null)
        {
            await FailAsync(job, reason, cancellationToken);
        }
    }

    private async Task SetStatusAsync(
        VideoAnalysisJob job,
        VideoAnalysisJobStatus status,
        CancellationToken cancellationToken)
    {
        job.Status = status;
        job.ErrorReason = null;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(
        VideoAnalysisJob job,
        string reason,
        CancellationToken cancellationToken)
    {
        job.Status = VideoAnalysisJobStatus.Failed;
        job.ErrorReason = reason;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<DetectedIngredient> DeserializeDetectedIngredients(string? json) =>
        json is null
            ? []
            : JsonSerializer.Deserialize<DetectedIngredient[]>(json, JsonOptions) ?? [];

    private static bool TryGetSupportedVideoUri(string? value, out Uri uri)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            uri = null!;
            return false;
        }

        var host = parsed.Host.TrimEnd('.').ToLowerInvariant();
        var isSupported = host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be" or
            "www.youtu.be" or "tiktok.com" or "www.tiktok.com" or "vm.tiktok.com" or "vt.tiktok.com";
        uri = parsed;
        return isSupported;
    }

    private static VideoAnalysisOperationResult Validation(string key, string message) =>
        new(
            VideoAnalysisOperationStatus.ValidationFailed,
            ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });

    private static string TranscriptError(VideoTranscriptStatus status) => status switch
    {
        VideoTranscriptStatus.InvalidUrl => "Only YouTube and TikTok video URLs are supported.",
        VideoTranscriptStatus.NotConfigured => "Video transcript provider is not configured.",
        VideoTranscriptStatus.NoTranscript => "The video has no spoken transcript.",
        VideoTranscriptStatus.OutOfCredits => "The transcript provider is out of credits.",
        VideoTranscriptStatus.PrivateVideo => "The video is private or unavailable.",
        VideoTranscriptStatus.RateLimited => "The transcript provider is temporarily rate limited.",
        _ => "Video transcript retrieval failed."
    };

    private static string ExtractionError(RecipeExtractionStatus status) => status switch
    {
        RecipeExtractionStatus.NotConfigured => "Configure an AI provider before analyzing a video.",
        RecipeExtractionStatus.NotARecipe => "The video does not contain a complete cookable recipe.",
        _ => "AI recipe extraction failed."
    };
}
