using Kotlet.Domain.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Translations;

internal sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("translations");
        builder.HasKey(translation => translation.Key);
        builder.Property(translation => translation.Key).HasColumnName("key").HasMaxLength(200);
        builder.Property(translation => translation.Value).HasColumnName("value").IsRequired();
    }
}
