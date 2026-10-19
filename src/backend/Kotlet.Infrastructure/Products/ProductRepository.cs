using System.Globalization;
using Kotlet.Application.Products;
using Kotlet.Domain.Pantry;
using Kotlet.Domain.Products;
using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kotlet.Infrastructure.Products;

internal sealed class ProductRepository(KotletDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetProductsAsync(Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking().Where(product => product.HouseId == houseId).OrderBy(product => product.Name).ToListAsync(cancellationToken);

    public Task<Product?> FindProductAsync(Guid houseId, string barcode, CancellationToken cancellationToken) =>
        dbContext.Products.SingleOrDefaultAsync(product => product.HouseId == houseId && product.Barcode == barcode, cancellationToken);

    public Task<Product?> FindProductAsync(Guid houseId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Products.SingleOrDefaultAsync(product => product.HouseId == houseId && product.Id == id, cancellationToken);

    public Task<bool> IngredientExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Ingredients.AnyAsync(ingredient => ingredient.Id == id, cancellationToken);

    public Task<bool> HasStockAsync(Guid productId, CancellationToken cancellationToken) =>
        dbContext.ProductStock.AnyAsync(stock => stock.ProductId == productId, cancellationToken);

    public void Add(Product product) => dbContext.Products.Add(product);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsConflict(exception))
        {
            throw new ProductConflictException(exception);
        }
    }

    public async Task<IReadOnlyList<ProductStock>> GetStockAsync(Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.ProductStock.AsNoTracking().Include(stock => stock.Product)
            .Where(stock => stock.HouseId == houseId).OrderBy(stock => stock.ExpirationDate == null)
            .ThenBy(stock => stock.ExpirationDate).ThenBy(stock => stock.Product.Name).ToListAsync(cancellationToken);

    public Task<ProductStock?> FindStockAsync(Guid houseId, Guid id, CancellationToken cancellationToken) =>
        dbContext.ProductStock.Include(stock => stock.Product).SingleOrDefaultAsync(stock => stock.HouseId == houseId && stock.Id == id, cancellationToken);

    public async Task<ProductResult<ProductStockDto>> AddStockAsync(Guid houseId, SaveProductStockCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var product = await FindProductAsync(houseId, command.ProductId, cancellationToken);
            if (product is null)
            {
                return new("not_found");
            }
            var batchKey = BatchKey(command.ExpirationDate, command.StorageLocation);
            var stock = await dbContext.ProductStock.SingleOrDefaultAsync(item => item.ProductId == product.Id && item.HouseId == houseId && item.BatchKey == batchKey, cancellationToken);
            if (stock is null)
            {
                stock = new ProductStock
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    ProductId = product.Id,
                    Product = product,
                    BatchKey = batchKey,
                    ExpirationDate = command.ExpirationDate,
                    StorageLocation = command.StorageLocation
                };
                dbContext.ProductStock.Add(stock);
            }
            if (stock.Quantity > ProductService.MaxQuantity - command.Quantity)
            {
                return new("validation_failed", Errors: new Dictionary<string, string[]> { ["quantity"] = ["The resulting quantity is too large."] });
            }
            stock.Quantity += command.Quantity;
            stock.Revision = Guid.NewGuid();
            // Also serialize stock changes against edits to package size/unit on the catalog entry.
            product.Revision = Guid.NewGuid();
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new("success", ProductStockDto.From(stock));
            }
            catch (DbUpdateException exception) when (IsConflict(exception))
            {
                // The failed SaveChanges transaction was rolled back; re-read before adding again.
                dbContext.ChangeTracker.Clear();
            }
        }
        return new("conflict", Message: "Stock was changed concurrently. Reload and retry.");
    }

    public async Task<ProductResult<ProductStockDto>> UpdateStockAsync(Guid houseId, Guid id, UpdateProductStockCommand command, CancellationToken cancellationToken)
    {
        var stock = await FindStockAsync(houseId, id, cancellationToken);
        if (stock is null)
        {
            return new("not_found");
        }
        var batchKey = BatchKey(command.ExpirationDate, command.StorageLocation);
        if (await dbContext.ProductStock.AnyAsync(item => item.HouseId == houseId && item.ProductId == stock.ProductId && item.BatchKey == batchKey && item.Id != id, cancellationToken))
        {
            return new("conflict", Message: "A batch with that expiry and location already exists. Adjust that batch instead.");
        }
        stock.Quantity = command.Quantity;
        stock.ExpirationDate = command.ExpirationDate;
        stock.StorageLocation = command.StorageLocation;
        stock.BatchKey = batchKey;
        stock.Revision = Guid.NewGuid();
        stock.Product.Revision = Guid.NewGuid();
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (ProductConflictException)
        {
            return new("conflict", Message: "Stock was changed concurrently. Reload and retry.");
        }
        return new("success", ProductStockDto.From(stock));
    }

    public async Task<bool> DeleteStockAsync(Guid houseId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.ProductStock.Where(stock => stock.HouseId == houseId && stock.Id == id).ExecuteDeleteAsync(cancellationToken) != 0;

    private static string BatchKey(DateOnly? expiry, StorageLocation? location) =>
        $"{expiry?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "none"}:{(int?)location ?? 0}";

    private static bool IsConflict(DbUpdateException exception) => exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 or 1555 };
}
