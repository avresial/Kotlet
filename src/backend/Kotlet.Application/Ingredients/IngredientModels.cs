using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public sealed record IngredientDto(
    Guid Id,
    string Name,
    string DefaultName,
    string? Translation,
    string MeasurementUnit,
    bool IsCountable,
    decimal? MeasurementUnitsPerPiece,
    decimal CaloriesPer100BaseUnits,
    decimal PricePer100BaseUnits,
    string? SvgIcon,
    FoodCategory Category,
    Allergen Allergens,
    FoodAttribute Attributes,
    DietarySuitability Suitability,
    bool IsAiModified,
    DateTimeOffset CreatedAtUtc);

public sealed record SaveIngredientCommand(
    string Name,
    string MeasurementUnit,
    bool IsCountable,
    decimal? MeasurementUnitsPerPiece,
    decimal CaloriesPer100BaseUnits,
    decimal PricePer100BaseUnits,
    string? Translation = null,
    FoodCategory Category = FoodCategory.Unknown,
    Allergen Allergens = Allergen.None,
    FoodAttribute Attributes = FoodAttribute.None,
    DietarySuitability Suitability = DietarySuitability.None,
    bool IsAiModified = false);

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
