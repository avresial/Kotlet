namespace Kotlet.Application.Ingredients;

public sealed class IngredientResolutionService(IngredientSearchService ingredientSearch)
{
    public const decimal MatchThreshold = 0.75m;

    public async Task<IReadOnlyList<IngredientResolution>> ResolveAsync(
        IReadOnlyList<string> names,
        CancellationToken cancellationToken)
    {
        var matches = await ingredientSearch.FindClosestAsync(names, cancellationToken);
        return matches.Select(match =>
        {
            var isCatalogMatch = match.Similarity >= MatchThreshold;
            return new IngredientResolution(
                match.InputName,
                isCatalogMatch ? match.IngredientId : null,
                isCatalogMatch ? match.MatchedName : null,
                match.Similarity,
                !isCatalogMatch);
        }).ToArray();
    }
}

public sealed record IngredientResolution(
    string SourceName,
    Guid? MatchedIngredientId,
    string? MatchedIngredientName,
    decimal? MatchScore,
    bool IsProposedNew);
