namespace Kotlet.Application.Recipes;

public sealed record RecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Note);

public sealed record CreateRecipeRequest(
    string Title,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientRequest> Ingredients);

public sealed record UpdateRecipeRequest(
    string Title,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientRequest> Ingredients);

public sealed record RecipeIngredientResponse(
    Guid Id,
    int SortOrder,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Note);

public sealed record RecipeSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    int IngredientCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    IReadOnlyList<RecipeImageResponse> Images,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public enum RecipeOperationStatus
{
    Success,
    NotFound,
    ValidationFailed
}

public sealed record RecipeOperationResult(
    RecipeOperationStatus Status,
    RecipeDetailResponse? Recipe = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
