using Kotlet.Domain.Tools;

namespace Kotlet.Application.Tools;

public sealed record StartVideoTranscriptionRequest(string Url);

public sealed record DetectedIngredient(
    string SourceName,
    decimal? Quantity,
    string? Unit,
    string? Note,
    Guid? MatchedIngredientId,
    string? MatchedIngredientName,
    decimal? MatchScore,
    bool IsProposedNew);

public sealed record VideoAnalysisJobResponse(
    Guid Id,
    VideoAnalysisJobStatus Status,
    string? Transcript,
    string? Title,
    string? Author,
    string? Platform,
    string? Language,
    string? SourceUrl,
    IReadOnlyList<DetectedIngredient> DetectedIngredients,
    string? ErrorReason,
    Guid? RecipeImportJobId);

public enum VideoAnalysisOperationStatus
{
    Success,
    NotFound,
    InvalidState,
    ValidationFailed
}

public sealed record VideoAnalysisOperationResult(
    VideoAnalysisOperationStatus Status,
    Guid? Id = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
