using System.Text.Json;
using Kotlet.Application.Ai;
using Kotlet.Domain.Ingredients;
using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ingredients;

public sealed record IngredientDetailsSuggestion(FoodCategory Category, Allergen Allergens, FoodAttribute Attributes, DietarySuitability Suitability);

public sealed class IngredientDetailsAutofillService(IIngredientRepository repository, IApplicationChatClientResolver clientResolver)
{
    private const int BatchSize = 10;

    public async Task<IngredientDetailsSuggestion?> SuggestAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150) return null;
        using var client = clientResolver.Resolve();
        if (client is null) return null;
        var prompt = $"Classify the food ingredient named \"{name.Trim()}\". Return JSON only with keys category, allergens, attributes, suitability. "
            + $"category: exactly one of {string.Join(", ", Enum.GetNames<FoodCategory>())}. "
            + $"allergens: zero or more of {Names<Allergen>()}. attributes: zero or more of {Names<FoodAttribute>()}. "
            + $"suitability: zero or more of {Names<DietarySuitability>()}. Use arrays for the last three fields and only listed values.";
        try
        {
            var response = await client.GetResponseAsync(
                [new(ChatRole.System, "You classify food ingredients into a fixed taxonomy."), new(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0f }, cancellationToken);
            var value = JsonSerializer.Deserialize<Response>(response.Text ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return value is not null && Enum.TryParse<FoodCategory>(value.Category, true, out var category) && Enum.IsDefined(category)
                && TryFlags(value.Allergens, out Allergen allergens) && TryFlags(value.Attributes, out FoodAttribute attributes)
                && TryFlags(value.Suitability, out DietarySuitability suitability)
                    ? new(category, allergens, attributes, suitability) : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken)
    {
        var written = 0;
        foreach (var candidate in (await repository.GetAllAsync(cancellationToken)).Where(IsEmpty))
        {
            var suggestion = await SuggestAsync(candidate.Name, cancellationToken);
            if (suggestion is null) continue;
            var ingredient = await repository.GetByIdAsync(candidate.Id, tracked: true, cancellationToken);
            if (ingredient is null || !IsEmpty(ingredient)) continue;
            ingredient.Category = suggestion.Category;
            ingredient.Allergens = suggestion.Allergens;
            ingredient.Attributes = suggestion.Attributes;
            ingredient.Suitability = suggestion.Suitability;
            ingredient.IsAiModified = true;
            written++;
            if (written % BatchSize == 0) await repository.SaveChangesAsync(cancellationToken);
        }
        if (written % BatchSize != 0) await repository.SaveChangesAsync(cancellationToken);
        return written;
    }

    private static bool IsEmpty(Ingredient ingredient) => ingredient.Category == FoodCategory.Unknown
        && ingredient.Allergens == Allergen.None && ingredient.Attributes == FoodAttribute.None
        && ingredient.Suitability == DietarySuitability.None;
    private static string Names<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames<T>().Where(name => name != "None"));
    private static bool TryFlags<T>(string[]? names, out T result) where T : struct, Enum
    {
        long flags = 0;
        foreach (var name in names ?? [])
        {
            if (!Enum.TryParse<T>(name, true, out var value) || Convert.ToInt64(value) == 0) { result = default; return false; }
            flags |= Convert.ToInt64(value);
        }
        result = (T)Enum.ToObject(typeof(T), flags);
        return true;
    }
    private sealed record Response(string Category, string[]? Allergens, string[]? Attributes, string[]? Suitability);
}
