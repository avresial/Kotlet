using Kotlet.Domain.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Shopping;

internal sealed class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> builder)
    {
        builder.ToTable("shopping_list_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseId).HasColumnName("house_id");
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(11, 3);
        builder.Property(x => x.IsPurchased).HasColumnName("is_purchased");
        builder.HasIndex(x => new { x.HouseId, x.IngredientId }).IsUnique().HasDatabaseName("ux_shopping_list_items_house_ingredient");
        builder.HasOne(x => x.House).WithMany(x => x.ShoppingListItems).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}
