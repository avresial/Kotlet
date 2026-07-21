namespace Kotlet.Application.MealPlanner;

public sealed record AddMealPlanItemRequest(
    DateOnly Date,
    string Slot,
    Guid? RecipeId,
    Guid? IngredientId,
    string? Note,
    Guid? PreparedMealId = null,
    IReadOnlyList<SelectedPreparedMealAddon>? Addons = null);

public sealed record SelectedPreparedMealAddon(Guid IngredientId, decimal Quantity, string Unit);

public sealed record AddWeeklyMealPlanRequest(
    DateOnly WeekStart,
    IReadOnlyList<AddMealPlanItemRequest> Meals);

public sealed record AddWeeklyMealPlanResponse(
    IReadOnlyList<MealPlanItemResponse> Added,
    int Skipped);

public sealed record CopyMealPlanDayRequest(DateOnly SourceDate, DateOnly TargetDate);
public sealed record CopyMealPlanWeekRequest(DateOnly SourceWeekStart, DateOnly TargetWeekStart);

public sealed record MoveMealPlanItemRequest(DateOnly Date, string Slot);

public sealed record SetParticipantsRequest(IReadOnlyList<Guid> UserIds);

public sealed record SetParticipantPortionRequest(int PortionPercent);

public sealed record SetServingsRequest(int? Servings);

public sealed record SetGuestsRequest(int Guests);

public sealed record MealParticipantResponse(
    Guid UserId,
    string DisplayName,
    bool IsCurrentUser,
    int PortionPercent);

public sealed record MealPlanItemResponse(
    Guid Id,
    string Slot,
    string Type,
    Guid? RecipeId,
    Guid? IngredientId,
    Guid? PreparedMealId,
    Guid? ParentMealPlanItemId,
    decimal? IngredientQuantity,
    string? IngredientUnit,
    string DisplayName,
    string? Note,
    int SortOrder,
    IReadOnlyList<MealParticipantResponse> Participants,
    int Guests,
    decimal Servings,
    bool ServingsOverridden);

public sealed record DailyMealPlanResponse(
    string Date,
    IReadOnlyDictionary<string, IReadOnlyList<MealPlanItemResponse>> Meals);

public sealed record MealPlanOverviewDay(
    string Date,
    IReadOnlyList<string> PlannedSlots);

/// <summary>A member of the current user's house, available to assign to meals.</summary>
public sealed record MealHouseMember(Guid UserId, string DisplayName);

public enum MealPlannerOperationStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}

public sealed record MealPlannerOperationResult(
    MealPlannerOperationStatus Status,
    MealPlanItemResponse? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record WeeklyMealPlannerOperationResult(
    MealPlannerOperationStatus Status,
    AddWeeklyMealPlanResponse? Plan = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record CopyMealPlanDayResult(
    MealPlannerOperationStatus Status,
    DailyMealPlanResponse? Plan = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record CopyMealPlanWeekResult(
    MealPlannerOperationStatus Status,
    int Copied = 0,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
