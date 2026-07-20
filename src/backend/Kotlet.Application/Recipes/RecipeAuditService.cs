using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

/// <summary>
/// Detects recipes with missing data. Missing ingredients or description is important
/// (the recipe cannot really be cooked from it); a missing image or meal type is minor.
/// </summary>
public sealed class RecipeAuditService(
    IRecipeRepository repository,
    IRecipeImageRepository? imageRepository = null)
{
    public const int DefaultLimit = 5;

    public async Task<IReadOnlyList<RecipeAuditItemResponse>> ListRecipesRequiringFixAsync(
        Guid houseId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 20);
        var recipes = await repository.GetAllWithIngredientsAsync(houseId, cancellationToken);
        // Without an image repository we cannot tell whether images exist, so skip that check.
        var firstImageIds = imageRepository is null || recipes.Count == 0
            ? null
            : await imageRepository.GetFirstImageIdsAsync(recipes.Select(r => r.Id).ToList(), cancellationToken);

        return recipes
            .Select(recipe => Audit(recipe, hasImage: firstImageIds?.ContainsKey(recipe.Id) ?? true))
            .OfType<RecipeAuditItemResponse>()
            .OrderBy(item => item.Importance == RecipeAuditImportance.Important ? 0 : 1)
            .ThenByDescending(item => item.MissingCount)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static RecipeAuditItemResponse? Audit(Recipe recipe, bool hasImage)
    {
        var important = new List<string>();
        var minor = new List<string>();

        if (recipe.Ingredients.Count == 0) important.Add(RecipeAuditElements.Ingredients);
        if (string.IsNullOrWhiteSpace(recipe.DescriptionMarkdown)) important.Add(RecipeAuditElements.Description);
        if (!hasImage) minor.Add(RecipeAuditElements.Image);
        if (recipe.MealType is null) minor.Add(RecipeAuditElements.MealType);

        if (important.Count == 0 && minor.Count == 0) return null;
        return new RecipeAuditItemResponse(
            recipe.Id,
            recipe.Title,
            recipe.Slug,
            important.Count > 0 ? RecipeAuditImportance.Important : RecipeAuditImportance.Minor,
            [.. important, .. minor],
            important.Count + minor.Count);
    }
}
