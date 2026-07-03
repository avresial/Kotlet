using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Application.Shopping;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

[McpServerToolType]
public sealed class KotletReadTools
{
    [McpServerTool(Name = "get_ingredients", ReadOnly = true, OpenWorld = false),
     Description("Finds ingredients and returns links to their complete MCP resources.")]
    public static async Task<IReadOnlyList<ResourceLinkBlock>> GetIngredients(
        IngredientService service,
        ILanguageContext language,
        [Description("Optional text to search for in ingredient names.")] string? search = null,
        CancellationToken cancellationToken = default) =>
        (await service.GetAllAsync(language.Language, cancellationToken))
        .Where(ingredient => string.IsNullOrWhiteSpace(search)
            || ingredient.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
        .Select(ingredient => Link(
            $"kotlet://ingredients/{ingredient.Id}", ingredient.Name,
            $"Ingredient measured in {ingredient.MeasurementUnit}."))
        .ToList();

    [McpServerTool(Name = "get_recipes", ReadOnly = true, OpenWorld = false),
     Description("Searches household recipes and returns links to their complete MCP resources.")]
    public static async Task<IReadOnlyList<ResourceLinkBlock>> GetRecipes(
        RecipeService service,
        ICurrentUser currentUser,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Recipes per page, from 1 to 100.")] int pageSize = 20,
        [Description("Optional text to search for in recipes.")] string? search = null,
        CancellationToken cancellationToken = default) =>
        (await service.ListAsync(RequireHouse(currentUser), page, pageSize, search, null, cancellationToken)).Items
        .Select(recipe => Link(
            $"kotlet://recipes/{recipe.Id}", recipe.Title,
            $"Recipe for {recipe.Servings} serving(s) with {recipe.IngredientCount} ingredient(s)."))
        .ToList();

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

    [McpServerTool(Name = "get_ingredient", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one ingredient with complete details: measurement unit, calories, price, category, allergens, attributes, and dietary suitability.")]
    public static async Task<McpIngredient> GetIngredient(
        [Description("Ingredient ID from get_ingredients.")] Guid ingredientId,
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        McpIngredient.From(await service.GetByIdAsync(ingredientId, language.Language, cancellationToken)
                           ?? throw new McpException("Ingredient not found."));

    [McpServerTool(Name = "get_recipe", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one complete household recipe: Markdown description with preparation steps, servings, and the full ingredient list with quantities.")]
    public static async Task<RecipeDetailResponse> GetRecipe(
        [Description("Recipe ID from get_recipes.")] Guid recipeId,
        RecipeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken)
        ?? throw new McpException("Recipe not found.");

    [McpServerTool(Name = "get_shopping_list", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns the household's complete shopping list, including quantities, prices, and purchased state.")]
    public static async Task<IReadOnlyList<McpShoppingListItem>> GetShoppingList(
        ShoppingListService service,
        ICurrentUser currentUser,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        (await service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken))
        .Select(McpShoppingListItem.From)
        .ToList();

    [McpServerTool(Name = "get_pantry", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns the household's complete pantry: every stored ingredient with quantity, expiration date, and storage location.")]
    public static async Task<IReadOnlyList<McpPantryItem>> GetPantry(
        PantryService service,
        ICurrentUser currentUser,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        (await service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken))
        .Select(McpPantryItem.From)
        .ToList();

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

    private static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    private static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");

    private static ResourceLinkBlock Link(string uri, string title, string description) => new()
    {
        Uri = uri,
        Name = title,
        Title = title,
        Description = description,
        MimeType = "application/json"
    };
}
