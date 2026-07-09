using Kotlet.Application.VideoTranscripts;
using Xunit;

namespace Kotlet.Application.UnitTests.VideoTranscripts;

public sealed class VideoTranscriptServiceTests
{
    [Theory]
    [InlineData("https://example.com/video")]
    [InlineData("not a url")]
    [InlineData("ftp://youtube.com/video")]
    public async Task GetAsync_WithUnsupportedUrl_ReturnsInvalidUrlWithoutCallingProvider(string value)
    {
        var provider = new FakeProvider(new(VideoTranscriptStatus.Success));
        var service = new VideoTranscriptService(provider);

        var result = await service.GetAsync(new Uri(value, UriKind.RelativeOrAbsolute), CancellationToken.None);

        Assert.Equal(VideoTranscriptStatus.InvalidUrl, result.Status);
        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", Platform.YouTube)]
    [InlineData("https://youtu.be/abc", Platform.YouTube)]
    [InlineData("https://www.tiktok.com/@cook/video/123", Platform.TikTok)]
    [InlineData("https://vm.tiktok.com/abc", Platform.TikTok)]
    public async Task GetAsync_WithSupportedUrl_DelegatesToProvider(string value, Platform platform)
    {
        var content = new VideoContent("transcript", "title", "description", "author", platform);
        var provider = new FakeProvider(new(VideoTranscriptStatus.Success, content));
        var service = new VideoTranscriptService(provider);

        var result = await service.GetAsync(new Uri(value), CancellationToken.None);

        Assert.Equal(VideoTranscriptStatus.Success, result.Status);
        Assert.Equal(content, result.Content);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(new Uri(value), provider.LastUrl);
    }

    [Fact]
    public async Task GetAsync_PropagatesProviderStatus()
    {
        var provider = new FakeProvider(new(VideoTranscriptStatus.NotConfigured));
        var service = new VideoTranscriptService(provider);

        var result = await service.GetAsync(new Uri("https://youtube.com/watch?v=abc"), CancellationToken.None);

        Assert.Equal(VideoTranscriptStatus.NotConfigured, result.Status);
    }

    private sealed class FakeProvider(VideoTranscriptResult result) : IVideoTranscriptProvider
    {
        public int CallCount { get; private set; }
        public Uri? LastUrl { get; private set; }

        public Task<VideoTranscriptResult> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            CallCount++;
            LastUrl = url;
            return Task.FromResult(result);
        }
    }
}
