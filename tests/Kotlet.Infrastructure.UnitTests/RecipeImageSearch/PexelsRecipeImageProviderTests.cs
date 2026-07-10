using System.Net;
using System.Net.Http.Headers;
using Kotlet.Application.RecipeImageSearch;
using Kotlet.Infrastructure.RecipeImageSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.RecipeImageSearch;

public sealed class PexelsRecipeImageProviderTests
{
    [Fact]
    public async Task Search_MapsPhotosAndForwardsRequestDetails()
    {
        var handler = new RecordingHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
                {
                  "photos": [{
                    "id": 42,
                    "width": 1200,
                    "height": 800,
                    "url": "https://www.pexels.com/photo/42",
                    "photographer": "Ada",
                    "photographer_url": "https://www.pexels.com/@ada",
                    "alt": "Pasta",
                    "src": { "medium": "https://images.pexels.com/medium.jpg" }
                  }]
                }
                """)
        });
        var provider = Create(handler, "secret");

        var result = await provider.SearchAsync(new("pasta salad", 8, "landscape", "pl-PL"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("query=pasta%20salad", request.RequestUri!.Query);
        Assert.Contains("per_page=8", request.RequestUri.Query);
        Assert.Contains("orientation=landscape", request.RequestUri.Query);
        Assert.Contains("locale=pl-PL", request.RequestUri.Query);
        Assert.Equal("secret", request.Headers.GetValues("Authorization").Single());
        Assert.NotNull(result.Candidates);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(RecipeImageSearchStatus.Success, result.Status);
        Assert.Equal("42", candidate.ExternalImageId);
        Assert.Equal("Ada", candidate.AuthorName);
        Assert.Equal("https://images.pexels.com/medium.jpg", candidate.PreviewUrl);
    }

    [Fact]
    public async Task Download_UsesPhotoIdThenRetrievesImageWithoutLeakingApiKey()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/v1/photos/42")
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""
                    {
                      "id": 42,
                      "url": "https://www.pexels.com/photo/42",
                      "photographer": "Ada",
                      "src": { "original": "https://images.pexels.com/original.jpg" }
                    }
                    """)
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            });
        handler.ImageResponseContentType = "image/jpeg";
        var provider = Create(handler, "secret");

        var result = await provider.DownloadAsync("42");

        Assert.Equal(RecipeImageDownloadStatus.Success, result.Status);
        Assert.NotNull(result.Content);
        Assert.Equal([1, 2, 3], result.Content.Content);
        Assert.Equal("image/jpeg", result.Content.ContentType);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Authorization", handler.Requests[0].Headers.Select(h => h.Key));
        Assert.DoesNotContain("Authorization", handler.Requests[1].Headers.Select(h => h.Key));
    }

    [Fact]
    public async Task Search_MapsRateLimit()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await Create(handler, "secret").SearchAsync(new("pasta"));

        Assert.Equal(RecipeImageSearchStatus.RateLimited, result.Status);
    }

    private static PexelsRecipeImageProvider Create(RecordingHandler handler, string? apiKey) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.pexels.com/") },
            new PexelsOptions { ApiKey = apiKey }, NullLogger<PexelsRecipeImageProvider>.Instance);

    private static StringContent Json(string content) => new(content, System.Text.Encoding.UTF8, "application/json");

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string? ImageResponseContentType { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = responder(request);
            if (ImageResponseContentType is not null && response.Content is not null)
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(ImageResponseContentType);
            return Task.FromResult(response);
        }
    }
}
