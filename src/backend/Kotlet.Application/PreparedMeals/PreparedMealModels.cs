namespace Kotlet.Application.PreparedMeals;

public sealed record PreparedMealAddonRequest(
    Guid IngredientId,
    decimal Quantity,
    string Unit,
    bool IsSelectedByDefault,
    bool IsRequired,
    int SortOrder);

public sealed record SavePreparedMealRequest(
    string Name,
    string? Description,
    string? Brand,
    string? Store,
    string? Category,
    decimal? PackageQuantity,
    string? PackageUnit,
    int Servings,
    decimal? CaloriesPerServing,
    decimal? Price,
    string? PreparationInstructions,
    Guid? ShoppingIngredientId,
    IReadOnlyList<PreparedMealAddonRequest> Addons);

public sealed record PreparedMealAddonResponse(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    bool IsSelectedByDefault,
    bool IsRequired,
    int SortOrder);

public sealed record PreparedMealResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Brand,
    string? Store,
    string? Category,
    decimal? PackageQuantity,
    string? PackageUnit,
    int Servings,
    decimal? CaloriesPerServing,
    decimal? Price,
    string? PreparationInstructions,
    Guid? ShoppingIngredientId,
    bool IsArchived,
    IReadOnlyList<PreparedMealAddonResponse> Addons);

public enum PreparedMealOperationStatus
{
    Success,
    NotFound,
    ValidationFailed
}

public sealed record PreparedMealOperationResult(
    PreparedMealOperationStatus Status,
    PreparedMealResponse? Meal = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
