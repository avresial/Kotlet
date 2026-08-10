using Kotlet.Domain.Common;
using Kotlet.Domain.Pantry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Pantry;

internal sealed class PantryItemConfiguration : IEntityTypeConfiguration<PantryItem>
{
    public void Configure(EntityTypeBuilder<PantryItem> builder)
    {
        builder.ToTable("pantry_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseId).HasColumnName("house_id");
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(quantity => quantity.Amount, amount => Quantity.FromAmount(amount))
            .HasPrecision(11, 3);
        builder.Property(x => x.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(x => x.StorageLocation).HasColumnName("storage_location");
        builder.Property(x => x.LastObservedQuantity).HasColumnName("last_observed_quantity").HasPrecision(11, 3);
        builder.Property(x => x.LastObservedUnit).HasColumnName("last_observed_unit").HasMaxLength(40);
        builder.Property(x => x.PackageDescription).HasColumnName("package_description").HasMaxLength(200);
        builder.Property(x => x.ConversionConfidence).HasColumnName("conversion_confidence").HasPrecision(5, 4);
        builder.Property(x => x.LastObservedAtUtc).HasColumnName("last_observed_at_utc");
        builder.Property(x => x.LastObservationIdsJson).HasColumnName("last_observation_ids_json").HasColumnType("text");
        builder.HasIndex(x => new { x.HouseId, x.IngredientId }).IsUnique().HasDatabaseName("ux_pantry_items_house_ingredient");
        builder.HasOne(x => x.House).WithMany(house => house.PantryItems).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}
