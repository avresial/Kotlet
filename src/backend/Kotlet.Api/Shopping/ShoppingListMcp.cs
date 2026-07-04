using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Api.Mcp;
using Kotlet.Application.Shopping;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Shopping;

/// <summary>MCP tools and resources for the household shopping list.</summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class ShoppingListMcp
{
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
        [Description("Shopping-list item ID from the kotlet://shopping-list resource.")] Guid itemId,
        [Description("New positive quantity and purchased state.")] UpdateShoppingListItemCommand request,
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(itemId, RequireHouse(currentUser), request, language.Language, cancellationToken);

    [McpServerTool(Name = "remove_shopping_list_item", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one item from the authenticated household's shopping list.")]
    public static async Task<object> RemoveShoppingListItem(
        [Description("Shopping-list item ID from the kotlet://shopping-list resource.")] Guid itemId,
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Removed(await service.DeleteAsync(itemId, RequireHouse(currentUser), cancellationToken)
            is ShoppingListOperationStatus.Success);

    [McpServerTool(Name = "clear_purchased_shopping_items", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes every purchased item from the authenticated household's shopping list.")]
    public static async Task<object> ClearPurchasedShoppingItems(
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.ClearPurchasedAsync(RequireHouse(currentUser), cancellationToken) };

    [McpServerResource(UriTemplate = "kotlet://shopping-list", Name = "shopping-list",
        Title = "Shopping list", MimeType = "application/json"),
     Description("Complete shopping-list snapshot for the authenticated household.")]
    public static async Task<string> ShoppingList(
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        Json(await service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken));
}
