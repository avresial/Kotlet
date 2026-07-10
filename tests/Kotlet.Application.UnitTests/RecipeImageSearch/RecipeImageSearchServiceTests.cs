using Kotlet.Application.RecipeImageSearch;
using Xunit;

namespace Kotlet.Application.UnitTests.RecipeImageSearch;

public sealed class RecipeImageSearchServiceTests
{
    [Fact]
    public async Task Search_RejectsBlankQueryWithoutCallingProvider()
    {
        var provider = new FakeProvider();

        var result = await new RecipeImageSearchService(provider)
            .SearchAsync(new RecipeImageSearchRequest("  "));

        Assert.Equal(RecipeImageSearchStatus.InvalidQuery, result.Status);
        Assert.False(provider.Called);
    }

    [Fact]
    public async Task Search_TrimsQueryAndClampsLimit()
    {
        var provider = new FakeProvider(new(RecipeImageSearchStatus.Success, []));

        var result = await new RecipeImageSearchService(provider)
            .SearchAsync(new RecipeImageSearchRequest("  pasta  ", 100));

        Assert.Equal(RecipeImageSearchStatus.Success, result.Status);
        Assert.Equal("pasta", provider.Request!.Query);
        Assert.Equal(RecipeImageSearchService.MaxLimit, provider.Request.Limit);
    }

    private sealed class FakeProvider(RecipeImageSearchResult? result = null) : IRecipeImageProvider
    {
        public bool Called { get; private set; }
        public RecipeImageSearchRequest? Request { get; private set; }
        public string Name => "fake";

        public Task<RecipeImageSearchResult> SearchAsync(RecipeImageSearchRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            Request = request;
            return Task.FromResult(result ?? new RecipeImageSearchResult(RecipeImageSearchStatus.Success, []));
        }

        public Task<RecipeImageDownloadResult> DownloadAsync(string externalImageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeImageDownloadResult(RecipeImageDownloadStatus.InvalidId));
    }
}
