using Kotlet.Domain.Houses;

namespace Kotlet.Domain.Pantry;

/// <summary>A structured household phrase that the catalogue could not resolve.</summary>
public sealed class PantryUnmatchedPhrase
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public required string RawPhrase { get; set; }
    public required string NormalizedPhrase { get; set; }
    public required string Locale { get; set; }
    public string CandidateIdsJson { get; set; } = "[]";
    public decimal? RecognitionConfidence { get; set; }
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public int OccurrenceCount { get; set; }
    public House House { get; set; } = null!;
}
