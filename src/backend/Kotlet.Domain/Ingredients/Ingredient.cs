using Kotlet.Domain.Common;

namespace Kotlet.Domain.Ingredients;

public sealed class Ingredient
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string MeasurementUnit { get; set; }
    public bool IsCountable { get; set; }
    public decimal? MeasurementUnitsPerPiece { get; set; }
    public Calories CaloriesPer100BaseUnits { get; set; }
    public Price PricePer100BaseUnits { get; set; }
    public string? SvgIcon { get; set; }
    public FoodCategory Category { get; set; }
    public Allergen Allergens { get; set; }
    public FoodAttribute Attributes { get; set; }
    public DietarySuitability Suitability { get; set; }
    public bool IsAiModified { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
