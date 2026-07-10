namespace Kotlet.Domain.Recipes;

public enum RecipeImportJobStatus
{
    Pending,
    FetchingTranscript,
    Extracting,
    ResolvingIngredients,
    ReadyForReview,
    Failed
}

public sealed class RecipeImportJob
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid UserId { get; set; }
    public required string Url { get; set; }
    public RecipeImportJobStatus Status { get; set; }
    public string? ErrorReason { get; set; }
    public string? DraftJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
