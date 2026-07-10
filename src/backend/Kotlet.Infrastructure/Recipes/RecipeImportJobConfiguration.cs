using Kotlet.Domain.Houses;
using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImportJobConfiguration : IEntityTypeConfiguration<RecipeImportJob>
{
    public void Configure(EntityTypeBuilder<RecipeImportJob> builder)
    {
        builder.ToTable("recipe_import_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasDefaultValue(RecipeImportJobStatus.Pending).IsRequired();
        builder.Property(x => x.ErrorReason).HasColumnName("error_reason").HasColumnType("text");
        builder.Property(x => x.DraftJson).HasColumnName("draft_json").HasColumnType("text");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.HouseId).HasDatabaseName("ix_recipe_import_jobs_house_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_recipe_import_jobs_user_id");
        builder.HasOne<House>().WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Cascade);
    }
}
