using Kotlet.Domain.Pantry;

namespace Kotlet.Domain.Products;

public sealed class ProductStock
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    // Non-null key makes a missing date/location participate in uniqueness on both databases.
    public required string BatchKey { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Product Product { get; set; } = null!;
}
