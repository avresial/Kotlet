using Kotlet.Application.Images;
using Kotlet.Application.RecipeImageSearch;
using Xunit;

namespace Kotlet.Application.UnitTests.RecipeImageSearch;

public sealed class RecipeImageImportServiceTests
{
    private static readonly RecipeImageContent DownloadedImage = new(
        [1, 2, 3], "image/jpeg", "42", "https://www.pexels.com/photo/42", "Ada",
        "https://pexels.com/@ada", "Pasta");

    [Fact]
    public async Task Import_DownloadsAndProcessesTheSelectedImage()
    {
        var provider = new FakeProvider(RecipeImageDownloadStatus.Success, DownloadedImage);
        var processor = new FakeProcessor();
        var service = new RecipeImageImportService([provider], processor);

        var result = await service.ImportAsync(new("Pexels", "42"));

        Assert.Equal(RecipeImageImportStatus.Success, result.Status);
        Assert.Equal("Pexels", result.Image!.Provider);
        Assert.Equal("42", result.Image.ExternalImageId);
        Assert.Equal("image/webp", result.Image.ContentType);
        Assert.Equal([9, 8, 7], result.Image.Content);
        Assert.Equal(new ImageProcessingOptions(1200, 900), processor.Options);
    }

    [Fact]
    public async Task Import_RejectsUnknownProviderWithoutDownloading()
    {
        var provider = new FakeProvider(RecipeImageDownloadStatus.Success, DownloadedImage);
        var service = new RecipeImageImportService([provider], new FakeProcessor());

        var result = await service.ImportAsync(new("Unknown", "42"));

        Assert.Equal(RecipeImageImportStatus.InvalidRequest, result.Status);
        Assert.False(provider.Downloaded);
    }

    [Theory]
    [InlineData(RecipeImageDownloadStatus.NotConfigured, RecipeImageImportStatus.NotConfigured)]
    [InlineData(RecipeImageDownloadStatus.NotFound, RecipeImageImportStatus.NotFound)]
    [InlineData(RecipeImageDownloadStatus.InvalidId, RecipeImageImportStatus.NotFound)]
    [InlineData(RecipeImageDownloadStatus.RateLimited, RecipeImageImportStatus.RateLimited)]
    [InlineData(RecipeImageDownloadStatus.Failed, RecipeImageImportStatus.Failed)]
    public async Task Import_MapsProviderDownloadFailures(RecipeImageDownloadStatus downloadStatus, RecipeImageImportStatus expected)
    {
        var service = new RecipeImageImportService(
            [new FakeProvider(downloadStatus, null)], new FakeProcessor());

        var result = await service.ImportAsync(new("Pexels", "42"));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Import_RejectsInvalidImage()
    {
        var service = new RecipeImageImportService(
            [new FakeProvider(RecipeImageDownloadStatus.Success, DownloadedImage)], new FakeProcessor(true));

        var result = await service.ImportAsync(new("Pexels", "42"));

        Assert.Equal(RecipeImageImportStatus.Failed, result.Status);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task Import_OmitsProviderAltTextThatCannotBeStored()
    {
        var downloaded = DownloadedImage with { AltText = new string('x', 301) };
        var service = new RecipeImageImportService(
            [new FakeProvider(RecipeImageDownloadStatus.Success, downloaded)], new FakeProcessor());

        var result = await service.ImportAsync(new("Pexels", "42"));

        Assert.Equal(RecipeImageImportStatus.Success, result.Status);
        Assert.Null(result.Image!.AltText);
    }

    private sealed class FakeProvider(RecipeImageDownloadStatus status, RecipeImageContent? content) : IRecipeImageProvider
    {
        public string Name => "Pexels";
        public bool Downloaded { get; private set; }

        public Task<RecipeImageSearchResult> SearchAsync(RecipeImageSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeImageSearchResult(RecipeImageSearchStatus.Success, []));

        public Task<RecipeImageDownloadResult> DownloadAsync(string externalImageId, CancellationToken cancellationToken = default)
        {
            Downloaded = true;
            return Task.FromResult(new RecipeImageDownloadResult(status, content));
        }
    }

    private sealed class FakeProcessor(bool throwInvalidImage = false) : IImageProcessor
    {
        public ImageProcessingOptions? Options { get; private set; }

        public Task<ImageProcessingResult> ProcessAsync(Stream image, ImageProcessingOptions options, CancellationToken cancellationToken = default)
        {
            Options = options;
            if (throwInvalidImage) throw new InvalidImageException("not an image");
            return Task.FromResult(new ImageProcessingResult([9, 8, 7], "image/webp", 1200, 900));
        }
    }
}
