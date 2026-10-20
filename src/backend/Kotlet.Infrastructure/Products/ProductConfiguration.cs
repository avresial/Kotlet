using Kotlet.Domain.Houses;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Products;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(product => product.Id);
        builder.HasAlternateKey(product => new { product.HouseId, product.Id });
        builder.HasIndex(product => new { product.HouseId, product.Barcode }).IsUnique();
        builder.Property(product => product.Id).HasColumnName("id");
        builder.Property(product => product.HouseId).HasColumnName("house_id");
        builder.Property(product => product.Barcode).HasColumnName("barcode").HasMaxLength(14).IsRequired();
        builder.Property(product => product.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(product => product.Brand).HasColumnName("brand").HasMaxLength(200);
        builder.Property(product => product.PackageDescription).HasColumnName("package_description").HasMaxLength(200);
        builder.Property(product => product.Unit).HasColumnName("unit").HasMaxLength(3).IsRequired();
        builder.Property(product => product.PackageQuantity).HasColumnName("package_quantity").HasPrecision(11, 3);
        builder.Property(product => product.IngredientId).HasColumnName("ingredient_id");
        builder.Property(product => product.CaloriesPer100).HasColumnName("calories_per100").HasPrecision(10, 3);
        builder.Property(product => product.ProteinPer100).HasColumnName("protein_per100").HasPrecision(10, 3);
        builder.Property(product => product.CarbsPer100).HasColumnName("carbs_per100").HasPrecision(10, 3);
        builder.Property(product => product.FatPer100).HasColumnName("fat_per100").HasPrecision(10, 3);
        builder.Property(product => product.NutritionBasis).HasColumnName("nutrition_basis").HasMaxLength(2);
        builder.Property(product => product.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
        builder.Property(product => product.SourceRetrievedAtUtc).HasColumnName("source_retrieved_at_utc");
        builder.Property(product => product.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.HasOne<House>().WithMany().HasForeignKey(product => product.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Ingredient>().WithMany().HasForeignKey(product => product.IngredientId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
{
    public void Configure(EntityTypeBuilder<ProductStock> builder)
    {
        builder.ToTable("product_stock");
        builder.HasKey(stock => stock.Id);
        builder.HasIndex(stock => new { stock.ProductId, stock.BatchKey }).IsUnique();
        builder.Property(stock => stock.Id).HasColumnName("id");
        builder.Property(stock => stock.HouseId).HasColumnName("house_id");
        builder.Property(stock => stock.ProductId).HasColumnName("product_id");
        builder.Property(stock => stock.Quantity).HasColumnName("quantity").HasPrecision(11, 3);
        builder.Property(stock => stock.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(stock => stock.StorageLocation).HasColumnName("storage_location");
        builder.Property(stock => stock.BatchKey).HasColumnName("batch_key").HasMaxLength(20).IsRequired();
        builder.Property(stock => stock.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.HasOne(stock => stock.Product).WithMany()
            .HasForeignKey(stock => new { stock.HouseId, stock.ProductId })
            .HasPrincipalKey(product => new { product.HouseId, product.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
