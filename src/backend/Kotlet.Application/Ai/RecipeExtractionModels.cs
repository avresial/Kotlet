namespace Kotlet.Application.Ai;

public sealed record DraftIngredient(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Note = null);

public sealed record RecipeDraft(
    string Title,
    int Servings,
    IReadOnlyList<DraftIngredient> Ingredients,
    string InstructionsMarkdown,
    IReadOnlyList<string> Gaps);

public enum RecipeExtractionStatus
{
    Extracted,
    InvalidRequest,
    NotConfigured,
    NotARecipe,
    Failed
}

public sealed record RecipeExtractionResult(
    RecipeExtractionStatus Status,
    RecipeDraft? Draft = null,
    string? Message = null);
