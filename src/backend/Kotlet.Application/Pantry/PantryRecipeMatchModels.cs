namespace Kotlet.Application.Pantry;

public sealed record PantryRecipeMatchIngredientDto(Guid IngredientId, string Name);

public sealed record PantryRecipeMatchDto(
    Guid RecipeId,
    string Title,
    string Slug,
    int TotalIngredientCount,
    int MatchedIngredientCount,
    bool IsFullMatch,
    IReadOnlyList<PantryRecipeMatchIngredientDto> MissingIngredients);
