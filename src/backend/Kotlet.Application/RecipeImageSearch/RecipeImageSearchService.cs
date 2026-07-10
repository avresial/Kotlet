namespace Kotlet.Application.RecipeImageSearch;

public sealed class RecipeImageSearchService(IRecipeImageProvider provider)
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 12;

    public Task<RecipeImageSearchResult> SearchAsync(
        RecipeImageSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Task.FromResult(new RecipeImageSearchResult(
                RecipeImageSearchStatus.InvalidQuery,
                Message: "A recipe image search query is required."));
        }

        var normalized = request with
        {
            Query = request.Query.Trim(),
            Limit = Math.Clamp(request.Limit <= 0 ? DefaultLimit : request.Limit, 1, MaxLimit)
        };
        return provider.SearchAsync(normalized, cancellationToken);
    }
}
