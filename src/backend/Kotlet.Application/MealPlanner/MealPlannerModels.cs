namespace Kotlet.Application.MealPlanner;

public sealed record AddMealPlanItemRequest(
    DateOnly Date,
    string Slot,
    Guid? RecipeId,
    Guid? IngredientId,
    string? Note);

public sealed record MealPlanItemResponse(
    Guid Id,
    string Slot,
    string Type,
    Guid? RecipeId,
    Guid? IngredientId,
    string DisplayName,
    string? Note,
    int SortOrder);

public sealed record DailyMealPlanResponse(
    string Date,
    IReadOnlyDictionary<string, IReadOnlyList<MealPlanItemResponse>> Meals);

public enum MealPlannerOperationStatus
{
    Success,
    NotFound,
    ValidationFailed
}

public sealed record MealPlannerOperationResult(
    MealPlannerOperationStatus Status,
    MealPlanItemResponse? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
