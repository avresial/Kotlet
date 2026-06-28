namespace Kotlet.Application.Shopping;

public sealed record ShoppingListItemDto(
    Guid Id, Guid IngredientId, string IngredientName, string MeasurementUnit,
    decimal Quantity, decimal PricePer100BaseUnits, decimal TotalPrice, bool IsPurchased);
public sealed record CreateShoppingListItemCommand(Guid IngredientId, decimal Quantity);
public sealed record UpdateShoppingListItemCommand(decimal Quantity, bool IsPurchased);

public enum ShoppingListOperationStatus { Success, NotFound, Conflict, ValidationFailed }
public sealed record ShoppingListOperationResult(
    ShoppingListOperationStatus Status,
    ShoppingListItemDto? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
