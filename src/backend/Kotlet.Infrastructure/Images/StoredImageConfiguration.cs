using Kotlet.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Images;

internal sealed class StoredImageConfiguration : IEntityTypeConfiguration<StoredImage>
{
    public void Configure(EntityTypeBuilder<StoredImage> b)
    {
        b.ToTable("images");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        b.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        b.Property(x => x.Content).HasColumnName("content").HasColumnType("bytea").IsRequired();
        b.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(300);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
