using Kotlet.Domain.FoodSettings;
using Kotlet.Domain.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.FoodSettings;

internal sealed class UserFoodSettingsConfiguration : IEntityTypeConfiguration<UserFoodSettings>
{
    public void Configure(EntityTypeBuilder<UserFoodSettings> builder)
    {
        builder.ToTable("user_food_settings");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.AvoidedAllergens).HasColumnName("avoided_allergens").HasDefaultValue(Allergen.None);
        builder.Property(x => x.AvoidedAttributes).HasColumnName("avoided_attributes").HasDefaultValue(FoodAttribute.None);
        builder.Property(x => x.RequiredSuitability).HasColumnName("required_suitability").HasDefaultValue(DietarySuitability.None);
        builder.HasOne(x => x.User).WithOne(x => x.FoodSettings).HasForeignKey<UserFoodSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserExcludedIngredientConfiguration : IEntityTypeConfiguration<UserExcludedIngredient>
{
    public void Configure(EntityTypeBuilder<UserExcludedIngredient> builder)
    {
        builder.ToTable("user_excluded_ingredients");
        builder.HasKey(x => new { x.UserId, x.IngredientId });
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.HasOne(x => x.Settings).WithMany(x => x.ExcludedIngredients).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}
