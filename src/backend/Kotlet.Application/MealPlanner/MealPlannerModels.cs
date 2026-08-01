namespace Kotlet.Application.MealPlanner;

public sealed record AddMealPlanItemRequest(
    DateOnly Date,
    string Slot,
    Guid? RecipeId,
    Guid? IngredientId,
    string? Note,
    Guid? PreparedMealId = null,
    IReadOnlyList<SelectedPreparedMealAddon>? Addons = null,
    string? FreeText = null);

public sealed record SelectedPreparedMealAddon(Guid IngredientId, decimal Quantity, string Unit);

public sealed record AddWeeklyMealPlanRequest(
    DateOnly WeekStart,
    IReadOnlyList<AddMealPlanItemRequest> Meals);

public sealed record AddWeeklyMealPlanResponse(
    IReadOnlyList<MealPlanItemResponse> Added,
    int Skipped);

public sealed record MealPlanPreviewItem(
    string Date,
    string Slot,
    string Type,
    Guid? RecipeId,
    Guid? IngredientId,
    Guid? PreparedMealId,
    string Title,
    int? Servings,
    decimal? CaloriesPerServing,
    IReadOnlyList<string> Ingredients,
    string? Note,
    string ResourceUri,
    string? FreeText = null);

public sealed record MealPlanPreviewResponse(
    string WeekStart,
    IReadOnlyList<MealPlanPreviewItem> Meals);

public sealed record MealPlanPreviewResult(
    MealPlannerOperationStatus Status,
    MealPlanPreviewResponse? Preview = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record CopyMealPlanDayRequest(DateOnly SourceDate, DateOnly TargetDate);
public sealed record CopyMealPlanWeekRequest(DateOnly SourceWeekStart, DateOnly TargetWeekStart);

public sealed record MoveMealPlanItemRequest(DateOnly Date, string Slot);

public sealed record MealPlanSourceRequest(
    Guid? RecipeId = null,
    Guid? IngredientId = null,
    Guid? PreparedMealId = null,
    string? FreeText = null,
    string? Note = null,
    IReadOnlyList<SelectedPreparedMealAddon>? Addons = null);

public sealed record ReplaceMealPlanRequest(
    Guid? MealId,
    DateOnly? Date,
    string? Slot,
    MealPlanSourceRequest Source,
    long? ExpectedVersion = null,
    string? IdempotencyKey = null,
    bool CreateIfMissing = false);

public sealed record MoveMealPlanMutationRequest(
    Guid MealId,
    DateOnly Date,
    string Slot,
    string DestinationBehavior = "reject",
    long? ExpectedVersion = null,
    long? DestinationExpectedVersion = null,
    string? IdempotencyKey = null);

public sealed record SwapMealPlanRequest(
    Guid FirstMealId,
    Guid SecondMealId,
    long? FirstExpectedVersion = null,
    long? SecondExpectedVersion = null,
    string? IdempotencyKey = null);

public sealed record ClearMealPlanSlotRequest(
    DateOnly Date,
    string Slot);

public sealed record ApplyMealPlanReplacementRequest(
    Guid MealId,
    MealPlanSourceRequest Source,
    long? ExpectedVersion = null,
    string? IdempotencyKey = null);

public sealed record MealPlanRecommendation(
    string Type,
    Guid? RecipeId,
    Guid? IngredientId,
    Guid? PreparedMealId,
    string? FreeText,
    string Title,
    string Reason,
    string ResourceUri);

public sealed record MealPlanRecommendationResponse(
    DateOnly Date,
    string Slot,
    IReadOnlyList<MealPlanRecommendation> Recommendations,
    bool SlotIsEmpty);

public sealed record MealPlanCandidate(
    Guid Id,
    string Date,
    string Slot,
    string Type,
    string Title,
    long Version);

public sealed record MealPlanMutationResponse(
    string Status,
    MealPlanItemResponse? Item = null,
    IReadOnlyList<MealPlanItemResponse>? Items = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null,
    IReadOnlyList<MealPlanCandidate>? Candidates = null);

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
    bool ServingsOverridden,
    string? FreeText = null,
    long Version = 1);

public sealed record DailyMealPlanResponse(
    string Date,
    IReadOnlyDictionary<string, IReadOnlyList<MealPlanItemResponse>> Meals);

public sealed record MealPlanOverviewDay(
    string Date,
    IReadOnlyList<string> PlannedSlots,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PlannedMeals);

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
