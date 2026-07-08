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

        return names.Select(input =>
        {
            var name = input?.Trim() ?? string.Empty;
            var match = searchableNames
                .Select(candidate => (Candidate: candidate, Distance: Distance(name, candidate.Name)))
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Candidate.Id)
                .FirstOrDefault();

            return name.Length == 0 || searchableNames.Length == 0
                ? new IngredientSearchResult(input ?? string.Empty, null, null, null, null, null, false)
                : new IngredientSearchResult(
                    input ?? string.Empty,
                    match.Candidate.Id,
                    match.Candidate.Name,
                    match.Candidate.Language,
                    match.Candidate.MeasurementUnit,
                    match.Distance,
                    match.Distance == 0,
                    Math.Round(1m - (decimal)match.Distance / Math.Max(name.Length, match.Candidate.Name.Length), 3));
        }).ToArray();
    }

    private static int Distance(string left, string right)
    {
        left = left.ToUpperInvariant();
        right = right.ToUpperInvariant();
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();

        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            previous = current;
        }

        return previous[right.Length];
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
