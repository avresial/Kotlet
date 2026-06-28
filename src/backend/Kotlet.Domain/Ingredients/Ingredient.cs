namespace Kotlet.Domain.Ingredients;

public sealed class Ingredient
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string MeasurementUnit { get; set; }
    public bool IsCountable { get; set; }
    public decimal? MeasurementUnitsPerPiece { get; set; }
    public decimal CaloriesPer100BaseUnits { get; set; }
    public decimal PricePer100BaseUnits { get; set; }
    public string? SvgIcon { get; set; }
}
