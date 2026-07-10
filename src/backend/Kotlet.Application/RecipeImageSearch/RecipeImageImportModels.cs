namespace Kotlet.Application.RecipeImageSearch;

public sealed record RecipeImageImportRequest(string Provider, string ExternalImageId);

public sealed record RecipeImageImportContent(
    byte[] Content,
    string ContentType,
    int Width,
    int Height,
    string Provider,
    string ExternalImageId,
    string SourcePageUrl,
    string? AuthorName,
    string? AuthorUrl,
    string? AltText);

public enum RecipeImageImportStatus
{
    Success,
    InvalidRequest,
    NotConfigured,
    NotFound,
    RateLimited,
    Failed
}

public sealed record RecipeImageImportResult(
    RecipeImageImportStatus Status,
    RecipeImageImportContent? Image = null,
    string? Message = null);
