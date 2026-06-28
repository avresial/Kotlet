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
        builder.Property(ingredient => ingredient.CaloriesPer100BaseUnits).HasColumnName("calories_per_100_base_units").HasPrecision(8, 2);
        builder.Property(ingredient => ingredient.PricePer100BaseUnits).HasColumnName("price_per_100_base_units").HasPrecision(10, 2);
        builder.Property(ingredient => ingredient.SvgIcon).HasColumnName("svg_icon");
        builder.HasIndex(ingredient => ingredient.Name).IsUnique().HasDatabaseName("ux_ingredients_name");
    }
}
