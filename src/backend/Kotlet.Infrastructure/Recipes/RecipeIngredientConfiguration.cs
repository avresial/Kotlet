using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.RecipeId).HasColumnName("recipe_id").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(i => i.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
        builder.Property(i => i.Unit).HasColumnName("unit").HasMaxLength(40);
        builder.Property(i => i.Note).HasColumnName("note").HasMaxLength(300);

        builder.HasIndex(i => i.RecipeId).HasDatabaseName("ix_recipe_ingredients_recipe_id");
        builder.HasIndex(i => new { i.RecipeId, i.SortOrder }).IsUnique().HasDatabaseName("ux_recipe_ingredients_recipe_sort");
    }
}
