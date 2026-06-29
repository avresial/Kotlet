using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Recipes;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

[McpServerToolType]
public sealed class KotletReadTools
{
    [McpServerTool(Name = "get_ingredients", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists all ingredients available in Kotlet, localized for the current request.")]
    public static Task<IReadOnlyCollection<IngredientDto>> GetIngredients(
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.GetAllAsync(language.Language, cancellationToken);

    [McpServerTool(Name = "get_ingredient", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Gets one Kotlet ingredient by its ID.")]
    public static async Task<IngredientDto> GetIngredient(
        [Description("The ingredient ID.")] Guid ingredientId,
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        await service.GetByIdAsync(ingredientId, language.Language, cancellationToken)
        ?? throw new KeyNotFoundException("Ingredient not found.");

    [McpServerTool(Name = "get_recipes", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists recipes in the authenticated user's active household.")]
    public static Task<PagedResponse<RecipeSummaryResponse>> GetRecipes(
        RecipeService service,
        ICurrentUser currentUser,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Recipes per page, from 1 to 100.")] int pageSize = 20,
        [Description("Optional text to search for in recipes.")] string? search = null,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(RequireHouse(currentUser), page, pageSize, search, cancellationToken);

    [McpServerTool(Name = "get_recipe", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Gets one recipe from the authenticated user's active household by its ID.")]
    public static async Task<RecipeDetailResponse> GetRecipe(
        [Description("The recipe ID.")] Guid recipeId,
        RecipeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken)
        ?? throw new KeyNotFoundException("Recipe not found.");

    [McpServerTool(Name = "get_meal_plan", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Gets the household meal plan for one date.")]
    public static Task<DailyMealPlanResponse> GetMealPlan(
        [Description("Date in yyyy-MM-dd format.")] string date,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            throw new ArgumentException("Date must use yyyy-MM-dd format.", nameof(date));

        return service.GetForDateAsync(
            RequireUser(currentUser), RequireHouse(currentUser), parsedDate, cancellationToken);
    }

    [McpServerTool(Name = "get_meal_plan_overview", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Gets a summary of planned household meal slots over a date range.")]
    public static Task<IReadOnlyList<MealPlanOverviewDay>> GetMealPlanOverview(
        [Description("First date in yyyy-MM-dd format.")] string from,
        [Description("Number of days to return, from 1 to 62.")] int days,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var parsedFrom))
            throw new ArgumentException("From must use yyyy-MM-dd format.", nameof(from));
        if (days is < 1 or > 62)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 62.");

        return service.GetOverviewAsync(RequireHouse(currentUser), parsedFrom, days, cancellationToken);
    }

    [McpServerTool(Name = "get_meal_plan_members", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists household members who can participate in planned meals.")]
    public static Task<IReadOnlyList<MealHouseMember>> GetMealPlanMembers(
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.GetHouseMembersAsync(RequireHouse(currentUser), cancellationToken);

    private static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    private static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");
}
