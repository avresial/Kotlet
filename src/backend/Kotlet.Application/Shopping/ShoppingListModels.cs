using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Shopping;

public sealed record ShoppingListItemDto(
    Guid Id, Guid? IngredientId, Guid? PreparedMealId, string IngredientName, string MeasurementUnit,
    decimal Quantity, decimal PricePer100BaseUnits, decimal TotalPrice, bool IsPurchased, FoodCategory Category, string? Note,
    string? CustomName = null);
public sealed record CreateShoppingListItemCommand(Guid? IngredientId, Guid? PreparedMealId, decimal Quantity, string? Note = null,
    string? CustomName = null, string? RequestedName = null);
public sealed record UpdateShoppingListItemCommand(decimal Quantity, bool IsPurchased, string? Note = null);
public sealed record GenerateShoppingListCommand(DateOnly From, DateOnly To);

public enum ShoppingListOperationStatus { Success, NotFound, Conflict, ValidationFailed }
public enum ShoppingListConflictReason { SameIngredient, SamePreparedMeal }

public sealed record ShoppingListConflict(
    string RequestedName,
    ShoppingListItemDto ExistingItem,
    string MatchedName,
    FoodCategory Category,
    ShoppingListConflictReason Reason);

public sealed record ShoppingListOperationResult(
    ShoppingListOperationStatus Status,
    ShoppingListItemDto? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null,
    IReadOnlyCollection<ShoppingListItemDto>? Items = null,
    ShoppingListConflict? Conflict = null);
