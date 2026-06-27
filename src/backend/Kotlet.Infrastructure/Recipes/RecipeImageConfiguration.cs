using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    // V1 keeps raw image bytes in PostgreSQL. The content endpoint is the abstraction point
    // for a later migration to object storage without changing recipe DTOs.
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("recipe_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.RecipeId).HasColumnName("recipe_id").IsRequired();
        builder.Property(i => i.FileName).HasColumnName("file_name").HasMaxLength(260).IsRequired();
        builder.Property(i => i.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(i => i.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(i => i.Content).HasColumnName("content").HasColumnType("bytea").IsRequired();
        builder.Property(i => i.AltText).HasColumnName("alt_text").HasMaxLength(300);
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(i => i.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(i => i.RecipeId).HasDatabaseName("ix_recipe_images_recipe_id");
        builder.HasIndex(i => new { i.RecipeId, i.SortOrder }).IsUnique()
            .HasDatabaseName("ux_recipe_images_recipe_id_sort_order");
    }
}
