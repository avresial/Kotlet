using Kotlet.Application.Translations;

namespace Kotlet.Application.Ingredients;

public sealed class IngredientSearchService(
    IIngredientRepository ingredients,
    ITranslationRepository translations)
{
    public async Task<IReadOnlyList<IngredientSearchResult>> FindClosestAsync(
        IReadOnlyList<string> names,
        CancellationToken cancellationToken)
    {
        var catalog = await ingredients.GetAllAsync(cancellationToken);
        var dictionary = await translations.GetAllAsync(cancellationToken);
        var searchableNames = catalog.SelectMany(ingredient =>
            new[] { (Name: (string?)ingredient.Name, Language: TranslationKeys.DefaultLanguage) }
                .Concat(TranslationKeys.TranslatedLanguages.Select(language =>
                    (dictionary.GetValueOrDefault(TranslationKeys.Ingredient(ingredient.Id, language)), language)))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Item1))
                .Select(entry => (ingredient.Id, Name: entry.Item1!, Language: entry.Item2, ingredient.MeasurementUnit)))
            .ToArray();

        var results = new IngredientSearchResult[names.Count];
        for (int i = 0; i < names.Count; i++)
        {
            var input = names[i];
            var name = input?.Trim() ?? string.Empty;

            if (name.Length == 0 || searchableNames.Length == 0)
            {
                results[i] = new IngredientSearchResult(input ?? string.Empty, null, null, null, null, null, false);
                continue;
            }

            var bestCandidate = searchableNames[0];
            var minDistance = Distance(name, bestCandidate.Name);

            for (var j = 1; j < searchableNames.Length; j++)
            {
                var candidate = searchableNames[j];
                var distance = Distance(name, candidate.Name);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCandidate = candidate;
                    continue;
                }

                if (distance != minDistance)
                {
                    continue;
                }

                var nameComparison = string.Compare(candidate.Name, bestCandidate.Name, StringComparison.OrdinalIgnoreCase);
                if (nameComparison < 0 ||
                    (nameComparison == 0 && candidate.Id.CompareTo(bestCandidate.Id) < 0))
                {
                    bestCandidate = candidate;
                }
            }

            results[i] = new IngredientSearchResult(
                input ?? string.Empty,
                bestCandidate.Id,
                bestCandidate.Name,
                bestCandidate.Language,
                bestCandidate.MeasurementUnit,
                minDistance,
                minDistance == 0,
                Math.Round(1m - (decimal)minDistance / Math.Max(name.Length, bestCandidate.Name.Length), 3));
        }
        return results;
    }

    private static int Distance(string left, string right)
    {
        var s1 = left.ToUpperInvariant();
        var s2 = right.ToUpperInvariant();
        var n = s1.Length;
        var m = s2.Length;

        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        if (n < m)
        {
            (s1, s2) = (s2, s1);
            (n, m) = (m, n);
        }

        var d = new int[m + 1];
        for (var j = 0; j <= m; j++)
        {
            d[j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            var prevDiag = d[0];
            d[0] = i;
            var character = s1[i - 1];
            for (var j = 1; j <= m; j++)
            {
                var oldD = d[j];
                var cost = character == s2[j - 1] ? 0 : 1;
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prevDiag + cost);
                prevDiag = oldD;
            }
        }

        return d[m];
    }
}

public sealed record IngredientSearchResult(
    string InputName,
    Guid? IngredientId,
    string? MatchedName,
    string? MatchedLanguage,
    string? MeasurementUnit,
    int? Distance,
    bool ExactMatch,
    decimal? Similarity = null);
