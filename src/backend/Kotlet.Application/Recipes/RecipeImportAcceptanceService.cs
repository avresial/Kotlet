using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Sources;

namespace Kotlet.Application.Recipes;

/// <summary>
/// Turns a reviewed import draft into a household recipe, creating any proposed ingredients,
/// and removes the import job.
/// </summary>
public sealed class RecipeImportAcceptanceService(
    IRecipeImportJobRepository jobs,
    IRecipeRepository recipes,
    IIngredientRepository ingredients,
    MeasurementMappingService measurements)
{
    /// <summary>Provider recorded on AiAssisted sources; all AI extraction goes through OpenRouter.</summary>
    public const string AiSourceProvider = "OpenRouter";

    private const string DefaultImportedRecipeSlug = "imported-recipe";

    public async Task<RecipeImportOperationResult> AcceptAsync(
        Guid id, Guid houseId, Guid userId, RecipeImportDraft draft, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, true, cancellationToken);
        if (job is null)
        {
            return new(RecipeImportOperationStatus.NotFound);
        }
        if (job.UserId != userId)
        {
            return new(RecipeImportOperationStatus.NotFound);
        }
        if (job.Status != RecipeImportJobStatus.ReadyForReview)
        {
            return new(RecipeImportOperationStatus.InvalidState);
        }
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Servings is < 1 or > 99 || draft.Ingredients.Count == 0 ||
            draft.Ingredients.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Quantity is null or <= 0 ||
                string.IsNullOrWhiteSpace(x.Unit) || (!x.IsProposedNew && x.IngredientId is null)))
            return Validation("draft", "Title, servings, and positive ingredient quantities with units are required.");

        var existingIds = draft.Ingredients.Where(x => !x.IsProposedNew && x.IngredientId.HasValue)
            .Select(x => x.IngredientId!.Value).Distinct().ToArray();
        var catalog = (await ingredients.GetByIdsAsync(existingIds, cancellationToken)).ToDictionary();
        if (catalog.Count != existingIds.Length)
        {
            return Validation("ingredients", "One or more matched ingredients no longer exist.");
        }

        var proposed = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in draft.Ingredients.Where(x => x.IsProposedNew))
        {
            var name = line.Name.Trim();
            if (proposed.ContainsKey(name))
            {
                continue;
            }
            var unit = MeasurementMappingService.CanonicalUnit(line.Unit!);
            var ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = name,
                MeasurementUnit = MeasurementMappingService.IsVolumeUnit(unit) ? "ml" : "g",
                // ponytail: one base unit per piece until imports can collect weight-per-piece metadata.
                IsCountable = unit == "piece",
                MeasurementUnitsPerPiece = unit == "piece" ? 1 : null,
                CaloriesPer100BaseUnits = Calories.Zero,
                PricePer100BaseUnits = Price.Zero,
                IsAiModified = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
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
            var normalized = measurements.NormalizeWithAliases(line.Quantity!.Value, line.Unit!, ingredient);
            if (normalized is null)
            {
                return Validation("ingredients", $"Unsupported measurement '{line.Unit}' for {line.Name}.");
            }
            recipeIngredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                IngredientId = ingredient.Id,
                Ingredient = ingredient,
                SortOrder = i,
                NormalizedQuantity = Quantity.FromAmount(normalized.Quantity),
                NormalizedUnit = normalized.Unit,
                Note = line.Note?.Trim()
            });
        }

        var title = draft.Title.Trim();
        var slug = await UniqueSlugAsync(houseId, RecipeService.GenerateSlug(title), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        recipes.Add(new Recipe
        {
            Id = recipeId,
            HouseId = houseId,
            OwnerUserId = userId,
            Title = title,
            Slug = slug,
            DescriptionMarkdown = draft.InstructionsMarkdown.Trim(),
            Servings = ServingCount.FromInt32(draft.Servings),
            IsAiAssisted = true,
            SourceUrl = job.Url,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Ingredients = recipeIngredients,
            Sources =
            [
                new RecipeSource
                {
                    RecipeId = recipeId,
                    Source = new Source
                    {
                        Id = Guid.NewGuid(), Type = SourceType.AiAssisted, Provider = AiSourceProvider,
                        Url = job.Url, RetrievedAtUtc = now
                    }
                }
            ]
        });
        jobs.Remove(job);
        await jobs.SaveChangesAsync(cancellationToken);
        return new(RecipeImportOperationStatus.Success, recipeId);
    }

    private async Task<string> UniqueSlugAsync(Guid houseId, string baseSlug, CancellationToken cancellationToken)
    {
        if (baseSlug.Length == 0)
        {
            baseSlug = DefaultImportedRecipeSlug;
        }
        if (!await recipes.SlugExistsAsync(houseId, baseSlug, null, cancellationToken))
        {
            return baseSlug;
        }

        for (var i = 2; i <= 1000; i++)
        {
            if (!await recipes.SlugExistsAsync(houseId, $"{baseSlug}-{i}", null, cancellationToken))
            {
                return $"{baseSlug}-{i}";
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}";
    }

    private static RecipeImportOperationResult Validation(string key, string message) =>
        new(RecipeImportOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });
}
