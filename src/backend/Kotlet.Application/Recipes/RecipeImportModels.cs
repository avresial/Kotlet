using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed record RecipeImportIngredient(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Note,
    Guid? IngredientId,
    string? MatchedName,
    bool IsProposedNew);

public sealed record RecipeImportDraft(
    string Title,
    int Servings,
    string InstructionsMarkdown,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<RecipeImportIngredient> Ingredients);

public sealed record RecipeImportJobResponse(
    Guid Id,
    RecipeImportJobStatus Status,
    RecipeImportDraft? Draft,
    string? ErrorReason);

public enum RecipeImportOperationStatus
{
    Success,
    NotFound,
    InvalidState,
    ValidationFailed
}

public sealed record RecipeImportOperationResult(
    RecipeImportOperationStatus Status,
    Guid? Id = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record StartRecipeImportRequest(string Url);
