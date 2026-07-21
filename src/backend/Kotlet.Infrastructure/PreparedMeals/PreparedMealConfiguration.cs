using Kotlet.Domain.Houses;
using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealConfiguration : IEntityTypeConfiguration<PreparedMeal>
{
    public void Configure(EntityTypeBuilder<PreparedMeal> builder)
    {
        builder.ToTable("prepared_meals", table => table.HasCheckConstraint(
            "ck_prepared_meals_values",
            "servings > 0 AND (price IS NULL OR price >= 0) AND calories_per_serving >= 0"));
        builder.HasKey(meal => meal.Id);
        builder.Property(meal => meal.Id).HasColumnName("id");
        builder.Property(meal => meal.HouseId).HasColumnName("house_id");
        builder.Property(meal => meal.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(meal => meal.Description).HasColumnName("description");
        builder.Property(meal => meal.Brand).HasColumnName("brand").HasMaxLength(160);
        builder.Property(meal => meal.Store).HasColumnName("store").HasMaxLength(160);
        builder.Property(meal => meal.Category).HasColumnName("category").HasMaxLength(160);
        builder.Property(meal => meal.PackageQuantity).HasColumnName("package_quantity");
        builder.Property(meal => meal.PackageUnit).HasColumnName("package_unit").HasMaxLength(32);
        builder.Property(meal => meal.Servings).HasColumnName("servings");
        builder.Property(meal => meal.CaloriesPerServing).HasColumnName("calories_per_serving");
        builder.Property(meal => meal.Price).HasColumnName("price");
        builder.Property(meal => meal.PreparationInstructions).HasColumnName("preparation_instructions");
        builder.Property(meal => meal.ShoppingIngredientId).HasColumnName("shopping_ingredient_id");
        builder.Property(meal => meal.IsArchived).HasColumnName("is_archived");
        builder.Property(meal => meal.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(meal => meal.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasOne<House>()
            .WithMany()
            .HasForeignKey(meal => meal.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(meal => meal.ShoppingIngredient)
            .WithMany()
            .HasForeignKey(meal => meal.ShoppingIngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(meal => meal.Addons)
            .WithOne(addon => addon.PreparedMeal)
            .HasForeignKey(addon => addon.PreparedMealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(meal => new { meal.HouseId, meal.Name });
    }
}
