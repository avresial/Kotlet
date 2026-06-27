namespace Kotlet.Application.Pantry;

public sealed record PantryItemDto(Guid Id, Guid IngredientId, string IngredientName, string MeasurementUnit, decimal Quantity);
public sealed record SavePantryItemCommand(Guid IngredientId, decimal Quantity);

public enum PantryOperationStatus { Success, NotFound, Conflict, ValidationFailed }
public sealed record PantryOperationResult(
    PantryOperationStatus Status,
    PantryItemDto? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
