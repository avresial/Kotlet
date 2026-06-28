namespace Kotlet.Application.Ingredients;

public sealed record IngredientDto(
    Guid Id,
    string Name,
    string MeasurementUnit,
    bool IsCountable,
    decimal? MeasurementUnitsPerPiece,
    decimal CaloriesPer100BaseUnits,
    decimal PricePer100BaseUnits);

public sealed record SaveIngredientCommand(
    string Name,
    string MeasurementUnit,
    bool IsCountable,
    decimal? MeasurementUnitsPerPiece,
    decimal CaloriesPer100BaseUnits,
    decimal PricePer100BaseUnits);

public enum IngredientOperationStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}

public sealed record IngredientOperationResult(
    IngredientOperationStatus Status,
    IngredientDto? Ingredient = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
