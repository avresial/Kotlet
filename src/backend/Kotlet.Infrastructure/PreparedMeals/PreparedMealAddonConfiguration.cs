using Kotlet.Domain.Common;
using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealAddonConfiguration : IEntityTypeConfiguration<PreparedMealAddon>
{
    public void Configure(EntityTypeBuilder<PreparedMealAddon> builder)
    {
        builder.ToTable("prepared_meal_addons", table => table.HasCheckConstraint(
            "ck_prepared_meal_addons_quantity",
            "default_quantity > 0"));
        builder.HasKey(addon => addon.Id);
        builder.Property(addon => addon.Id).HasColumnName("id");
        builder.Property(addon => addon.PreparedMealId).HasColumnName("prepared_meal_id");
        builder.Property(addon => addon.IngredientId).HasColumnName("ingredient_id");
        builder.Property(addon => addon.DefaultQuantity)
            .HasColumnName("default_quantity")
            .HasConversion(quantity => quantity.Amount, amount => Quantity.FromAmount(amount));
        builder.Property(addon => addon.Unit).HasColumnName("unit").HasMaxLength(32);
        builder.Property(addon => addon.IsSelectedByDefault).HasColumnName("is_selected_by_default");
        builder.Property(addon => addon.IsRequired).HasColumnName("is_required");
        builder.Property(addon => addon.SortOrder).HasColumnName("sort_order");
        builder.HasOne(addon => addon.Ingredient)
            .WithMany()
            .HasForeignKey(addon => addon.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(addon => new { addon.PreparedMealId, addon.IngredientId }).IsUnique();
    }
}
