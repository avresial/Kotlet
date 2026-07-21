using Kotlet.Domain.Images;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Sources;

namespace Kotlet.Application.Images;

public sealed record StoredImageSourceData(string Provider, string? ExternalId, string? Url, string? AuthorName, string? AuthorUrl);
public sealed record StoredImageContent(string FileName, string ContentType, byte[] Content);
public sealed record StoredImageCreateResult(StoredImage? Image, IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed class StoredImageService(IStoredImageRepository repository, IImageProcessor processor)
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerOwner = 10;
    public const int ProcessedMaxWidth = 1200;
    public const int ProcessedMaxHeight = 900;
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private static readonly Dictionary<string, string[]> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"]
    };

    public async Task<StoredImageCreateResult> CreateAsync(string fileName, string contentType, byte[] content,
        string? altText, StoredImageSourceData? source, CancellationToken ct)
    {
        var errors = Validate(fileName, contentType, content, altText, source);
        if (errors.Count > 0) return new(null, errors);
        ImageProcessingResult processed;
        try
        {
            using var stream = new MemoryStream(content, false);
            processed = await processor.ProcessAsync(
                stream,
                new(ProcessedMaxWidth, ProcessedMaxHeight),
                ct);
        }
        catch (InvalidImageException)
        {
            return new(null, new Dictionary<string, string[]>
            {
                ["file"] = ["The file is not a valid image."]
            });
        }
        var image = new StoredImage
        {
            Id = Guid.NewGuid(),
            FileName = Path.ChangeExtension(Path.GetFileName(fileName), ".webp"),
            ContentType = processed.ContentType,
            FileSizeBytes = processed.Content.LongLength,
            Content = processed.Content,
            AltText = altText?.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Sources = source is null ? [] : [new RecipeImageSource { Source = new Source { Id = Guid.NewGuid(), Type = SourceType.ExternalImage,
                Provider = source.Provider.Trim(), ExternalId = source.ExternalId?.Trim(), Url = source.Url?.Trim(), AuthorName = source.AuthorName?.Trim(),
                AuthorUrl = source.AuthorUrl?.Trim(), RetrievedAtUtc = DateTimeOffset.UtcNow } }]
        };
        repository.Add(image);
        return new(image);
    }

    public async Task<StoredImageContent?> GetContentAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, true, ct) is { } image ? new(image.FileName, image.ContentType, image.Content) : null;

    public async Task<bool> UpdateAltTextAsync(Guid id, string? altText, CancellationToken ct)
    {
        if (altText?.Trim().Length > 300 || await repository.GetAsync(id, false, ct) is null) return false;
        await repository.UpdateAltTextAsync(id, string.IsNullOrWhiteSpace(altText) ? null : altText.Trim(), DateTimeOffset.UtcNow, ct);
        return true;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct) => repository.DeleteAsync(id, ct);

    private static Dictionary<string, string[]> Validate(string fileName, string contentType, byte[] content, string? altText, StoredImageSourceData? source)
    {
        var errors = new Dictionary<string, string[]>();
        if (content.Length == 0) errors["file"] = ["Image file must not be empty."];
        else if (content.LongLength > MaxFileSizeBytes) errors["file"] = ["Image file cannot exceed 5 MB."];
        if (!AllowedTypes.Contains(contentType)) errors["contentType"] = ["Only JPEG, PNG, and WebP images are supported."];
        else if (!AllowedExtensions[contentType].Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase)) errors["fileName"] = ["File extension does not match its content type."];
        if (Path.GetFileName(fileName).Length > 260) errors["fileName"] = ["File name cannot exceed 260 characters."];
        if (altText?.Trim().Length > 300) errors["altText"] = ["Alt text cannot exceed 300 characters."];
        if (source is not null)
        {
            if (string.IsNullOrWhiteSpace(source.Provider) || source.Provider.Trim().Length > 100) errors["sourceProvider"] = ["Image source provider is required and cannot exceed 100 characters."];
            if (source.ExternalId?.Trim().Length > 200) errors["sourceExternalId"] = ["Image source id cannot exceed 200 characters."];
            if (source.Url is not null && (!Uri.TryCreate(source.Url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) errors["sourceUrl"] = ["Image source URL must be an absolute http(s) URL."];
            if (source.AuthorName?.Trim().Length > 200) errors["sourceAuthorName"] = ["Image author name cannot exceed 200 characters."];
            if (source.AuthorUrl is not null && (!Uri.TryCreate(source.AuthorUrl.Trim(), UriKind.Absolute, out var authorUri) || authorUri.Scheme is not ("http" or "https"))) errors["sourceAuthorUrl"] = ["Image author URL must be an absolute http(s) URL."];
        }
        return errors;
    }
}
