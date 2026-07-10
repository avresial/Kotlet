namespace Kotlet.Domain.Sources;

/// <summary>
/// Reusable provenance metadata describing where content came from. Entities reference it
/// through explicit foreign-key-backed join entities (e.g. <c>RecipeSource</c>,
/// <c>RecipeImageSource</c>) rather than a generic polymorphic relation.
/// </summary>
public sealed class Source
{
    public Guid Id { get; set; }
    public SourceType Type { get; set; }

    /// <summary>Free-form provider name, e.g. "OpenRouter", "Pexels", "YouTube" or "Website".</summary>
    public required string Provider { get; set; }

    /// <summary>Provider or source page URL.</summary>
    public string? Url { get; set; }

    /// <summary>Provider-specific identifier of the content (e.g. a Pexels photo id).</summary>
    public string? ExternalId { get; set; }

    public string? Title { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorUrl { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
}
