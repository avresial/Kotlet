namespace Kotlet.Domain.AgentMemory;

public enum AgentMemorySource
{
    ExplicitRequest,
    UserCorrection,
    UserProvidedFact,
    AgentInference
}

public enum AgentMemoryReviewStatus
{
    Confirmed,
    Inferred,
    NeedsReview
}

public sealed class AgentMemory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid HouseId { get; set; }
    public string? Title { get; set; }
    public required string Content { get; set; }
    public string? Category { get; set; }
    public AgentMemorySource Source { get; set; }
    public decimal? Confidence { get; set; }
    public AgentMemoryReviewStatus ReviewStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public long Version { get; set; } = 1;
    public long Revision { get; set; }
    public string? CreatedByClient { get; set; }
    public string? UpdatedByClient { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class AgentMemoryCollection
{
    public Guid UserId { get; set; }
    public Guid HouseId { get; set; }
    public long Revision { get; set; }
}
