namespace Kotlet.Application.Pantry;

public sealed record PantryItemDto(Guid Id, Guid IngredientId, string IngredientName, string MeasurementUnit, decimal Quantity, DateOnly? ExpirationDate);
public sealed record SavePantryItemCommand(Guid IngredientId, decimal Quantity, DateOnly? ExpirationDate = null);

public enum PantryOperationStatus { Success, NotFound, Conflict, ValidationFailed }
public sealed record PantryOperationResult(
    PantryOperationStatus Status,
    PantryItemDto? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
