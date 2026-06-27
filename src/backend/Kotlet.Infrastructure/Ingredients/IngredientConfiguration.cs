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
        builder.Property(ingredient => ingredient.CaloriesPer100Grams).HasColumnName("calories_per_100_grams").HasPrecision(8, 2);
        builder.Property(ingredient => ingredient.Price).HasColumnName("price").HasPrecision(10, 2);
        builder.HasIndex(ingredient => ingredient.Name).IsUnique().HasDatabaseName("ux_ingredients_name");
    }
}
