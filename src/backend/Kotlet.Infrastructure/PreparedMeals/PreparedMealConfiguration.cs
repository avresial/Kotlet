using Kotlet.Domain.Houses;
using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealConfiguration : IEntityTypeConfiguration<PreparedMeal>
{
    public void Configure(EntityTypeBuilder<PreparedMeal> b)
    {
        b.ToTable("prepared_meals", t => t.HasCheckConstraint("ck_prepared_meals_values", "servings > 0 AND (price IS NULL OR price >= 0) AND (calories_per_serving IS NULL OR calories_per_serving >= 0)"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.HouseId).HasColumnName("house_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasColumnName("description");
        b.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(160); b.Property(x => x.Store).HasColumnName("store").HasMaxLength(160);
        b.Property(x => x.Category).HasColumnName("category").HasMaxLength(160); b.Property(x => x.PackageQuantity).HasColumnName("package_quantity");
        b.Property(x => x.PackageUnit).HasColumnName("package_unit").HasMaxLength(32); b.Property(x => x.Servings).HasColumnName("servings");
        b.Property(x => x.CaloriesPerServing).HasColumnName("calories_per_serving");
        b.Property(x => x.Price).HasColumnName("price");
        b.Property(x => x.PreparationInstructions).HasColumnName("preparation_instructions"); b.Property(x => x.ShoppingIngredientId).HasColumnName("shopping_ingredient_id");
        b.Property(x => x.IsArchived).HasColumnName("is_archived"); b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc"); b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.HasOne<House>().WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.ShoppingIngredient).WithMany().HasForeignKey(x => x.ShoppingIngredientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Addons).WithOne(x => x.PreparedMeal).HasForeignKey(x => x.PreparedMealId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.HouseId, x.Name });
    }
}
