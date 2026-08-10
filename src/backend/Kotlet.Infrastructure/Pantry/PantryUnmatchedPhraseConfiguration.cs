using Kotlet.Domain.Pantry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Pantry;

internal sealed class PantryUnmatchedPhraseConfiguration : IEntityTypeConfiguration<PantryUnmatchedPhrase>
{
    public void Configure(EntityTypeBuilder<PantryUnmatchedPhrase> builder)
    {
        builder.ToTable("pantry_unmatched_phrases");
        builder.HasKey(phrase => phrase.Id);
        builder.Property(phrase => phrase.Id).HasColumnName("id");
        builder.Property(phrase => phrase.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(phrase => phrase.RawPhrase).HasColumnName("raw_phrase").HasMaxLength(300).IsRequired();
        builder.Property(phrase => phrase.NormalizedPhrase).HasColumnName("normalized_phrase").HasMaxLength(300).IsRequired();
        builder.Property(phrase => phrase.Locale).HasColumnName("locale").HasMaxLength(20).IsRequired();
        builder.Property(phrase => phrase.CandidateIdsJson).HasColumnName("candidate_ids_json").HasColumnType("text").IsRequired();
        builder.Property(phrase => phrase.RecognitionConfidence).HasColumnName("recognition_confidence").HasPrecision(5, 4);
        builder.Property(phrase => phrase.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").IsRequired();
        builder.Property(phrase => phrase.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(phrase => phrase.OccurrenceCount).HasColumnName("occurrence_count").IsRequired();
        builder.HasIndex(phrase => new { phrase.HouseId, phrase.NormalizedPhrase, phrase.Locale })
            .IsUnique()
            .HasDatabaseName("ux_pantry_unmatched_phrases_house_phrase_locale");
        builder.HasOne(phrase => phrase.House)
            .WithMany()
            .HasForeignKey(phrase => phrase.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
