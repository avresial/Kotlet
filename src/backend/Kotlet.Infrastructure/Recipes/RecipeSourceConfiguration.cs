using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeSourceConfiguration : IEntityTypeConfiguration<RecipeSource>
{
    public void Configure(EntityTypeBuilder<RecipeSource> builder)
    {
        builder.ToTable("recipe_sources");
        builder.HasKey(x => new { x.RecipeId, x.SourceId });
        builder.Property(x => x.RecipeId).HasColumnName("recipe_id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.HasIndex(x => x.SourceId).HasDatabaseName("ix_recipe_sources_source_id");
        builder.HasOne(x => x.Recipe).WithMany(r => r.Sources).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
