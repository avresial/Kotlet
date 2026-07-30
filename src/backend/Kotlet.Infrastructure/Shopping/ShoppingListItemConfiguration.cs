using Kotlet.Domain.Common;
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
        builder.Property(x => x.PreparedMealId).HasColumnName("prepared_meal_id");
        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(quantity => quantity.Amount, amount => Quantity.FromAmount(amount))
            .HasPrecision(11, 3);
        builder.Property(x => x.IsPurchased).HasColumnName("is_purchased");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        builder.HasIndex(x => new { x.HouseId, x.IngredientId }).IsUnique()
            .HasFilter("ingredient_id IS NOT NULL").HasDatabaseName("ux_shopping_list_items_house_ingredient");
        builder.HasIndex(x => new { x.HouseId, x.PreparedMealId }).IsUnique()
            .HasFilter("prepared_meal_id IS NOT NULL").HasDatabaseName("ux_shopping_list_items_house_prepared_meal");
        builder.ToTable(table => table.HasCheckConstraint("ck_shopping_list_items_one_source",
            "(CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1"));
        builder.HasOne(x => x.House).WithMany(x => x.ShoppingListItems).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PreparedMeal).WithMany().HasForeignKey(x => x.PreparedMealId).OnDelete(DeleteBehavior.Cascade);
    }
}
