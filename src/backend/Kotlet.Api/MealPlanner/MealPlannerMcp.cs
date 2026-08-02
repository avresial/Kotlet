using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Application.MealPlanner;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.MealPlanner;

/// <summary>MCP tools and resources for the household meal planner.</summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class MealPlannerMcp
{
    [McpServerTool(Name = "get_meal_plan_overview", ReadOnly = true, OpenWorld = false),
     Description("Finds household meal-plan days and returns links to their complete MCP resources.")]
    public static async Task<IReadOnlyList<ResourceLinkBlock>> GetMealPlanOverview(
        [Description("First date in yyyy-MM-dd format.")] string from,
        [Description("Number of days to return, from 1 to 62.")] int days,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var parsedFrom))
            throw new McpException("From must use yyyy-MM-dd format.");
        if (days is < 1 or > 62)
            throw new McpException("Days must be between 1 and 62.");

        return (await service.GetOverviewAsync(
                RequireUser(currentUser), RequireHouse(currentUser), parsedFrom, days, cancellationToken))
            .Select(day => Link(
                $"kotlet://meal-plans/days/{day.Date}", $"Meal plan for {day.Date}",
                day.PlannedSlots.Count == 0
                    ? "No meals planned."
                    : $"Planned slots: {string.Join(", ", day.PlannedSlots)}."))
            .ToList();
    }

    [McpServerTool(Name = "get_meal_plan_members", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns the household members who can be assigned to meals, each with their user id and display " +
                "name. Call this to obtain the userIds needed by add_meal_participants.")]
    public static async Task<IReadOnlyList<MealHouseMember>> GetMealPlanMembers(
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await service.GetHouseMembersAsync(RequireHouse(currentUser), cancellationToken);

    [McpServerTool(Name = "get_meal_plan", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns a compact household meal plan for a range of days: planned meals, references, participants, guests, and servings. Use get_meal_plan_overview first when only checking which days have meals, or show_meal_plan when the user asks to see the plan.")]
    public static async Task<IReadOnlyList<DailyMealPlanResponse>> GetMealPlan(
        [Description("First date in yyyy-MM-dd format.")] string from,
        [Description("Number of days to return, from 1 to 31.")] int days,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var parsedFrom))
            throw new McpException("From must use yyyy-MM-dd format.");
        if (days is < 1 or > 31)
            throw new McpException("Days must be between 1 and 31.");

        var userId = RequireUser(currentUser);
        var houseId = RequireHouse(currentUser);
        return await service.GetForRangeAsync(userId, houseId, parsedFrom, days, cancellationToken);
    }

    [McpServerTool(Name = "meal_plan_get_range", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns the meal plan for an absolute yyyy-MM-dd date and range length. This is the read step for " +
                 "meal-plan replacement and movement; it never changes meals or the shopping list.")]
    public static async Task<IReadOnlyList<DailyMealPlanResponse>> GetMealPlanRange(
        [Description("First date in yyyy-MM-dd format; resolve relative dates before calling this tool.")] string from,
        [Description("Number of days to return, from 1 to 62.")] int days,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var parsedFrom))
            throw new McpException("From must use yyyy-MM-dd format.");
        if (days is < 1 or > 62)
            throw new McpException("Days must be between 1 and 62.");

        return await service.GetForRangeAsync(
            RequireUser(currentUser), RequireHouse(currentUser), parsedFrom, days, cancellationToken);
    }

    [McpServerTool(Name = "meal_plan_replace", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Replaces one meal-plan entry by mealId, or the only meal in an absolute yyyy-MM-dd date and slot. " +
                 "The source must contain exactly one recipeId, ingredientId, preparedMealId, or freeText. " +
                 "Use freeText only after recipe and ingredient lookup found no catalog match, the user approved an " +
                 "uncatalogued meal, and confirmUncatalogued is true. " +
                 "Use createIfMissing when an empty slot should be filled. Supply expectedVersion after reading the plan. " +
                 "The operation is atomic and does not mutate the shopping list.")]
    public static Task<MealPlanMutationResponse> ReplaceMealPlan(
        ReplaceMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.ReplaceAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "meal_plan_move", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Moves a meal to an absolute yyyy-MM-dd destination slot and clears its original slot. " +
                 "destinationBehavior is reject (default), replace, or swap. Empty and ambiguous destinations are " +
                 "reported explicitly; expectedVersion protects against concurrent edits. The shopping list is not changed.")]
    public static Task<MealPlanMutationResponse> MoveMealPlan(
        MoveMealPlanMutationRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.MoveAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "meal_plan_swap", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Atomically swaps the dates and slots of two meal-plan entries. Obtain both ids and versions from " +
                 "meal_plan_get_range; no shopping-list mutation occurs.")]
    public static Task<MealPlanMutationResponse> SwapMealPlan(
        SwapMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.SwapAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "meal_plan_clear_slot", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Clears every planned meal in one absolute yyyy-MM-dd slot. An already-empty slot is a successful no-op. " +
                 "The result includes the removed entries and the shopping list is not changed.")]
    public static Task<MealPlanMutationResponse> ClearMealPlanSlot(
        ClearMealPlanSlotRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.ClearSlotAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "meal_plan_recommend_replacement", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists up to five household recipes and prepared meals as replacement options for an absolute " +
                 "yyyy-MM-dd slot. Options are not ranked by date, slot, or preferences, and the tool does not change " +
                 "the meal plan or shopping list. Apply a selected option with meal_plan_apply_replacement.")]
    public static async Task<MealPlanRecommendationResponse> RecommendMealPlanReplacement(
        [Description("Date in yyyy-MM-dd format; resolve relative dates before calling this tool.")] string date,
        [Description("Slot: breakfast, second-breakfast, dinner, snack, or supper.")] string slot,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            throw new McpException("Date must use yyyy-MM-dd format.");
        var normalizedSlot = string.IsNullOrWhiteSpace(slot) ? null : slot.Trim().ToLowerInvariant();
        if (normalizedSlot is not ("breakfast" or "second-breakfast" or "dinner" or "snack" or "supper"))
            throw new McpException("Slot must be one of: breakfast, second-breakfast, dinner, snack, supper.");
        return await service.RecommendReplacementAsync(
            RequireHouse(currentUser), parsedDate, normalizedSlot, cancellationToken);
    }

    [McpServerTool(Name = "meal_plan_apply_replacement", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Applies a selected recommendation to an existing meal-plan entry by mealId. The source must contain " +
                 "exactly one recipeId, ingredientId, preparedMealId, or freeText. Use freeText only for a user-approved " +
                 "uncatalogued meal with confirmUncatalogued=true. Supply expectedVersion; the operation " +
                 "is atomic and leaves the shopping list unchanged.")]
    public static Task<MealPlanMutationResponse> ApplyMealPlanReplacement(
        ApplyMealPlanReplacementRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.ApplyReplacementAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "add_weekly_meal_plan", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds up to 35 meals within one seven-day period. Catalog meals must use ids obtained from " +
                 "get_recipes, get_ingredients, or get_prepared_meals. Existing identical meals are skipped; existing " +
                 "meals are never replaced. A freeText meal requires explicit user approval and " +
                 "confirmUncatalogued=true. Returns status, added/skipped counts, and the new meal ids.")]
    public static Task<WeeklyMealPlannerOperationResult> AddWeeklyMealPlan(
        [Description("The week start and meals to add. Every meal date must fall within the seven-day period.")]
        AddWeeklyMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddWeekAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "add_meal_to_plan", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Adds one meal to a single slot on a single day of the household meal plan. A meal is " +
                 "either a recipe, a bare ingredient, a prepared meal, or freeText — provide EXACTLY ONE of recipeId, ingredientId, " +
                "preparedMealId, or freeText, never more than one. " +
                "IMPORTANT: to plan a recipe, first call get_recipes with the full title. If there are no results, retry " +
                "with distinctive title terms and resolve ingredient clues with get_ingredients, then filter get_recipes " +
                "with ingredientIds. Choose the closest real catalog recipe and copy its id into recipeId. Never invent " +
                "or guess a recipe id, and never use freeText for a renamed or translated catalog recipe. " +
                "Use freeText only after these searches fail, the user explicitly approves an uncatalogued meal, and " +
                "confirmUncatalogued is true. " +
                "Likewise resolve a bare ingredient with get_ingredients first. Use add_weekly_meal_plan instead when " +
                "adding several meals across a week. The returned status is Success, NotFound, Conflict, or " +
                "ValidationFailed; on ValidationFailed inspect validationErrors for the offending field.")]
    public static Task<MealPlannerOperationResult> AddMealToPlan(
        [Description("The meal to add. date is yyyy-MM-dd. slot is one of: breakfast, second-breakfast, dinner, " +
                      "snack, supper. Set recipeId to a recipe id obtained from get_recipes, ingredientId to an " +
                      "ingredient id from get_ingredients, preparedMealId to a prepared meal id from " +
                      "get_prepared_meals, or freeText for a meal that is not in the catalog — supply exactly one. " +
                      "freeText also requires confirmUncatalogued=true after explicit user approval; note is optional free text.")]
        AddMealPlanItemRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddItemAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "add_meal_participants", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds one or more household members (\"users\") to an already-planned meal, on top of anyone " +
                "already assigned. Obtain the meal's id from get_meal_plan and the member ids from the " +
                "kotlet://meal-plans/members resource (or get_meal_plan_members). Ids that are already assigned are " +
                "ignored, so the call is safe to repeat. Every id must belong to your household or the call returns " +
                "ValidationFailed. This does NOT replace the participant list — use it to grow it.")]
    public static Task<MealPlannerOperationResult> AddMealParticipants(
        [Description("Meal id (the meal-plan item id) from get_meal_plan.")] Guid mealId,
        [Description("Household member ids to add to the meal, from the kotlet://meal-plans/members resource " +
                     "or get_meal_plan_members.")]
        IReadOnlyList<Guid> userIds,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddParticipantsAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, userIds, cancellationToken);

    [McpServerTool(Name = "set_meal_participants", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Replaces all household members assigned to a planned meal. Pass an empty userIds list to remove " +
                "every participant. Obtain mealId from get_meal_plan and userIds from get_meal_plan_members. " +
                "Returns status, meal id, assigned count, and resulting servings.")]
    public static Task<MealPlannerOperationResult> SetMealParticipants(
        [Description("Meal-plan item id from get_meal_plan.")] Guid mealId,
        [Description("Complete replacement list of household member ids, or an empty list.")] IReadOnlyList<Guid> userIds,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.SetParticipantsAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, userIds, cancellationToken);

    [McpServerTool(Name = "set_meal_participant_portion", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Sets one assigned household member's portion for a planned meal. Calories, quantities, cost, " +
                "shopping needs, and daily totals are recalculated from this percentage.")]
    public static Task<MealPlannerOperationResult> SetMealParticipantPortion(
        [Description("Meal-plan item id from get_meal_plan.")] Guid mealId,
        [Description("Assigned household member id from the meal's participants.")] Guid userId,
        [Description("Percentage of one regular serving, from 50 to 150.")] int portionPercent,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.SetParticipantPortionAsync(
            RequireUser(currentUser), RequireHouse(currentUser), mealId, userId, portionPercent, cancellationToken);

    [McpServerTool(Name = "set_meal_guests", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Sets the number of unregistered guests eating a planned meal. Each guest receives one regular serving.")]
    public static Task<MealPlannerOperationResult> SetMealGuests(
        [Description("Meal-plan item id from get_meal_plan.")] Guid mealId,
        [Description("Guest count, from 0 to 99.")] int guests,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.SetGuestsAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, guests, cancellationToken);

    [McpServerTool(Name = "set_meal_servings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Sets the legacy total serving override for an unassigned planned meal, or clears it with null. " +
                "For meals with household participants, use set_meal_participant_portion instead.")]
    public static Task<MealPlannerOperationResult> SetMealServings(
        [Description("Meal-plan item id from get_meal_plan.")] Guid mealId,
        [Description("Total servings from 0 to 99, or null to derive servings from people and guests.")] int? servings,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.SetServingsAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, servings, cancellationToken);

    [McpServerTool(Name = "move_meal_in_plan", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Moves one planned meal to another date and/or meal slot while preserving participants, portions, guests, and notes.")]
    public static Task<MealPlannerOperationResult> MoveMealInPlan(
        [Description("Meal-plan item id from get_meal_plan.")] Guid mealId,
        [Description("New yyyy-MM-dd date and slot: breakfast, second-breakfast, dinner, snack, or supper.")]
        MoveMealPlanItemRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.MoveItemAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, request, cancellationToken);

    [McpServerTool(Name = "copy_meal_plan_day", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Copies every meal, participant portion, guest, and note from one day to an empty target day.")]
    public static Task<CopyMealPlanDayResult> CopyMealPlanDay(
        [Description("Source and target dates in yyyy-MM-dd format.")] CopyMealPlanDayRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.CopyDayAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "copy_meal_plan_week", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Copies a complete Monday-to-Sunday meal plan to an empty target week, including participant portions, guests, and notes.")]
    public static Task<CopyMealPlanWeekResult> CopyMealPlanWeek(
        [Description("Source and target Monday dates in yyyy-MM-dd format.")] CopyMealPlanWeekRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.CopyWeekAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "remove_meal_from_plan", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one planned meal from the household meal plan. Identify the meal with " +
                "get_meal_plan first — each planned meal carries its own id. This only unplans the meal; the " +
                "underlying recipe and ingredients are left untouched. Returns { removed: true } when a meal was " +
                "deleted and { removed: false } when no meal with that id exists in your household.")]
    public static async Task<object> RemoveMealFromPlan(
        [Description("Meal id (the meal-plan item id) from get_meal_plan.")] Guid mealId,
        MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Removed(await service.RemoveItemAsync(RequireHouse(currentUser), mealId, cancellationToken)
            is MealPlannerOperationStatus.Success);

    [McpServerResource(UriTemplate = "kotlet://meal-plans/days/{date}", Name = "daily-meal-plan",
        Title = "Daily meal plan", MimeType = "application/json"),
     Description("Complete household meal plan for one yyyy-MM-dd date.")]
    public static async Task<string> DailyMealPlan(
        string date, MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            throw new ArgumentException("Date must use yyyy-MM-dd format.", nameof(date));
        return Json(await service.GetForDateAsync(
            RequireUser(currentUser), RequireHouse(currentUser), parsedDate, cancellationToken));
    }

    [McpServerResource(UriTemplate = "kotlet://meal-plans/weeks/{weekStart}", Name = "weekly-meal-plan",
        Title = "Weekly meal plan", MimeType = "application/json"),
     Description("Complete household meal plan for seven days beginning on the yyyy-MM-dd weekStart date.")]
    public static async Task<string> WeeklyMealPlan(
        string weekStart, MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var start))
            throw new ArgumentException("Week start must use yyyy-MM-dd format.", nameof(weekStart));

        var userId = RequireUser(currentUser);
        var houseId = RequireHouse(currentUser);
        return Json(await service.GetForRangeAsync(userId, houseId, start, 7, cancellationToken));
    }

    [McpServerResource(UriTemplate = "kotlet://meal-plans/members", Name = "meal-plan-members",
        Title = "Meal-plan household members", MimeType = "application/json"),
     Description("Household members who can participate in planned meals.")]
    public static async Task<string> MealPlanMembers(
        MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Json(await service.GetHouseMembersAsync(RequireHouse(currentUser), cancellationToken));
}
