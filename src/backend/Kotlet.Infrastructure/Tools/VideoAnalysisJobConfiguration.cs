using Kotlet.Domain.Houses;
using Kotlet.Domain.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Tools;

internal sealed class VideoAnalysisJobConfiguration : IEntityTypeConfiguration<VideoAnalysisJob>
{
    public void Configure(EntityTypeBuilder<VideoAnalysisJob> builder)
    {
        builder.ToTable("video_analysis_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id");
        builder.Property(job => job.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(job => job.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(job => job.Url).HasColumnName("url").HasMaxLength(2000).IsRequired();
        builder.Property(job => job.Status).HasColumnName("status")
            .HasDefaultValue(VideoAnalysisJobStatus.Pending).IsRequired();
        builder.Property(job => job.ErrorReason).HasColumnName("error_reason").HasColumnType("text");
        builder.Property(job => job.Transcript).HasColumnName("transcript").HasColumnType("text");
        builder.Property(job => job.Title).HasColumnName("title").HasMaxLength(500);
        builder.Property(job => job.Author).HasColumnName("author").HasMaxLength(500);
        builder.Property(job => job.Platform).HasColumnName("platform").HasMaxLength(50);
        builder.Property(job => job.Language).HasColumnName("language").HasMaxLength(50);
        builder.Property(job => job.SourceUrl).HasColumnName("source_url").HasMaxLength(2000);
        builder.Property(job => job.DetectedIngredientsJson).HasColumnName("detected_ingredients_json").HasColumnType("text");
        builder.Property(job => job.DraftJson).HasColumnName("draft_json").HasColumnType("text");
        builder.Property(job => job.RecipeImportJobId).HasColumnName("recipe_import_job_id");
        builder.Property(job => job.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(job => job.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(job => job.HouseId).HasDatabaseName("ix_video_analysis_jobs_house_id");
        builder.HasIndex(job => job.UserId).HasDatabaseName("ix_video_analysis_jobs_user_id");
        builder.HasOne<House>().WithMany().HasForeignKey(job => job.HouseId).OnDelete(DeleteBehavior.Cascade);
    }
}
