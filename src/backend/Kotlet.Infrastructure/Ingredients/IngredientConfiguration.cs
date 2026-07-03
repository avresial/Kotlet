using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Ingredients;

internal sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients");
        builder.HasKey(ingredient => ingredient.Id);
        builder.Property(ingredient => ingredient.Id).HasColumnName("id");
        builder.Property(ingredient => ingredient.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(ingredient => ingredient.MeasurementUnit).HasColumnName("measurement_unit").HasMaxLength(30).IsRequired();
        builder.Property(ingredient => ingredient.IsCountable).HasColumnName("is_countable").IsRequired();
        builder.Property(ingredient => ingredient.MeasurementUnitsPerPiece).HasColumnName("measurement_units_per_piece").HasPrecision(12, 3);
        builder.Property(ingredient => ingredient.CaloriesPer100BaseUnits)
            .HasColumnName("calories_per_100_base_units")
            .HasConversion(calories => calories.Kilocalories, kilocalories => Calories.FromKilocalories(kilocalories))
            .HasPrecision(8, 2);
        builder.Property(ingredient => ingredient.PricePer100BaseUnits)
            .HasColumnName("price_per_100_base_units")
            .HasConversion(price => price.Amount, amount => Price.FromAmount(amount))
            .HasPrecision(10, 2);
        builder.Property(ingredient => ingredient.SvgIcon).HasColumnName("svg_icon");
        builder.Property(ingredient => ingredient.Category).HasColumnName("category").HasDefaultValue(FoodCategory.Unknown).IsRequired();
        builder.Property(ingredient => ingredient.Allergens).HasColumnName("allergens").HasDefaultValue(Allergen.None).IsRequired();
        builder.Property(ingredient => ingredient.Attributes).HasColumnName("attributes").HasDefaultValue(FoodAttribute.None).IsRequired();
        builder.Property(ingredient => ingredient.Suitability).HasColumnName("suitability").HasDefaultValue(DietarySuitability.None).IsRequired();
        builder.Property(ingredient => ingredient.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        // The name is no longer unique: ingredients created in a non-default language store the
        // placeholder "Unknown" as their default-language name, so duplicates are expected. Uniqueness
        // of the user-facing (translated) name is enforced in the application layer instead.
        builder.HasIndex(ingredient => ingredient.Name).HasDatabaseName("ix_ingredients_name");
    }
}
