using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Recipes;
using Kotlet.Application.Shopping;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;

namespace Kotlet.Api.Mcp;

[McpServerResourceType]
public sealed class KotletResources
{
    [McpServerResource(UriTemplate = "kotlet://identity", Name = "identity",
        Title = "Current Kotlet identity", MimeType = "application/json"),
     Description("Authenticated Kotlet user identity and roles.")]
    public static string Identity(ClaimsPrincipal user) => Json(new
    {
        UserId = user.FindFirstValue(OpenIddictConstants.Claims.Subject),
        Name = user.FindFirstValue(OpenIddictConstants.Claims.Name),
        Email = user.FindFirstValue(OpenIddictConstants.Claims.Email),
        Roles = user.FindAll(OpenIddictConstants.Claims.Role).Select(claim => claim.Value).ToArray()
    });

    [McpServerResource(UriTemplate = "kotlet://ingredients/{ingredientId}", Name = "ingredient",
        Title = "Kotlet ingredient", MimeType = "application/json"),
     Description("Complete ingredient details, including measurement, calories, price, and localization.")]
    public static async Task<string> Ingredient(
        Guid ingredientId, IngredientService service, ILanguageContext language, CancellationToken cancellationToken) =>
        Json(await service.GetByIdAsync(ingredientId, language.Language, cancellationToken)
             ?? throw new KeyNotFoundException("Ingredient not found."));

    [McpServerResource(UriTemplate = "kotlet://recipes/{recipeId}", Name = "recipe",
        Title = "Kotlet recipe", MimeType = "application/json"),
     Description("Complete household recipe, including description, servings, ingredients, and images.")]
    public static async Task<string> Recipe(
        Guid recipeId, RecipeService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Json(await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken)
             ?? throw new KeyNotFoundException("Recipe not found."));

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

    [McpServerResource(UriTemplate = "kotlet://shopping-list", Name = "shopping-list",
        Title = "Shopping list", MimeType = "application/json"),
     Description("Complete shopping-list snapshot for the authenticated household.")]
    public static async Task<string> ShoppingList(
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        Json(await service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken));

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);

    private static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    private static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");
}
