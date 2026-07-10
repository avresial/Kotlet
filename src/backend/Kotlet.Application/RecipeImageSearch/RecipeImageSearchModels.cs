namespace Kotlet.Application.RecipeImageSearch;

public sealed record RecipeImageSearchRequest(
    string Query,
    int Limit = 10,
    string? Orientation = "landscape",
    string? Locale = null);

public sealed record RecipeImageCandidate(
    string Provider,
    string ExternalImageId,
    string PreviewUrl,
    string SourcePageUrl,
    string? AuthorName,
    string? AuthorUrl,
    string? AltText,
    int? Width,
    int? Height);

public sealed record RecipeImageContent(
    byte[] Content,
    string ContentType,
    string ExternalImageId,
    string SourcePageUrl,
    string? AuthorName,
    string? AuthorUrl,
    string? AltText);

public enum RecipeImageSearchStatus
{
    Success,
    InvalidQuery,
    NotConfigured,
    RateLimited,
    Failed
}

public enum RecipeImageDownloadStatus
{
    Success,
    InvalidId,
    NotConfigured,
    NotFound,
    RateLimited,
    Failed
}

public sealed record RecipeImageSearchResult(
    RecipeImageSearchStatus Status,
    IReadOnlyList<RecipeImageCandidate>? Candidates = null,
    string? Message = null);

public sealed record RecipeImageDownloadResult(
    RecipeImageDownloadStatus Status,
    RecipeImageContent? Content = null,
    string? Message = null);

public interface IRecipeImageProvider
{
    string Name { get; }

    Task<RecipeImageSearchResult> SearchAsync(
        RecipeImageSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<RecipeImageDownloadResult> DownloadAsync(
        string externalImageId,
        CancellationToken cancellationToken = default);
}
