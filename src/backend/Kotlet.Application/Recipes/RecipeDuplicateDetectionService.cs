using System.Text;
using System.Text.RegularExpressions;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeDuplicateDetectionService(IRecipeRepository repository)
{
    private const double SimilarTitleThreshold = 0.6;

    public async Task<RecipeExistenceResult> CheckExistsAsync(
        Guid houseId, string? title, string? sourceUrl, CancellationToken cancellationToken)
    {
        var recipes = await repository.GetAllForDuplicateCheckAsync(houseId, cancellationToken);
        var normalizedUrl = RecipeSourceUrl.Normalize(sourceUrl);
        var titleTokens = TokenizeTitle(title);
        var matches = recipes
            .Select(recipe => (Recipe: recipe, MatchType: Classify(recipe, normalizedUrl, titleTokens)))
            .Where(match => match.MatchType is not null)
            .Select(match => new RecipeExistenceMatch(
                match.Recipe.Id,
                match.Recipe.Title,
                match.Recipe.SourceUrl ?? RecipeSourceUrl.Extract(match.Recipe.DescriptionMarkdown),
                match.MatchType!.Value))
            .OrderBy(match => match.MatchType)
            .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new(matches.Count > 0, matches);
    }

    private static RecipeMatchType? Classify(
        Recipe recipe, string? normalizedUrl, IReadOnlyList<string> titleTokens)
    {
        if (normalizedUrl is not null
            && (string.Equals(RecipeSourceUrl.Normalize(recipe.SourceUrl), normalizedUrl, StringComparison.OrdinalIgnoreCase)
                || RecipeSourceUrl.DescriptionContains(recipe.DescriptionMarkdown, normalizedUrl)))
            return RecipeMatchType.SourceUrl;
        if (titleTokens.Count == 0)
            return null;

        var recipeTokens = TokenizeTitle(recipe.Title);
        if (recipeTokens.SequenceEqual(titleTokens))
            return RecipeMatchType.ExactTitle;
        return TitleSimilarity(titleTokens, recipeTokens) >= SimilarTitleThreshold
            ? RecipeMatchType.SimilarTitle
            : null;
    }

    private static IReadOnlyList<string> TokenizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in title)
        {
            if (char.IsLetterOrDigit(character))
                current.Append(char.ToLowerInvariant(character));
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    private static double TitleSimilarity(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        var union = leftSet.Union(rightSet).Count();
        return union == 0 ? 0 : (double)leftSet.Intersect(rightSet).Count() / union;
    }
}

internal static class RecipeSourceUrl
{
    private static readonly Regex SourceLinePattern = new(
        @"^[\s>*_#-]*source[\s*_]*:?\s*<?(?<url>https?://[^\s<>)\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    internal static string? Normalize(string? url)
    {
        var trimmed = url?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    internal static string? Extract(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return null;
        var match = SourceLinePattern.Match(description);
        return match.Success ? match.Groups["url"].Value.TrimEnd('.', ',', ';') : null;
    }

    internal static bool DescriptionContains(string? description, string normalizedUrl)
    {
        if (string.IsNullOrEmpty(description))
            return false;
        var start = 0;
        while (true)
        {
            var index = description.IndexOf(normalizedUrl, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;
            var rest = description.AsSpan(index + normalizedUrl.Length).TrimStart('/');
            if (rest.IsEmpty || !IsPathCharacter(rest[0]))
                return true;
            start = index + 1;
        }
    }

    private static bool IsPathCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '%' or '~' or '+';
}
