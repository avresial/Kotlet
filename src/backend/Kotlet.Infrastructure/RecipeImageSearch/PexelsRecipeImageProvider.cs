using System.Globalization;
using System.Net;
using System.Text.Json;
using Kotlet.Application.RecipeImageSearch;
using Microsoft.Extensions.Logging;

namespace Kotlet.Infrastructure.RecipeImageSearch;

internal sealed class PexelsRecipeImageProvider(
    HttpClient httpClient,
    PexelsOptions options,
    RecipeImagesOptions recipeImages,
    ILogger<PexelsRecipeImageProvider> logger) : IRecipeImageProvider
{
    public const string ProviderName = "Pexels";
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => ProviderName;

    public async Task<RecipeImageSearchResult> SearchAsync(
        RecipeImageSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsSelectedProvider() || !options.IsConfigured)
        {
            return new(RecipeImageSearchStatus.NotConfigured,
                Message: "The recipe image provider is not configured.");
        }

        try
        {
            var query = $"v1/search?query={Uri.EscapeDataString(request.Query)}&per_page={request.Limit}";
            if (!string.IsNullOrWhiteSpace(request.Orientation))
                query += $"&orientation={Uri.EscapeDataString(request.Orientation)}";
            if (!string.IsNullOrWhiteSpace(request.Locale))
                query += $"&locale={Uri.EscapeDataString(request.Locale)}";

            using var response = await SendApiRequestAsync(query, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return MapStatus(response.StatusCode, "The recipe image search request failed.");

            var payload = await JsonSerializer.DeserializeAsync<SearchResponse>(
                await response.Content.ReadAsStreamAsync(cancellationToken), JsonOptions, cancellationToken);
            if (payload?.Photos is null)
                return Failed("The recipe image search response was malformed.");

            var candidates = new List<RecipeImageCandidate>(payload.Photos.Count);
            foreach (var photo in payload.Photos)
            {
                var candidate = MapCandidate(photo);
                if (candidate is null)
                    return Failed("The recipe image search response was malformed.");
                candidates.Add(candidate);
            }

            return new(RecipeImageSearchStatus.Success, candidates);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed("The recipe image search request timed out.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Pexels recipe image search failed");
            return Failed("The recipe image search request failed.");
        }
    }

    public async Task<RecipeImageDownloadResult> DownloadAsync(
        string externalImageId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSelectedProvider() || !options.IsConfigured)
        {
            return new(RecipeImageDownloadStatus.NotConfigured,
                Message: "The recipe image provider is not configured.");
        }

        if (!int.TryParse(externalImageId, NumberStyles.None, CultureInfo.InvariantCulture, out var photoId) || photoId <= 0)
            return new(RecipeImageDownloadStatus.InvalidId, Message: "The provider image id is invalid.");

        try
        {
            using var photoResponse = await SendApiRequestAsync(
                $"v1/photos/{photoId.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
            if (!photoResponse.IsSuccessStatusCode)
                return MapDownloadStatus(photoResponse.StatusCode, "The recipe image could not be found.");

            var photo = await JsonSerializer.DeserializeAsync<Photo>(
                await photoResponse.Content.ReadAsStreamAsync(cancellationToken), JsonOptions, cancellationToken);
            var imageUrl = photo?.Src?.Original ?? photo?.Src?.Large2x ?? photo?.Src?.Large ?? photo?.Src?.Medium;
            if (photo is null || photo.Url is null || string.IsNullOrWhiteSpace(imageUrl))
                return new(RecipeImageDownloadStatus.Failed, Message: "The provider image response was malformed.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            using var imageRequest = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            using var imageResponse = await httpClient.SendAsync(imageRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!imageResponse.IsSuccessStatusCode)
                return MapDownloadStatus(imageResponse.StatusCode, "The provider image download failed.");
            if (imageResponse.Content.Headers.ContentLength > MaxDownloadBytes)
                return new(RecipeImageDownloadStatus.Failed, Message: "The provider image is too large.");

            var content = await ReadContentAsync(imageResponse.Content, timeout.Token);
            if (content is null)
                return new(RecipeImageDownloadStatus.Failed, Message: "The provider image is too large.");

            return new(RecipeImageDownloadStatus.Success, new RecipeImageContent(
                content,
                imageResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                externalImageId,
                photo.Url,
                photo.Photographer,
                photo.PhotographerUrl,
                photo.Alt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(RecipeImageDownloadStatus.Failed, Message: "The provider image download timed out.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Pexels recipe image download failed for {ExternalImageId}", externalImageId);
            return new(RecipeImageDownloadStatus.Failed, Message: "The provider image download failed.");
        }
    }

    private async Task<HttpResponseMessage> SendApiRequestAsync(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Authorization", options.ApiKey);
        return await httpClient.SendAsync(request, timeout.Token);
    }

    private bool IsSelectedProvider() =>
        string.Equals(recipeImages.Provider, ProviderName, StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]?> ReadContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaxDownloadBytes) return null;
            output.Write(buffer, 0, read);
        }
    }

    private static RecipeImageCandidate? MapCandidate(Photo photo)
    {
        var previewUrl = photo.Src?.Medium ?? photo.Src?.Large ?? photo.Src?.Landscape;
        return photo.Id > 0 && !string.IsNullOrWhiteSpace(photo.Url) && !string.IsNullOrWhiteSpace(previewUrl)
            ? new(ProviderName, photo.Id.ToString(CultureInfo.InvariantCulture), previewUrl, photo.Url!,
                photo.Photographer, photo.PhotographerUrl, photo.Alt,
                photo.Width > 0 ? photo.Width : null, photo.Height > 0 ? photo.Height : null)
            : null;
    }

    private static RecipeImageSearchResult Failed(string message) =>
        new(RecipeImageSearchStatus.Failed, Message: message);

    private static RecipeImageSearchResult MapStatus(HttpStatusCode statusCode, string message) =>
        new(statusCode == HttpStatusCode.TooManyRequests
            ? RecipeImageSearchStatus.RateLimited
            : RecipeImageSearchStatus.Failed, Message: message);

    private static RecipeImageDownloadResult MapDownloadStatus(HttpStatusCode statusCode, string message) =>
        new(statusCode switch
        {
            HttpStatusCode.NotFound => RecipeImageDownloadStatus.NotFound,
            HttpStatusCode.TooManyRequests => RecipeImageDownloadStatus.RateLimited,
            _ => RecipeImageDownloadStatus.Failed
        }, Message: message);

    private sealed record SearchResponse(IReadOnlyList<Photo>? Photos);

    private sealed record Photo(
        int Id,
        int Width,
        int Height,
        string? Url,
        string? Photographer,
        string? PhotographerUrl,
        string? Alt,
        PhotoSources? Src);

    private sealed record PhotoSources(
        string? Original,
        string? Large2x,
        string? Large,
        string? Medium,
        string? Landscape);
}
