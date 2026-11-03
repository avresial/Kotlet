using System.Text.Json;
using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Application.VideoTranscripts;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

/// <summary>
/// Runs a queued import job: fetches the video transcript, extracts a recipe draft with AI and
/// matches its ingredients, leaving the job ready for review or failed with a reason.
/// </summary>
public sealed class RecipeImportProcessor(
    IRecipeImportJobRepository jobs,
    RecipeDuplicateDetectionService duplicateDetection,
    IngredientResolutionService ingredientResolution,
    VideoTranscriptService transcripts,
    AiRecipeExtractionService extraction)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is null || job.Status is RecipeImportJobStatus.ReadyForReview or RecipeImportJobStatus.Failed)
        {
            return;
        }

        await SetStatusAsync(job, RecipeImportJobStatus.FetchingTranscript, cancellationToken);
        var transcript = await transcripts.GetAsync(new Uri(job.Url), cancellationToken);
        if (transcript.Status != VideoTranscriptStatus.Success || transcript.Content is null)
        {
            await FailAsync(job, transcript.Message ?? TranscriptError(transcript.Status), cancellationToken);
            return;
        }

        await SetStatusAsync(job, RecipeImportJobStatus.Extracting, cancellationToken);
        var extracted = await extraction.ExtractAsync(job.UserId, transcript.Content, cancellationToken);
        if (extracted.Status != RecipeExtractionStatus.Extracted || extracted.Draft is null)
        {
            await FailAsync(job, extracted.Message ?? ExtractionError(extracted.Status), cancellationToken);
            return;
        }

        await SetStatusAsync(job, RecipeImportJobStatus.ResolvingIngredients, cancellationToken);
        var draft = await ResolveAsync(job.HouseId, job.Url, extracted.Draft, cancellationToken);
        job.DraftJson = JsonSerializer.Serialize(draft, JsonOptions);
        job.Status = RecipeImportJobStatus.ReadyForReview;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is not null)
        {
            await FailAsync(job, reason, cancellationToken);
        }
    }

    private async Task<RecipeImportDraft> ResolveAsync(
        Guid houseId, string sourceUrl, RecipeDraft draft, CancellationToken cancellationToken)
    {
        var matches = await ingredientResolution.ResolveAsync(
            draft.Ingredients.Select(ingredient => ingredient.Name).ToArray(),
            cancellationToken);
        var duplicates = await duplicateDetection.CheckExistsAsync(houseId, draft.Title, sourceUrl, cancellationToken);
        return new(draft.Title, draft.Servings, draft.InstructionsMarkdown, draft.Gaps,
            draft.Ingredients.Zip(matches, (line, match) => new RecipeImportIngredient(
                line.Name, line.Quantity, line.Unit, line.Note,
                match.MatchedIngredientId,
                match.MatchedIngredientName,
                match.MatchScore,
                match.IsProposedNew)).ToArray(), duplicates.Matches);
    }

    private async Task SetStatusAsync(RecipeImportJob job, RecipeImportJobStatus status, CancellationToken cancellationToken)
    {
        job.Status = status;
        job.ErrorReason = null;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(RecipeImportJob job, string reason, CancellationToken cancellationToken)
    {
        job.Status = RecipeImportJobStatus.Failed;
        job.ErrorReason = reason;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

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
        RecipeExtractionStatus.NotConfigured => "Configure an AI provider before importing a recipe.",
        RecipeExtractionStatus.NotARecipe => "The video does not contain a complete cookable recipe.",
        _ => "AI recipe extraction failed."
    };
}
