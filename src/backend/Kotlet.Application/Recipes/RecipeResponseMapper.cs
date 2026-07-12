using Kotlet.Application.Measurements;
using Kotlet.Application.Translations;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeResponseMapper(
    MeasurementMappingService measurementMappingService,
    ITranslationRepository translations)
{
    public async Task<RecipeDetailResponse> ToDetailResponseAsync(
        Recipe recipe, string languageCode, IReadOnlyList<RecipeImageResponse>? images = null, bool canEdit = false, CancellationToken cancellationToken = default)
    {
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        var ingredients = recipe.Ingredients
            .OrderBy(i => i.SortOrder)
            .Select(i =>
            {
                var display = measurementMappingService.ToDisplay(i.NormalizedQuantity.Amount, i.NormalizedUnit, i.Ingredient);
                var resolvedName = ResolveName(i.IngredientId, i.Ingredient.Name, languageCode, dictionary);
                return new RecipeIngredientResponse(i.Id, i.SortOrder, i.IngredientId, resolvedName,
                    display.Quantity, display.Unit, i.NormalizedQuantity.Amount, i.NormalizedUnit, i.Note);
            })
            .ToList();

        return new RecipeDetailResponse(recipe.Id, recipe.Title, recipe.Slug, recipe.OwnerUserId, recipe.DescriptionMarkdown, recipe.Servings.Value, recipe.MealType?.ToApiValue(),
            ingredients,
            images ?? [],
            canEdit,
            recipe.IsAiAssisted, recipe.SourceUrl,
            recipe.CreatedAtUtc, recipe.UpdatedAtUtc);
    }

    public RecipeSummaryResponse ToSummaryResponse(
        Recipe recipe, IReadOnlyDictionary<Guid, Guid>? firstImageIds = null)
    {
        string? firstImageUrl = null;
        if (firstImageIds is not null && firstImageIds.TryGetValue(recipe.Id, out var imageId))
            firstImageUrl = $"/api/recipes/{recipe.Id}/images/{imageId}/content";
        return new(recipe.Id, recipe.Title, recipe.Slug, recipe.OwnerUserId, recipe.Ingredients.Count, recipe.Servings.Value, recipe.MealType?.ToApiValue(),
            firstImageUrl, recipe.IsAiAssisted, recipe.CreatedAtUtc, recipe.UpdatedAtUtc);
    }

    public static RecipeImageResponse ToImageResponse(RecipeImage i) => new(i.Id, i.RecipeId, i.FileName,
        i.ContentType, i.FileSizeBytes, i.AltText, i.SortOrder,
        $"/api/recipes/{i.RecipeId}/images/{i.Id}/content", i.CreatedAtUtc,
        SourceAttributionResponse.FromPrimarySource(i));

    private Task<IReadOnlyDictionary<string, string>> LoadTranslationsAsync(string languageCode, CancellationToken cancellationToken) =>
        TranslationKeys.IsDefaultLanguage(languageCode)
            ? Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>())
            : translations.GetAllAsync(cancellationToken);

    private static string ResolveName(Guid ingredientId, string fallback, string languageCode, IReadOnlyDictionary<string, string> dictionary) =>
        !TranslationKeys.IsDefaultLanguage(languageCode)
        && dictionary.TryGetValue(TranslationKeys.Ingredient(ingredientId, languageCode), out var translated)
        && !string.IsNullOrWhiteSpace(translated) ? translated : fallback;
}
