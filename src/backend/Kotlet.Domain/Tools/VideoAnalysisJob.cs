namespace Kotlet.Domain.Tools;

public enum VideoAnalysisJobStatus
{
    Pending,
    Transcribing,
    DetectingIngredients,
    MatchingIngredients,
    Ready,
    Failed
}

public sealed class VideoAnalysisJob
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid UserId { get; set; }
    public required string Url { get; set; }
    public VideoAnalysisJobStatus Status { get; set; }
    public string? ErrorReason { get; set; }
    public string? Transcript { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Platform { get; set; }
    public string? Language { get; set; }
    public string? SourceUrl { get; set; }
    public string? DetectedIngredientsJson { get; set; }
    public string? DraftJson { get; set; }
    public Guid? RecipeImportJobId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
