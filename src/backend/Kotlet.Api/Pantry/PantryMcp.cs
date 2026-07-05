using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Api.Mcp;
using Kotlet.Application.Pantry;
using Kotlet.Domain.Pantry;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Pantry;

/// <summary>MCP tools and resources for the household pantry.</summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class PantryMcp
{
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

    [McpServerTool(Name = "add_pantry_item", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Adds an ingredient to the authenticated household's pantry. Fails with a conflict when the ingredient is already stored; use update_pantry_item to change its quantity instead.")]
    public static async Task<McpPantryOperationResult> AddPantryItem(
        [Description("Ingredient ID from get_ingredients.")] Guid ingredientId,
        [Description("Quantity on hand, in the ingredient's base measurement unit.")] decimal quantity,
        PantryService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken,
        [Description("Optional expiration date in yyyy-MM-dd format.")] DateOnly? expirationDate = null,
        [Description("Optional storage location: Refrigerator, Freezer, or Cabinet.")] string? storageLocation = null)
    {
        StorageLocation? location = null;
        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            if (!McpEnum.TryParse(storageLocation, StorageLocation.Cabinet, out var parsed))
                return new(nameof(PantryOperationStatus.ValidationFailed), ValidationErrors: new Dictionary<string, string[]>
                {
                    ["storageLocation"] = [$"Unknown storage location '{storageLocation}'. Use: {string.Join(", ", Enum.GetNames<StorageLocation>())}."]
                });
            location = parsed;
        }
        return McpPantryOperationResult.From(await service.CreateAsync(
            RequireHouse(currentUser),
            new SavePantryItemCommand(ingredientId, quantity, expirationDate, location),
            language.Language, cancellationToken));
    }

    [McpServerTool(Name = "update_pantry_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Sets the stored quantity of one pantry item.")]
    public static async Task<McpPantryOperationResult> UpdatePantryItem(
        [Description("Pantry item ID from get_pantry.")] Guid itemId,
        [Description("New quantity on hand, zero or positive, in the ingredient's base measurement unit.")] decimal quantity,
        PantryService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        McpPantryOperationResult.From(await service.UpdateAsync(
            itemId, RequireHouse(currentUser), quantity, language.Language, cancellationToken));

    [McpServerTool(Name = "remove_pantry_item", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one item from the authenticated household's pantry.")]
    public static async Task<object> RemovePantryItem(
        [Description("Pantry item ID from get_pantry.")] Guid itemId,
        PantryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Removed(await service.DeleteAsync(itemId, RequireHouse(currentUser), cancellationToken)
            is PantryOperationStatus.Success);

    [McpServerResource(UriTemplate = "kotlet://pantry", Name = "pantry",
        Title = "Pantry", MimeType = "application/json"),
     Description("Complete pantry snapshot for the authenticated household: stored ingredients with quantities, expiration dates, and storage locations.")]
    public static async Task<string> Pantry(
        PantryService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        Json((await service.GetAllAsync(RequireHouse(currentUser), language.Language, cancellationToken))
            .Select(McpPantryItem.From));
}
