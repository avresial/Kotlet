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
        builder.Property(x => x.CustomName)
            .HasColumnName("custom_name")
            .HasMaxLength(500);
        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .HasConversion(quantity => quantity.Amount, amount => Quantity.FromAmount(amount))
            .HasPrecision(11, 3);
        builder.Property(item => item.IsPurchased).HasColumnName("is_purchased");
        builder.Property(item => item.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.HasIndex(item => new { item.HouseId, item.IngredientId })
            .IsUnique()
            .HasFilter("ingredient_id IS NOT NULL")
            .HasDatabaseName("ux_shopping_list_items_house_ingredient");
        builder.HasIndex(item => new { item.HouseId, item.PreparedMealId })
            .IsUnique()
            .HasFilter("prepared_meal_id IS NOT NULL")
            .HasDatabaseName("ux_shopping_list_items_house_prepared_meal");
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "ck_shopping_list_items_one_source",
                "(CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END + CASE WHEN custom_name IS NULL THEN 0 ELSE 1 END) = 1"));

        builder.HasOne(item => item.House)
            .WithMany(house => house.ShoppingListItems)
            .HasForeignKey(item => item.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Ingredient)
            .WithMany()
            .HasForeignKey(item => item.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.PreparedMeal)
            .WithMany()
            .HasForeignKey(item => item.PreparedMealId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
