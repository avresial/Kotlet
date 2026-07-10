using System.Text.Json;
using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
using Kotlet.Application.VideoTranscripts;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeImportService(
    IRecipeImportJobRepository jobs,
    IRecipeRepository recipes,
    IIngredientRepository ingredients,
    IngredientSearchService ingredientSearch,
    MeasurementMappingService measurements,
    VideoTranscriptService transcripts,
    AiRecipeExtractionService extraction,
    IRecipeImportSignal signal)
{
    private const decimal MatchThreshold = 0.75m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecipeImportOperationResult> CreateJobAsync(
        Guid houseId, Guid userId, string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Validation("url", "URL must be an absolute http(s) URL.");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new RecipeImportJob
        {
            Id = Guid.NewGuid(), HouseId = houseId, UserId = userId, Url = uri.ToString(),
            Status = RecipeImportJobStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        jobs.Add(job);
        await jobs.SaveChangesAsync(cancellationToken);
        signal.Enqueue(job.Id);
        return new(RecipeImportOperationStatus.Success, job.Id);
    }

    public async Task<RecipeImportJobResponse?> GetJobAsync(
        Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, false, cancellationToken);
        if (job is null) return null;
        var draft = job.DraftJson is null ? null : JsonSerializer.Deserialize<RecipeImportDraft>(job.DraftJson, JsonOptions);
        return new(job.Id, job.Status, draft, job.ErrorReason);
    }

    public async Task ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is null || job.Status is RecipeImportJobStatus.ReadyForReview or RecipeImportJobStatus.Failed) return;

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
        var draft = await ResolveAsync(extracted.Draft, cancellationToken);
        job.DraftJson = JsonSerializer.Serialize(draft, JsonOptions);
        job.Status = RecipeImportJobStatus.ReadyForReview;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, null, true, cancellationToken);
        if (job is not null) await FailAsync(job, reason, cancellationToken);
    }

    public async Task<RecipeImportOperationResult> AcceptAsync(
        Guid id, Guid houseId, Guid userId, RecipeImportDraft draft, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, true, cancellationToken);
        if (job is null) return new(RecipeImportOperationStatus.NotFound);
        if (job.Status != RecipeImportJobStatus.ReadyForReview)
            return new(RecipeImportOperationStatus.InvalidState);
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Servings is < 1 or > 99 || draft.Ingredients.Count == 0 ||
            draft.Ingredients.Any(x => x.Quantity is null or <= 0 || string.IsNullOrWhiteSpace(x.Unit)))
            return Validation("draft", "Title, servings, and positive ingredient quantities with units are required.");

        var existingIds = draft.Ingredients.Where(x => !x.IsProposedNew && x.IngredientId.HasValue)
            .Select(x => x.IngredientId!.Value).Distinct().ToArray();
        var catalog = (await ingredients.GetByIdsAsync(existingIds, cancellationToken)).ToDictionary();
        if (catalog.Count != existingIds.Length)
            return Validation("ingredients", "One or more matched ingredients no longer exist.");

        var proposed = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in draft.Ingredients.Where(x => x.IsProposedNew))
        {
            var name = line.Name.Trim();
            if (proposed.ContainsKey(name)) continue;
            var unit = CanonicalUnit(line.Unit!);
            var ingredient = new Ingredient
            {
                Id = Guid.NewGuid(), Name = name, MeasurementUnit = IsVolume(unit) ? "ml" : "g",
                // ponytail: one base unit per piece until imports can collect weight-per-piece metadata.
                IsCountable = unit == "piece", MeasurementUnitsPerPiece = unit == "piece" ? 1 : null,
                CaloriesPer100BaseUnits = Calories.Zero, PricePer100BaseUnits = Price.Zero,
                IsAiModified = true, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            proposed.Add(name, ingredient);
            ingredients.Add(ingredient);
            catalog.Add(ingredient.Id, ingredient);
        }

        var recipeId = Guid.NewGuid();
        var recipeIngredients = new List<RecipeIngredient>(draft.Ingredients.Count);
        for (var i = 0; i < draft.Ingredients.Count; i++)
        {
            var line = draft.Ingredients[i];
            var ingredient = line.IsProposedNew ? proposed[line.Name.Trim()] : catalog[line.IngredientId!.Value];
            var normalized = Normalize(line.Quantity!.Value, line.Unit!, ingredient);
            if (normalized is null)
                return Validation("ingredients", $"Unsupported measurement '{line.Unit}' for {line.Name}.");
            recipeIngredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredient.Id, Ingredient = ingredient,
                SortOrder = i, NormalizedQuantity = Quantity.FromAmount(normalized.Quantity),
                NormalizedUnit = normalized.Unit, Note = line.Note?.Trim()
            });
        }

        var title = draft.Title.Trim();
        var slug = await UniqueSlugAsync(houseId, RecipeService.GenerateSlug(title), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        recipes.Add(new Recipe
        {
            Id = recipeId, HouseId = houseId, OwnerUserId = userId, Title = title, Slug = slug,
            DescriptionMarkdown = draft.InstructionsMarkdown.Trim(), Servings = ServingCount.FromInt32(draft.Servings),
            IsAiAssisted = true, SourceUrl = job.Url, CreatedAtUtc = now, UpdatedAtUtc = now,
            Ingredients = recipeIngredients
        });
        jobs.Remove(job);
        await jobs.SaveChangesAsync(cancellationToken);
        return new(RecipeImportOperationStatus.Success, recipeId);
    }

    private async Task<RecipeImportDraft> ResolveAsync(RecipeDraft draft, CancellationToken cancellationToken)
    {
        var matches = await ingredientSearch.FindClosestAsync(draft.Ingredients.Select(x => x.Name).ToArray(), cancellationToken);
        return new(draft.Title, draft.Servings, draft.InstructionsMarkdown, draft.Gaps,
            draft.Ingredients.Zip(matches, (line, match) => new RecipeImportIngredient(
                line.Name, line.Quantity, line.Unit, line.Note,
                match.Similarity >= MatchThreshold ? match.IngredientId : null,
                match.Similarity >= MatchThreshold ? match.MatchedName : null,
                match.Similarity < MatchThreshold)).ToArray());
    }

    private async Task<string> UniqueSlugAsync(Guid houseId, string baseSlug, CancellationToken cancellationToken)
    {
        if (baseSlug.Length == 0) baseSlug = "imported-recipe";
        if (!await recipes.SlugExistsAsync(houseId, baseSlug, null, cancellationToken)) return baseSlug;
        for (var i = 2; i <= 1000; i++)
            if (!await recipes.SlugExistsAsync(houseId, $"{baseSlug}-{i}", null, cancellationToken)) return $"{baseSlug}-{i}";
        return $"{baseSlug}-{Guid.NewGuid():N}";
    }

    private NormalizedMeasurement? Normalize(decimal quantity, string rawUnit, Ingredient ingredient)
    {
        var unit = CanonicalUnit(rawUnit);
        if (unit == "kg") return measurements.Normalize(quantity * 1000, "g", ingredient);
        if (unit == "l") return measurements.Normalize(quantity * 1000, "ml", ingredient);
        return measurements.Normalize(quantity, unit, ingredient);
    }

    private static string CanonicalUnit(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "cups" => "cup", "tablespoon" or "tablespoons" => "tbsp",
        "teaspoon" or "teaspoons" => "tsp", "pieces" => "piece", var value => value
    };
    private static bool IsVolume(string unit) => unit is "ml" or "l" or "cup" or "tbsp" or "tsp";

    private async Task SetStatusAsync(RecipeImportJob job, RecipeImportJobStatus status, CancellationToken cancellationToken)
    {
        job.Status = status; job.ErrorReason = null; job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }
    private async Task FailAsync(RecipeImportJob job, string reason, CancellationToken cancellationToken)
    {
        job.Status = RecipeImportJobStatus.Failed; job.ErrorReason = reason; job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await jobs.SaveChangesAsync(cancellationToken);
    }
    private static RecipeImportOperationResult Validation(string key, string message) =>
        new(RecipeImportOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });
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
