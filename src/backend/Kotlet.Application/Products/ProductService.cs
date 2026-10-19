using Kotlet.Domain.Pantry;
using Kotlet.Domain.Products;

namespace Kotlet.Application.Products;

public sealed class ProductService(IProductRepository repository, IOpenFoodFactsClient openFoodFacts)
{
    public const decimal MaxQuantity = 99999999.999m;

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(Guid houseId, CancellationToken cancellationToken) =>
        (await repository.GetProductsAsync(houseId, cancellationToken)).Select(ProductDto.From).ToArray();

    public async Task<ProductLookupResult?> LookupAsync(Guid houseId, string barcode, CancellationToken cancellationToken)
    {
        if (!Barcode.TryNormalize(barcode, out var normalized))
        {
            return null;
        }
        var local = await repository.FindProductAsync(houseId, normalized, cancellationToken);
        return local is not null ? new("local", ProductDto.From(local)) : await openFoodFacts.LookupAsync(normalized, cancellationToken);
    }

    public async Task<ProductResult<ProductDto>> SaveAsync(Guid houseId, string barcode, SaveProductCommand command, CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (!Barcode.TryNormalize(barcode, out var normalized))
        {
            errors["barcode"] = ["Enter an EAN-8, UPC-A, EAN-13 or GTIN-14 with a valid check digit."];
        }
        if (command.IngredientId is { } ingredientId && !await repository.IngredientExistsAsync(ingredientId, cancellationToken))
        {
            errors["ingredientId"] = ["The ingredient does not exist."];
        }
        if (errors.Count != 0)
        {
            return new("validation_failed", Errors: errors);
        }
        var product = await repository.FindProductAsync(houseId, normalized, cancellationToken);
        if (product is not null && (product.Unit != command.Unit || product.PackageQuantity != command.PackageQuantity)
            && await repository.HasStockAsync(product.Id, cancellationToken))
        {
            return new("conflict", Message: "The unit and package size cannot change while stock exists.");
        }
        var isNew = product is null;
        product ??= new Product { Id = Guid.NewGuid(), HouseId = houseId, Barcode = normalized, Name = command.Name.Trim(), Unit = command.Unit };
        product.Name = command.Name.Trim();
        product.Brand = NullIfEmpty(command.Brand);
        product.PackageDescription = NullIfEmpty(command.PackageDescription);
        product.PackageQuantity = command.PackageQuantity;
        product.Unit = command.Unit;
        product.IngredientId = command.IngredientId;
        product.CaloriesPer100 = command.CaloriesPer100 is { } caloriesPer100 ? decimal.Round(caloriesPer100, 3) : null;
        product.ProteinPer100 = command.ProteinPer100 is { } proteinPer100 ? decimal.Round(proteinPer100, 3) : null;
        product.CarbsPer100 = command.CarbsPer100 is { } carbsPer100 ? decimal.Round(carbsPer100, 3) : null;
        product.FatPer100 = command.FatPer100 is { } fatPer100 ? decimal.Round(fatPer100, 3) : null;
        product.NutritionBasis = command.NutritionBasis;
        // Corrections must never erase the attribution of imported data.
        if (command.Source == "OpenFoodFacts")
        {
            product.Source = "OpenFoodFacts";
            product.SourceRetrievedAtUtc ??= DateTimeOffset.UtcNow;
        }
        product.Revision = Guid.NewGuid();
        if (isNew)
        {
            repository.Add(product);
        }
        try
        {
            await repository.SaveAsync(cancellationToken);
        }
        catch (ProductConflictException)
        {
            return new("conflict", Message: "The product was saved concurrently. Look up the code again and retry.");
        }
        return new("success", ProductDto.From(product));
    }

    public async Task<IReadOnlyList<ProductStockDto>> GetStockAsync(Guid houseId, CancellationToken cancellationToken) =>
        (await repository.GetStockAsync(houseId, cancellationToken)).Select(ProductStockDto.From).ToArray();

    public Task<ProductResult<ProductStockDto>> AddStockAsync(Guid houseId, SaveProductStockCommand command, CancellationToken cancellationToken) =>
        ValidStock(command.Quantity, command.StorageLocation, false) is { } errors
            ? Task.FromResult(new ProductResult<ProductStockDto>("validation_failed", Errors: errors))
            : repository.AddStockAsync(houseId, command, cancellationToken);

    public Task<ProductResult<ProductStockDto>> UpdateStockAsync(Guid houseId, Guid id, UpdateProductStockCommand command, CancellationToken cancellationToken) =>
        ValidStock(command.Quantity, command.StorageLocation, true) is { } errors
            ? Task.FromResult(new ProductResult<ProductStockDto>("validation_failed", Errors: errors))
            : repository.UpdateStockAsync(houseId, id, command, cancellationToken);

    public Task<bool> DeleteStockAsync(Guid houseId, Guid id, CancellationToken cancellationToken) => repository.DeleteStockAsync(houseId, id, cancellationToken);

    private static Dictionary<string, string[]> Validate(SaveProductCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name is required and must be at most 200 characters."];
        }
        if (command.Brand?.Length > 200 || command.PackageDescription?.Length > 200)
        {
            errors["brand"] = ["Brand and package description must be at most 200 characters."];
        }
        if (command.PackageQuantity <= 0 || command.PackageQuantity > MaxQuantity || decimal.Round(command.PackageQuantity, 3) != command.PackageQuantity)
        {
            errors["packageQuantity"] = ["Package quantity must be positive, at most 99999999.999, with up to three decimal places."];
        }
        if (command.Unit is not ("g" or "ml" or "pcs"))
        {
            errors["unit"] = ["Choose g, ml or pcs."];
        }
        if (command.NutritionBasis is not (null or "g" or "ml"))
        {
            errors["nutritionBasis"] = ["Nutritional values must be per 100 g or per 100 ml."];
        }
        if ((command.CaloriesPer100.HasValue || command.ProteinPer100.HasValue || command.CarbsPer100.HasValue || command.FatPer100.HasValue) && command.NutritionBasis is null)
        {
            errors["nutritionBasis"] = ["Select the basis of nutritional values."];
        }
        if (command.CaloriesPer100 is < 0 or > 1000 || command.ProteinPer100 is < 0 or > 100 || command.CarbsPer100 is < 0 or > 100 || command.FatPer100 is < 0 or > 100)
        {
            errors["nutrition"] = ["Calories must be between 0 and 1000; macronutrients between 0 and 100."];
        }
        if (command.Source is not ("Manual" or "OpenFoodFacts"))
        {
            errors["source"] = ["Invalid product source."];
        }
        return errors;
    }

    private static Dictionary<string, string[]>? ValidStock(decimal quantity, StorageLocation? location, bool allowZero)
    {
        var errors = new Dictionary<string, string[]>();
        if (quantity < 0 || (!allowZero && quantity == 0) || quantity > MaxQuantity || decimal.Round(quantity, 3) != quantity)
        {
            errors["quantity"] = ["Quantity is out of range or has more than three decimal places."];
        }
        if (location.HasValue && !Enum.IsDefined(location.Value))
        {
            errors["storageLocation"] = ["Invalid storage location."];
        }
        return errors.Count == 0 ? null : errors;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
