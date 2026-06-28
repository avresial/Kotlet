using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Measurements;

public sealed class MeasurementMappingService
{
    private static readonly (string Unit, decimal Factor)[] KitchenUnits =
    [
        ("cup", 250m),
        ("tbsp", 15m),
        ("tsp", 5m)
    ];

    public NormalizedMeasurement? Normalize(decimal quantity, string unit, Ingredient ingredient)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(unit)) return null;

        var normalizedUnit = unit.Trim().ToLowerInvariant();
        if (normalizedUnit == ingredient.MeasurementUnit)
            return new(quantity, ingredient.MeasurementUnit);

        if (normalizedUnit == "piece")
        {
            return ingredient.IsCountable && ingredient.MeasurementUnitsPerPiece is > 0
                ? new(quantity * ingredient.MeasurementUnitsPerPiece.Value, ingredient.MeasurementUnit)
                : null;
        }

        var mapping = KitchenUnits.FirstOrDefault(x => x.Unit == normalizedUnit);
        return mapping == default
            ? null
            : new(quantity * mapping.Factor, ingredient.MeasurementUnit);
    }

    public DisplayMeasurement ToDisplay(decimal normalizedQuantity, string normalizedUnit, Ingredient ingredient)
    {
        if (ingredient.IsCountable && ingredient.MeasurementUnitsPerPiece is > 0)
        {
            return DividesToWholeNumber(normalizedQuantity, ingredient.MeasurementUnitsPerPiece.Value, out var pieces)
                ? new(pieces, "piece")
                : new(normalizedQuantity, normalizedUnit);
        }

        foreach (var mapping in KitchenUnits)
        {
            if (DividesToWholeNumber(normalizedQuantity, mapping.Factor, out var quantity))
                return new(quantity, mapping.Unit);
        }

        return new(normalizedQuantity, normalizedUnit);
    }

    private static bool DividesToWholeNumber(decimal value, decimal divisor, out decimal result)
    {
        result = value / divisor;
        return result > 0 && result == decimal.Truncate(result);
    }
}

public sealed record NormalizedMeasurement(decimal Quantity, string Unit);
public sealed record DisplayMeasurement(decimal Quantity, string Unit);
