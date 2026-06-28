namespace Kotlet.Application.MealPlanner;

public sealed record AddMealPlanItemRequest(
    DateOnly Date,
    string Slot,
    Guid? RecipeId,
    Guid? IngredientId,
    string? Note);

public sealed record SetParticipantsRequest(IReadOnlyList<Guid> UserIds);

public sealed record SetServingsRequest(int? Servings);

public sealed record MealParticipantResponse(
    Guid UserId,
    string DisplayName,
    bool IsCurrentUser);

public sealed record MealPlanItemResponse(
    Guid Id,
    string Slot,
    string Type,
    Guid? RecipeId,
    Guid? IngredientId,
    string DisplayName,
    string? Note,
    int SortOrder,
    IReadOnlyList<MealParticipantResponse> Participants,
    int Servings,
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
    ValidationFailed
}

public sealed record MealPlannerOperationResult(
    MealPlannerOperationStatus Status,
    MealPlanItemResponse? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
