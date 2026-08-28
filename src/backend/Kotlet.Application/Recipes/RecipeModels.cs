using Kotlet.Domain.Common;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;

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
    bool IsAiAssisted = false,
    string? VideoUrl = null,
    string? VideoThumbnailUrl = null);

// No IsAiAssisted here: the flag records provenance and survives human edits.
public sealed record UpdateRecipeRequest(
    string Title,
    string? DescriptionMarkdown,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    int Servings = 1,
    string? MealType = null,
    string? SourceUrl = null,
    string? VideoUrl = null,
    string? VideoThumbnailUrl = null);

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
    string? DescriptionMarkdown,
    string? FirstImageUrl,
    bool IsAiAssisted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeSummaryData(
    Guid Id,
    string Title,
    string Slug,
    Guid OwnerUserId,
    int IngredientCount,
    ServingCount Servings,
    MealSlot? MealType,
    string? DescriptionMarkdown,
    bool IsAiAssisted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static RecipeSummaryData FromRecipe(Recipe recipe) => new(
        recipe.Id,
        recipe.Title,
        recipe.Slug,
        recipe.OwnerUserId,
        recipe.Ingredients.Count,
        recipe.Servings,
        recipe.MealType,
        recipe.DescriptionMarkdown,
        recipe.IsAiAssisted,
        recipe.CreatedAtUtc,
        recipe.UpdatedAtUtc);
}

public sealed record RecipePlanningIngredientResponse(Guid Id, string Name);

public sealed record RecipePlanningSummaryResponse(
    Guid Id,
    string Title,
    int Servings,
    string? MealType,
    IReadOnlyList<RecipePlanningIngredientResponse> Ingredients,
    string? DescriptionMarkdown,
    string? FirstImageUrl);

public sealed record RecipePlanningSearchResponse(
    IReadOnlyList<RecipePlanningSummaryResponse> Recipes,
    int TotalCount);

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
    string? VideoUrl,
    string? VideoThumbnailUrl,
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
