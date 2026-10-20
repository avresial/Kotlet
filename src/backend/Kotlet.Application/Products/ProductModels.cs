using Kotlet.Domain.Pantry;
using Kotlet.Domain.Products;
using BarcodeValue = Kotlet.Application.Products.Barcode;

namespace Kotlet.Application.Products;

public sealed record ProductDto(
    Guid Id, string Barcode, string Name, string? Brand, string? PackageDescription,
    decimal PackageQuantity, string Unit, Guid? IngredientId,
    decimal? CaloriesPer100, decimal? ProteinPer100, decimal? CarbsPer100, decimal? FatPer100,
    string? NutritionBasis, string Source, string? SourceUrl, DateTimeOffset? SourceRetrievedAtUtc,
    int? FoodDataCentralId = null, DateTimeOffset? FoodDataCentralRetrievedAtUtc = null)
{
    public static ProductDto From(Product product) => new(
        product.Id, product.Barcode, product.Name, product.Brand, product.PackageDescription,
        product.PackageQuantity, product.Unit, product.IngredientId, product.CaloriesPer100,
        product.ProteinPer100, product.CarbsPer100, product.FatPer100, product.NutritionBasis,
        product.Source, product.Source == "OpenFoodFacts"
            ? $"https://world.openfoodfacts.org/product/{BarcodeValue.ToOpenFoodFactsCode(product.Barcode)}" : product.Source == "FoodDataCentral" && product.FoodDataCentralId is { } foodId
                ? $"https://fdc.nal.usda.gov/food-details/{foodId}/nutrients" : null,
        product.SourceRetrievedAtUtc, product.FoodDataCentralId, product.FoodDataCentralRetrievedAtUtc);
}

public sealed record SaveProductCommand(
    string Name, string? Brand = null, string? PackageDescription = null,
    decimal PackageQuantity = 1, string Unit = "pcs", Guid? IngredientId = null,
    decimal? CaloriesPer100 = null, decimal? ProteinPer100 = null, decimal? CarbsPer100 = null,
    decimal? FatPer100 = null, string? NutritionBasis = null, string Source = "Manual", int? FoodDataCentralId = null);
public sealed record ProductLookupResult(string Status, ProductDto? Product = null);
public sealed record SaveProductStockCommand(Guid ProductId, decimal Quantity, DateOnly? ExpirationDate = null, StorageLocation? StorageLocation = null);
public sealed record UpdateProductStockCommand(decimal Quantity, DateOnly? ExpirationDate = null, StorageLocation? StorageLocation = null);
public sealed record ProductStockDto(Guid Id, ProductDto Product, decimal Quantity, DateOnly? ExpirationDate, StorageLocation? StorageLocation)
{
    public static ProductStockDto From(ProductStock stock) => new(stock.Id, ProductDto.From(stock.Product), stock.Quantity, stock.ExpirationDate, stock.StorageLocation);
}
public sealed record ProductResult<T>(string Status, T? Value = default, string? Message = null, IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IOpenFoodFactsClient
{
    Task<ProductLookupResult> LookupAsync(string normalizedBarcode, CancellationToken cancellationToken);
}

public interface IFoodDataCentralClient
{
    Task<ProductDto?> LookupAsync(string normalizedBarcode, CancellationToken cancellationToken);
}

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetProductsAsync(Guid houseId, CancellationToken cancellationToken);
    Task<Product?> FindProductAsync(Guid houseId, string barcode, CancellationToken cancellationToken);
    Task<Product?> FindProductAsync(Guid houseId, Guid id, CancellationToken cancellationToken);
    Task<bool> IngredientExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasStockAsync(Guid productId, CancellationToken cancellationToken);
    void Add(Product product);
    Task SaveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductStock>> GetStockAsync(Guid houseId, CancellationToken cancellationToken);
    Task<ProductStock?> FindStockAsync(Guid houseId, Guid id, CancellationToken cancellationToken);
    Task<ProductResult<ProductStockDto>> AddStockAsync(Guid houseId, SaveProductStockCommand command, CancellationToken cancellationToken);
    Task<ProductResult<ProductStockDto>> UpdateStockAsync(Guid houseId, Guid id, UpdateProductStockCommand command, CancellationToken cancellationToken);
    Task<bool> DeleteStockAsync(Guid houseId, Guid id, CancellationToken cancellationToken);
}

public sealed class ProductConflictException(Exception innerException) : Exception("The product was changed concurrently. Reload and retry.", innerException);
