namespace Kotlet.Application.Recipes;

public sealed record RecipeIngredientRequest(
    Guid IngredientId,
    decimal Quantity,
    string Unit,
    string? Note);

public sealed record CreateRecipeRequest(
    string Title,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    int Servings = 1,
    string? MealType = null,
    string? SourceUrl = null,
    bool IsAiAssisted = false);

// No IsAiAssisted here: the flag records provenance and survives human edits.
public sealed record UpdateRecipeRequest(
    string Title,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    int Servings = 1,
    string? MealType = null,
    string? SourceUrl = null);

public sealed record RecipeIngredientResponse(
    Guid Id,
    int SortOrder,
    Guid IngredientId,
    string Name,
    decimal Quantity,
    string Unit,
    decimal NormalizedQuantity,
    string NormalizedUnit,
    string? Note);

public sealed record RecipeSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    Guid CreatedByUserId,
    int IngredientCount,
    int Servings,
    string? MealType,
    string? FirstImageUrl,
    bool IsAiAssisted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    Guid CreatedByUserId,
    string? DescriptionMarkdown,
    int Servings,
    string? MealType,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    IReadOnlyList<RecipeImageResponse> Images,
    bool CanEdit,
    bool IsAiAssisted,
    string? SourceUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public enum RecipeMatchType
{
    SourceUrl,
    ExactTitle,
    SimilarTitle
}

public sealed record RecipeExistenceMatch(
    Guid RecipeId,
    string Title,
    string? SourceUrl,
    RecipeMatchType MatchType);

public sealed record RecipeExistenceResult(
    bool Exists,
    IReadOnlyList<RecipeExistenceMatch> Matches);

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
