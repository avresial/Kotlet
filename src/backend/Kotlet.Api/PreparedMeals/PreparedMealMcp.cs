using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Application.PreparedMeals;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.PreparedMeals;

/// <summary>MCP tools and resources for household prepared meals.</summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class PreparedMealMcp
{
    [McpServerTool(Name = "get_prepared_meals", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns compact prepared-meal candidates for planning: ID, name, category, servings, calories, price, and only required/default add-ons. Use get_prepared_meal for full package and preparation details.")]
    public static async Task<IReadOnlyList<McpPreparedMealSummary>> GetPreparedMeals(
        PreparedMealService service,
        ICurrentUser currentUser,
        [Description("Whether to include archived prepared meals.")] bool includeArchived = false,
        [Description("Optional case-insensitive name, brand, or category search.")] string? search = null,
        [Description("Maximum compact results, from 1 to 20. Default 10.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var meals = await service.ListAsync(RequireHouse(currentUser), includeArchived, cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            meals = meals.Where(meal =>
                    meal.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (meal.Brand?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (meal.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
        return meals.Take(limit).Select(McpPreparedMealSummary.From).ToList();
    }

    [McpServerTool(Name = "get_prepared_meal", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one active prepared meal with its package details, nutrition, instructions, and add-ons.")]
    public static async Task<PreparedMealResponse> GetPreparedMeal(
        [Description("Prepared-meal ID from get_prepared_meals.")] Guid preparedMealId,
        PreparedMealService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await service.GetAsync(preparedMealId, RequireHouse(currentUser), cancellationToken)
        ?? throw new McpException("Prepared meal not found.");

    [McpServerTool(Name = "add_prepared_meal", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Adds a prepared meal to the authenticated household. Add-ons must reference existing " +
                 "ingredient IDs from get_ingredients.")]
    public static Task<PreparedMealOperationResult> AddPreparedMeal(
        [Description("Complete prepared meal to create. Use an empty add-ons list when it has none.")]
        SavePreparedMealRequest request,
        PreparedMealService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.CreateAsync(RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "update_prepared_meal", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Replaces the editable details and add-ons of one prepared meal.")]
    public static Task<PreparedMealOperationResult> UpdatePreparedMeal(
        [Description("Prepared-meal ID from get_prepared_meals.")] Guid preparedMealId,
        [Description("Complete replacement prepared-meal details. Use an empty add-ons list when it has none.")]
        SavePreparedMealRequest request,
        PreparedMealService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(preparedMealId, RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "remove_prepared_meal", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Removes one prepared meal from active use by archiving it. Existing meal-plan entries " +
                 "are preserved.")]
    public static async Task<object> RemovePreparedMeal(
        [Description("Prepared-meal ID from get_prepared_meals.")] Guid preparedMealId,
        PreparedMealService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        Removed(await service.SetArchivedAsync(
            preparedMealId, RequireHouse(currentUser), true, cancellationToken)
            is PreparedMealOperationStatus.Success);

    [McpServerResource(UriTemplate = "kotlet://prepared-meals/{preparedMealId}", Name = "prepared-meal",
        Title = "Prepared meal", MimeType = "application/json"),
     Description("Complete details for one active household prepared meal.")]
    public static async Task<string> PreparedMeal(
        Guid preparedMealId,
        PreparedMealService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        Json(await service.GetAsync(preparedMealId, RequireHouse(currentUser), cancellationToken)
            ?? throw new ArgumentException("Prepared meal not found.", nameof(preparedMealId)));
}
