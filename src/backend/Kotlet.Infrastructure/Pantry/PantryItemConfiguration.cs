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
        builder.HasIndex(x => new { x.HouseId, x.IngredientId }).IsUnique().HasDatabaseName("ux_pantry_items_house_ingredient");
        builder.HasOne(x => x.House).WithMany(house => house.PantryItems).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}
