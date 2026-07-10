using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed record RecipeImageResponse(
    Guid Id, Guid RecipeId, string FileName, string ContentType, long FileSizeBytes,
    string? AltText, int SortOrder, string ContentUrl, DateTimeOffset CreatedAtUtc,
    SourceAttributionResponse? Source = null);

/// <summary>Subset of stored source metadata needed to render attribution in the UI.</summary>
public sealed record SourceAttributionResponse(
    string Provider, string? AuthorName, string? AuthorUrl, string? Url)
{
    /// <summary>Maps the primary (first) source of an image; null when the image has none.</summary>
    public static SourceAttributionResponse? FromPrimarySource(RecipeImage image)
    {
        var source = image.Sources.Select(s => s.Source).FirstOrDefault();
        return source is null ? null : new(source.Provider, source.AuthorName, source.AuthorUrl, source.Url);
    }
}

public sealed record RecipeImageContent(string FileName, string ContentType, byte[] Content);
public sealed record UpdateRecipeImageRequest(string? AltText);
public sealed record ReorderRecipeImagesRequest(IReadOnlyList<Guid> ImageIds);

public enum RecipeImageOperationStatus { Success, NotFound, ValidationFailed, LimitExceeded }

public sealed record RecipeImageOperationResult(
    RecipeImageOperationStatus Status,
    RecipeImageResponse? Image = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
