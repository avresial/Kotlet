using Kotlet.Application.Recipes;
using Kotlet.Application.Translations;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Pantry;

/// <summary>
/// Suggests up to <see cref="MaxSuggestions"/> recipes the house can cook from its pantry,
/// preferring the recipes whose required ingredients are best covered by what is on hand.
/// Version 1 ignores required quantities: an ingredient counts as available when the pantry
/// holds any amount of it. As soon as <see cref="MaxSuggestions"/> fully satisfied recipes
/// are found the search stops and returns those.
/// </summary>
public sealed class PantryRecipeMatchService(
    IPantryRepository pantryRepository,
    IRecipeRepository recipeRepository,
    IPantryRecipeMatchCache cache,
    ITranslationRepository translations)
{
    public const int MaxSuggestions = 5;

    public async Task<IReadOnlyList<PantryRecipeMatchDto>> GetSuggestionsAsync(
        Guid houseId, string languageCode, CancellationToken cancellationToken)
    {
        if (!cache.TryGet(houseId, out var matches) || matches is null)
        {
            matches = await ComputeAsync(houseId, cancellationToken);
            cache.Set(houseId, matches);
        }
        return await LocalizeAsync(matches, languageCode, cancellationToken);
    }

    private async Task<IReadOnlyList<PantryRecipeMatchDto>> ComputeAsync(Guid houseId, CancellationToken cancellationToken)
    {
        var pantryItems = await pantryRepository.GetAllAsync(houseId, cancellationToken);
        var available = pantryItems.Where(item => item.Quantity.Amount > 0).Select(item => item.IngredientId).ToHashSet();
        if (available.Count == 0)
            return [];

        var recipes = await recipeRepository.GetAllWithIngredientsAsync(houseId, cancellationToken);
        var fullMatches = new List<PantryRecipeMatchDto>();
        var partialMatches = new List<PantryRecipeMatchDto>();
        foreach (var recipe in recipes)
        {
            if (ToMatch(recipe, available) is not { } match)
                continue;
            if (match.IsFullMatch)
            {
                fullMatches.Add(match);
                if (fullMatches.Count == MaxSuggestions)
                    return fullMatches;
            }
            else
            {
                partialMatches.Add(match);
            }
        }

        return fullMatches
            .Concat(partialMatches
                .OrderByDescending(match => match.MatchedIngredientCount)
                .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .ToArray();
    }

    private static PantryRecipeMatchDto? ToMatch(Recipe recipe, HashSet<Guid> availableIngredientIds)
    {
        var required = recipe.Ingredients.DistinctBy(i => i.IngredientId).ToArray();
        if (required.Length == 0)
            return null;

        var missing = required
            .Where(i => !availableIngredientIds.Contains(i.IngredientId))
            .Select(i => new PantryRecipeMatchIngredientDto(i.IngredientId, i.Ingredient.Name))
            .ToArray();
        var matched = required.Length - missing.Length;
        return matched == 0
            ? null
            : new(recipe.Id, recipe.Title, recipe.Slug, required.Length, matched, missing.Length == 0, missing);
    }

    private async Task<IReadOnlyList<PantryRecipeMatchDto>> LocalizeAsync(
        IReadOnlyList<PantryRecipeMatchDto> matches, string languageCode, CancellationToken cancellationToken)
    {
        if (TranslationKeys.IsDefaultLanguage(languageCode) || matches.All(match => match.MissingIngredients.Count == 0))
            return matches;

        var dictionary = await translations.GetAllAsync(cancellationToken);
        return matches.Select(match => match with
        {
            MissingIngredients = match.MissingIngredients
                .Select(ingredient => ingredient with { Name = ResolveName(ingredient, languageCode, dictionary) })
                .ToArray()
        }).ToArray();
    }

    private static string ResolveName(
        PantryRecipeMatchIngredientDto ingredient, string languageCode, IReadOnlyDictionary<string, string> dictionary) =>
        dictionary.TryGetValue(TranslationKeys.Ingredient(ingredient.IngredientId, languageCode), out var translated)
        && !string.IsNullOrWhiteSpace(translated) ? translated : ingredient.Name;
}
