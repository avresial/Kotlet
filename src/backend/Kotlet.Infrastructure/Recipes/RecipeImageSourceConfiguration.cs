using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImageSourceConfiguration : IEntityTypeConfiguration<RecipeImageSource>
{
    public void Configure(EntityTypeBuilder<RecipeImageSource> builder)
    {
        builder.ToTable("image_sources");
        builder.HasKey(x => new { x.RecipeImageId, x.SourceId });
        builder.Property(x => x.RecipeImageId).HasColumnName("image_id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.HasIndex(x => x.SourceId).HasDatabaseName("ix_recipe_image_sources_source_id");
        builder.HasOne(x => x.Image).WithMany(i => i.Sources).HasForeignKey(x => x.RecipeImageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
