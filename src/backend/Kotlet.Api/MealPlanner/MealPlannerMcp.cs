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

        return (await service.GetOverviewAsync(RequireHouse(currentUser), parsedFrom, days, cancellationToken))
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
     Description("Returns the household's complete meal plan for a range of days: every planned meal per slot with participants and servings. Use get_meal_plan_overview first when only checking which days have meals.")]
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
        var plan = new List<DailyMealPlanResponse>(days);
        for (var offset = 0; offset < days; offset++)
            plan.Add(await service.GetForDateAsync(userId, houseId, parsedFrom.AddDays(offset), cancellationToken));
        return plan;
    }

    [McpServerTool(Name = "add_weekly_meal_plan", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds up to 35 meals within one seven-day period. Existing identical meals are skipped; existing meals are never replaced.")]
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
                "either a recipe or a bare ingredient — provide EXACTLY ONE of recipeId or ingredientId, never both. " +
                "IMPORTANT: to plan a recipe you must first look it up — call get_recipes (optionally with a search " +
                "term) to find the recipe and copy its id into recipeId. Never invent or guess a recipe id. " +
                "Likewise resolve a bare ingredient with get_ingredients first. Use add_weekly_meal_plan instead when " +
                "adding several meals across a week. The returned status is Success, NotFound, Conflict, or " +
                "ValidationFailed; on ValidationFailed inspect validationErrors for the offending field.")]
    public static Task<MealPlannerOperationResult> AddMealToPlan(
        [Description("The meal to add. date is yyyy-MM-dd. slot is one of: breakfast, second-breakfast, dinner, " +
                     "snack, supper. Set recipeId to a recipe id obtained from get_recipes, OR ingredientId to an " +
                     "ingredient id from get_ingredients — supply exactly one. note is optional free text.")]
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
                "every participant. Obtain mealId from get_meal_plan and userIds from get_meal_plan_members.")]
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
        var days = new List<DailyMealPlanResponse>(7);
        for (var offset = 0; offset < 7; offset++)
            days.Add(await service.GetForDateAsync(userId, houseId, start.AddDays(offset), cancellationToken));
        return Json(days);
    }

    [McpServerResource(UriTemplate = "kotlet://meal-plans/members", Name = "meal-plan-members",
        Title = "Meal-plan household members", MimeType = "application/json"),
     Description("Household members who can participate in planned meals.")]
    public static async Task<string> MealPlanMembers(
        MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Json(await service.GetHouseMembersAsync(RequireHouse(currentUser), cancellationToken));
}
