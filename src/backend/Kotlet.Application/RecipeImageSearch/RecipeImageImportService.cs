using Kotlet.Application.Images;

namespace Kotlet.Application.RecipeImageSearch;

public sealed class RecipeImageImportService(
    IEnumerable<IRecipeImageProvider> providers,
    IImageProcessor imageProcessor)
{
    public const int MaxWidth = 1200;
    public const int MaxHeight = 900;
    private const long MaxProcessedBytes = 5 * 1024 * 1024;

    public async Task<RecipeImageImportResult> ImportAsync(
        RecipeImageImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.ExternalImageId))
            return new(RecipeImageImportStatus.InvalidRequest, Message: "Provider and external image id are required.");

        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.Name, request.Provider.Trim(), StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return new(RecipeImageImportStatus.InvalidRequest, Message: "The requested image provider is unavailable.");

        var downloaded = await provider.DownloadAsync(request.ExternalImageId.Trim(), cancellationToken);
        if (downloaded.Status != RecipeImageDownloadStatus.Success || downloaded.Content is null)
            return MapDownloadResult(downloaded);

        ImageProcessingResult processed;
        try
        {
            using var input = new MemoryStream(downloaded.Content.Content, writable: false);
            processed = await imageProcessor.ProcessAsync(
                input, new ImageProcessingOptions(MaxWidth, MaxHeight), cancellationToken);
        }
        catch (InvalidImageException)
        {
            return new(RecipeImageImportStatus.Failed, Message: "The provider image is not a supported image.");
        }

        if (!string.Equals(processed.ContentType, "image/webp", StringComparison.OrdinalIgnoreCase))
            return new(RecipeImageImportStatus.Failed, Message: "The processed provider image was not WebP.");
        if (processed.Content.LongLength > MaxProcessedBytes)
            return new(RecipeImageImportStatus.Failed, Message: "The processed provider image is too large.");

        return new(RecipeImageImportStatus.Success, new RecipeImageImportContent(
            processed.Content,
            "image/webp",
            processed.Width,
            processed.Height,
            provider.Name,
            downloaded.Content.ExternalImageId,
            downloaded.Content.SourcePageUrl,
            downloaded.Content.AuthorName,
            downloaded.Content.AuthorUrl,
            NormalizeAltText(downloaded.Content.AltText)));
    }

    private static string? NormalizeAltText(string? altText)
    {
        var value = altText?.Trim();
        return value is { Length: > 300 } ? null : value;
    }

    private static RecipeImageImportResult MapDownloadResult(RecipeImageDownloadResult result) =>
        new(result.Status switch
        {
            RecipeImageDownloadStatus.NotConfigured => RecipeImageImportStatus.NotConfigured,
            RecipeImageDownloadStatus.NotFound or RecipeImageDownloadStatus.InvalidId => RecipeImageImportStatus.NotFound,
            RecipeImageDownloadStatus.RateLimited => RecipeImageImportStatus.RateLimited,
            _ => RecipeImageImportStatus.Failed
        }, Message: result.Message ?? "The provider image download failed.");
}
