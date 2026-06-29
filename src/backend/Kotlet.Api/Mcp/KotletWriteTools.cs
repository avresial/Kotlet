using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Shopping;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

[McpServerToolType]
public sealed class KotletWriteTools
{
    [McpServerTool(Name = "add_weekly_meal_plan", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds up to 21 meals within one seven-day period. Existing identical meals are skipped; existing meals are never replaced.")]
    public static Task<WeeklyMealPlannerOperationResult> AddWeeklyMealPlan(
        [Description("The week start and meals to add. Every meal date must fall within the seven-day period.")]
        AddWeeklyMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddWeekAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "get_shopping_list", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists the authenticated household's shopping-list items and their IDs.")]
    public static Task<IReadOnlyCollection<ShoppingListItemDto>> GetShoppingList(
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken);

    [McpServerTool(Name = "add_shopping_list_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds an ingredient to the authenticated household's shopping list. Repeating an existing ingredient does not create a duplicate.")]
    public static Task<ShoppingListOperationResult> AddShoppingListItem(
        [Description("Ingredient ID and positive quantity in the ingredient's measurement unit.")]
        CreateShoppingListItemCommand request,
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.CreateAsync(RequireHouse(currentUser), request, language.Language, cancellationToken);

    [McpServerTool(Name = "update_shopping_list_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Changes the quantity or purchased state of one shopping-list item.")]
    public static Task<ShoppingListOperationResult> UpdateShoppingListItem(
        [Description("Shopping-list item ID returned by get_shopping_list.")] Guid itemId,
        [Description("New positive quantity and purchased state.")] UpdateShoppingListItemCommand request,
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(itemId, RequireHouse(currentUser), request, language.Language, cancellationToken);

    [McpServerTool(Name = "remove_shopping_list_item", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one item from the authenticated household's shopping list.")]
    public static async Task<object> RemoveShoppingListItem(
        [Description("Shopping-list item ID returned by get_shopping_list.")] Guid itemId,
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.DeleteAsync(itemId, RequireHouse(currentUser), cancellationToken)
            is ShoppingListOperationStatus.Success };

    [McpServerTool(Name = "clear_purchased_shopping_items", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes every purchased item from the authenticated household's shopping list.")]
    public static async Task<object> ClearPurchasedShoppingItems(
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.ClearPurchasedAsync(RequireHouse(currentUser), cancellationToken) };

    private static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    private static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");
}
