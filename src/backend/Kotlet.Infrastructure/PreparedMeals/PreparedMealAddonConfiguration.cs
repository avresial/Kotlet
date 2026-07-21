using Kotlet.Domain.Common;
using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealAddonConfiguration : IEntityTypeConfiguration<PreparedMealAddon>
{
    public void Configure(EntityTypeBuilder<PreparedMealAddon> b)
    {
        b.ToTable("prepared_meal_addons", t => t.HasCheckConstraint("ck_prepared_meal_addons_quantity", "default_quantity > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.PreparedMealId).HasColumnName("prepared_meal_id");
        b.Property(x => x.IngredientId).HasColumnName("ingredient_id"); b.Property(x => x.DefaultQuantity).HasColumnName("default_quantity").HasConversion(x => x.Amount, x => Quantity.FromAmount(x));
        b.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(32); b.Property(x => x.IsSelectedByDefault).HasColumnName("is_selected_by_default");
        b.Property(x => x.IsRequired).HasColumnName("is_required"); b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PreparedMealId, x.IngredientId }).IsUnique();
    }
}
