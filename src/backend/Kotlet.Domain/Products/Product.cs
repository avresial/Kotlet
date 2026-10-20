namespace Kotlet.Domain.Products;

// A packaged, branded product is not a recipe ingredient. The optional link is descriptive.
public sealed class Product
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? PackageDescription { get; set; }
    public decimal PackageQuantity { get; set; }
    public required string Unit { get; set; }
    public Guid? IngredientId { get; set; }
    public decimal? CaloriesPer100 { get; set; }
    public decimal? ProteinPer100 { get; set; }
    public decimal? CarbsPer100 { get; set; }
    public decimal? FatPer100 { get; set; }
    public string? NutritionBasis { get; set; }
    public string Source { get; set; } = "Manual";
    public DateTimeOffset? SourceRetrievedAtUtc { get; set; }
    public int? FoodDataCentralId { get; set; }
    public DateTimeOffset? FoodDataCentralRetrievedAtUtc { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}
