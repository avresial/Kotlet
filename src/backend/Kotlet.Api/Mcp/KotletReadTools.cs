using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Recipes;
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
            throw new ArgumentException("From must use yyyy-MM-dd format.", nameof(from));
        if (days is < 1 or > 62)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 62.");

        return (await service.GetOverviewAsync(RequireHouse(currentUser), parsedFrom, days, cancellationToken))
            .Select(day => Link(
                $"kotlet://meal-plans/days/{day.Date}", $"Meal plan for {day.Date}",
                day.PlannedSlots.Count == 0
                    ? "No meals planned."
                    : $"Planned slots: {string.Join(", ", day.PlannedSlots)}."))
            .ToList();
    }

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
