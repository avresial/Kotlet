using Kotlet.Domain.Common;
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
        builder.Property(i => i.IngredientId).HasColumnName("ingredient_id").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(i => i.NormalizedQuantity)
            .HasColumnName("normalized_quantity")
            .HasConversion(quantity => quantity.Amount, amount => Quantity.FromAmount(amount))
            .HasPrecision(12, 3)
            .IsRequired();
        builder.Property(i => i.NormalizedUnit).HasColumnName("normalized_unit").HasMaxLength(2).IsRequired();
        builder.Property(i => i.Note).HasColumnName("note").HasMaxLength(300);

        builder.HasOne(i => i.Ingredient).WithMany().HasForeignKey(i => i.IngredientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.RecipeId).HasDatabaseName("ix_recipe_ingredients_recipe_id");
        builder.HasIndex(i => i.IngredientId).HasDatabaseName("ix_recipe_ingredients_ingredient_id");
        builder.HasIndex(i => new { i.RecipeId, i.SortOrder }).IsUnique().HasDatabaseName("ux_recipe_ingredients_recipe_sort");
    }
}
