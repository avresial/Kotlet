using Kotlet.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Sources;

internal sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("sources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000);
        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(200);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
        builder.Property(x => x.AuthorName).HasColumnName("author_name").HasMaxLength(200);
        builder.Property(x => x.AuthorUrl).HasColumnName("author_url").HasMaxLength(2000);
        builder.Property(x => x.RetrievedAtUtc).HasColumnName("retrieved_at_utc").IsRequired();
    }
}
