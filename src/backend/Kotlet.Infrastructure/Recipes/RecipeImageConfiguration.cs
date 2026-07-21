using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    // Ownership and ordering stay recipe-specific; bytes and metadata live in the shared image store.
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("recipe_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.RecipeId).HasColumnName("recipe_id").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.HasOne(i => i.Image).WithOne().HasForeignKey<RecipeImage>(i => i.Id).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.RecipeId).HasDatabaseName("ix_recipe_images_recipe_id");
        builder.HasIndex(i => new { i.RecipeId, i.SortOrder }).IsUnique()
            .HasDatabaseName("ux_recipe_images_recipe_id_sort_order");
    }
}
