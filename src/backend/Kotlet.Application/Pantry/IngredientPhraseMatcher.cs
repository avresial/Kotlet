using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Pantry;

/// <summary>
/// Fuzzy-matches free-text observation phrases against ingredient names and their translations.
/// Pure and stateless apart from the searchable name index built at construction.
/// </summary>
internal sealed class IngredientPhraseMatcher
{
    internal const decimal MinimumCandidateConfidence = 0.55m;
    internal const decimal MinimumMatchedConfidence = 0.72m;
    internal const decimal AmbiguousConfidenceMargin = 0.08m;

    private readonly List<CatalogName> _searchableNames;

    public IngredientPhraseMatcher(
        IReadOnlyCollection<Ingredient> ingredients,
        IReadOnlyDictionary<string, string> translations)
    {
        _searchableNames = BuildSearchableNames(ingredients, translations);
    }

    public PhraseMatch Match(string normalizedPhrase)
    {
        var candidates = FindCandidates(normalizedPhrase);
        if (candidates.Count == 0 || candidates[0].Confidence < MinimumCandidateConfidence)
        {
            return new(PhraseMatchKind.Unmatched, candidates);
        }

        var hasCloseRunnerUp = candidates.Count > 1
            && candidates[1].Confidence >= MinimumCandidateConfidence
            && candidates[0].Confidence - candidates[1].Confidence < AmbiguousConfidenceMargin;
        var isAmbiguous = candidates[0].Confidence < MinimumMatchedConfidence || hasCloseRunnerUp;
        return new(isAmbiguous ? PhraseMatchKind.Ambiguous : PhraseMatchKind.Matched, candidates);
    }

    public static string NormalizePhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return string.Empty;
        }

        var characters = phrase.Trim().ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(' ', new string(characters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal static decimal Similarity(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1m;
        }

        var distance = Distance(left, right);
        var denominator = Math.Max(left.Length, right.Length);
        return denominator == 0 ? 0 : Math.Round(1m - (decimal)distance / denominator, 3);
    }

    private List<ScoredCandidate> FindCandidates(string normalizedPhrase) =>
        _searchableNames
            .Where(candidate => normalizedPhrase.Length > 0 && candidate.NormalizedName.Length > 0)
            .Where(candidate => CouldReachCandidateConfidence(normalizedPhrase, candidate.NormalizedName))
            .Select(candidate => new ScoredCandidate(
                candidate.Id,
                candidate.Name,
                candidate.MeasurementUnit,
                Similarity(normalizedPhrase, candidate.NormalizedName)))
            .GroupBy(candidate => candidate.Id)
            .Select(group => group.OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Id)
            .ToList();

    private static List<CatalogName> BuildSearchableNames(
        IReadOnlyCollection<Ingredient> ingredients,
        IReadOnlyDictionary<string, string> translations)
    {
        var names = new List<CatalogName>();
        foreach (var ingredient in ingredients)
        {
            AddCatalogName(names, ingredient.Id, ingredient.Name, ingredient.MeasurementUnit);
            foreach (var language in TranslationKeys.TranslatedLanguages)
            {
                var translation = translations.GetValueOrDefault(TranslationKeys.Ingredient(ingredient.Id, language));
                if (!string.IsNullOrWhiteSpace(translation))
                {
                    AddCatalogName(names, ingredient.Id, translation, ingredient.MeasurementUnit);
                }
            }
        }
        return names;
    }

    private static void AddCatalogName(
        ICollection<CatalogName> names,
        Guid ingredientId,
        string name,
        string measurementUnit) =>
        names.Add(new(ingredientId, name.Trim(), measurementUnit, NormalizePhrase(name)));

    // A cheap upper bound on similarity lets us skip the edit-distance computation for names
    // whose length difference alone rules them out.
    private static bool CouldReachCandidateConfidence(string left, string right)
    {
        var longestLength = Math.Max(left.Length, right.Length);
        var lengthDifference = Math.Abs(left.Length - right.Length);
        var maximumSimilarity = 1m - (decimal)lengthDifference / longestLength;
        return maximumSimilarity >= MinimumCandidateConfidence;
    }

    private static int Distance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private sealed record CatalogName(Guid Id, string Name, string MeasurementUnit, string NormalizedName);
}

internal enum PhraseMatchKind
{
    Unmatched,
    Ambiguous,
    Matched
}

internal sealed record PhraseMatch(PhraseMatchKind Kind, IReadOnlyList<ScoredCandidate> Candidates);

internal sealed record ScoredCandidate(Guid Id, string Name, string MeasurementUnit, decimal Confidence);
