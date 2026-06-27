namespace Kotlet.Domain.Ingredients;

public sealed class Ingredient
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string MeasurementUnit { get; set; }
    public decimal CaloriesPer100Grams { get; set; }
    public decimal Price { get; set; }
}
