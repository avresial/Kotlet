using System.ComponentModel;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Pantry;
using Kotlet.Application.Shopping;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Api.Mcp;

/// <summary>
/// Agent-facing MCP shapes. The REST DTOs serialize category/allergen/attribute enums as
/// numeric bitmasks, which AI agents cannot interpret; these records expose the same data
/// as readable name lists instead.
/// </summary>
public sealed record McpIngredient(
    Guid Id,
    string Name,
    string DefaultName,
    string MeasurementUnit,
    bool IsCountable,
    decimal? MeasurementUnitsPerPiece,
    decimal CaloriesPer100BaseUnits,
    decimal PricePer100BaseUnits,
    string Category,
    string[] Allergens,
    string[] Attributes,
    string[] Suitability)
{
    public static McpIngredient From(IngredientDto dto) => new(
        dto.Id, dto.Name, dto.DefaultName, dto.MeasurementUnit, dto.IsCountable,
        dto.MeasurementUnitsPerPiece, dto.CaloriesPer100BaseUnits, dto.PricePer100BaseUnits,
        dto.Category.ToString(), McpEnum.Names(dto.Allergens), McpEnum.Names(dto.Attributes),
        McpEnum.Names(dto.Suitability));
}

public sealed record McpIngredientOperationResult(
    string Status,
    McpIngredient? Ingredient = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null)
{
    public static McpIngredientOperationResult From(IngredientOperationResult result) => new(
        result.Status.ToString(),
        result.Ingredient is null ? null : McpIngredient.From(result.Ingredient),
        result.ValidationErrors, result.Message);
}

public sealed record CreateIngredientMcpRequest(
    [property: Description("Ingredient name in English, at most 150 characters.")]
    string Name,
    [property: Description("Base measurement unit: \"g\" for solids or \"ml\" for liquids.")]
    string MeasurementUnit,
    [property: Description("Kilocalories per 100 base units (per 100 g or 100 ml). Use 0 when unknown.")]
    decimal CaloriesPer100BaseUnits,
    [property: Description("True when the ingredient is naturally counted in pieces (eggs, apples).")]
    bool IsCountable = false,
    [property: Description("Grams or millilitres in one piece. Required when isCountable is true.")]
    decimal? MeasurementUnitsPerPiece = null,
    [property: Description("Price per 100 base units in the household currency. Use 0 when unknown.")]
    decimal PricePer100BaseUnits = 0,
    [property: Description("Food category. One of: Unknown, Meat, Poultry, Fish, Shellfish, Egg, Dairy, Cheese, Vegetable, Fruit, Legume, Grain, Nut, Seed, Mushroom, Herb, Spice, Oil, Sweetener, Condiment, Sauce, Beverage, Composite, Additive.")]
    string Category = "Unknown",
    [property: Description("Allergens present, from: Gluten, Crustaceans, Eggs, Fish, Peanuts, Soybeans, Milk, TreeNuts, Celery, Mustard, Sesame, Sulphites, Lupin, Molluscs.")]
    string[]? Allergens = null,
    [property: Description("Food attributes, from: AnimalOrigin, PlantOrigin, ContainsLactose, ContainsAlcohol, ContainsCaffeine, HighHistamine, HighFodmap, Fermented, Smoked, Spicy, Processed, UltraProcessed.")]
    string[]? Attributes = null,
    [property: Description("Diets the ingredient suits, from: Vegan, Vegetarian, Pescatarian, GlutenFree, LactoseFree, LowFodmap, LowHistamine, Keto, LowCarb.")]
    string[]? Suitability = null);

public sealed record McpIngredientCandidate(
    [property: Description("Ingredient name to resolve, e.g. \"chickpeas\".")]
    string Name,
    [property: Description("Optional unit hint: \"g\", \"ml\", or \"pcs\". Used only when creating a missing ingredient.")]
    string? ExpectedUnit = null,
    [property: Description("Optional category hint using the documented Kotlet category names (e.g. Legume). Unknown hints fall back to Unknown.")]
    string? CategoryHint = null,
    [property: Description("Kilocalories per 100 base units. Used only when creating a missing ingredient; defaults to 0.")]
    decimal? CaloriesPer100BaseUnits = null,
    [property: Description("Grams or millilitres in one piece. Used only when creating a missing \"pcs\" ingredient; defaults to 100.")]
    decimal? MeasurementUnitsPerPiece = null);

public sealed record McpResolvedIngredient(
    [property: Description("The name you passed in, echoed back so you can line results up with your input.")]
    string InputName,
    [property: Description("Catalog ingredient ID. Use this directly as add_recipe's ingredientId.")]
    Guid IngredientId,
    [property: Description("Canonical catalog name of the matched ingredient.")]
    string MatchedName,
    [property: Description("Base unit this ingredient is measured in (\"g\", \"ml\", or pieces). Use it to express the quantity in add_recipe.")]
    string MeasurementUnit,
    [property: Description("\"existing\" if it was already in the catalog, \"created\" if this call added it (only when createMissing=true).")]
    string Status);

public sealed record McpIngredientNameMatch(Guid IngredientId, string Name);

public sealed record McpAmbiguousIngredient(
    [property: Description("The name you passed in that matched more than one catalog ingredient.")]
    string InputName,
    [property: Description("Candidate ingredients. Pick the right one yourself; nothing was resolved or created for this name.")]
    IReadOnlyList<McpIngredientNameMatch> Matches);

public sealed record McpMissingIngredient(
    [property: Description("The name you passed in that has no catalog match.")]
    string InputName,
    [property: Description("Why it is unresolved. When createMissing=false this means the ingredient is new; surface it to the user before adding it.")]
    string Reason);

public sealed record McpIngredientBatchResolutionResult(
    [property: Description("Names matched to exactly one catalog ingredient, ready to drop into add_recipe.")]
    IReadOnlyList<McpResolvedIngredient> Resolved,
    [property: Description("Names that matched several ingredients. Choose one per entry before adding the recipe.")]
    IReadOnlyList<McpAmbiguousIngredient> Ambiguous,
    [property: Description("Names not in the catalog. If non-empty, list them for the user and ask whether to add them before proceeding.")]
    IReadOnlyList<McpMissingIngredient> Missing)
{
    public static McpIngredientBatchResolutionResult From(IngredientBatchResolutionResult result) => new(
        result.Resolved
            .Select(entry => new McpResolvedIngredient(
                entry.InputName, entry.IngredientId, entry.MatchedName, entry.MeasurementUnit,
                entry.Status == IngredientResolutionStatus.Created ? "created" : "existing"))
            .ToList(),
        result.Ambiguous
            .Select(entry => new McpAmbiguousIngredient(
                entry.InputName,
                entry.Matches.Select(match => new McpIngredientNameMatch(match.IngredientId, match.Name)).ToList()))
            .ToList(),
        result.Missing
            .Select(entry => new McpMissingIngredient(entry.InputName, entry.Reason))
            .ToList());
}

public sealed record McpRecipeMatch(Guid RecipeId, string Title, string? SourceUrl, string MatchType);

public sealed record McpRecipeExistenceResult(bool Exists, IReadOnlyList<McpRecipeMatch> Matches)
{
    public static McpRecipeExistenceResult From(Kotlet.Application.Recipes.RecipeExistenceResult result) => new(
        result.Exists,
        result.Matches
            .Select(match => new McpRecipeMatch(match.RecipeId, match.Title, match.SourceUrl, match.MatchType switch
            {
                Kotlet.Application.Recipes.RecipeMatchType.SourceUrl => "sourceUrl",
                Kotlet.Application.Recipes.RecipeMatchType.ExactTitle => "exactTitle",
                _ => "similarTitle"
            }))
            .ToList());
}

public sealed record McpShoppingListItem(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string MeasurementUnit,
    decimal Quantity,
    decimal TotalPrice,
    bool IsPurchased,
    string Category)
{
    public static McpShoppingListItem From(ShoppingListItemDto dto) => new(
        dto.Id, dto.IngredientId, dto.IngredientName, dto.MeasurementUnit,
        dto.Quantity, dto.TotalPrice, dto.IsPurchased, dto.Category.ToString());
}

public sealed record McpPantryItem(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string MeasurementUnit,
    decimal Quantity,
    DateOnly? ExpirationDate,
    string? StorageLocation)
{
    public static McpPantryItem From(PantryItemDto dto) => new(
        dto.Id, dto.IngredientId, dto.IngredientName, dto.MeasurementUnit,
        dto.Quantity, dto.ExpirationDate, dto.StorageLocation?.ToString());
}

public sealed record McpPantryOperationResult(
    string Status,
    McpPantryItem? Item = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null)
{
    public static McpPantryOperationResult From(PantryOperationResult result) => new(
        result.Status.ToString(),
        result.Item is null ? null : McpPantryItem.From(result.Item),
        result.ValidationErrors, result.Message);
}

internal static class McpEnum
{
    public static string[] Names<T>(T flags) where T : struct, Enum =>
        Enum.GetValues<T>()
            .Where(value => Convert.ToInt64(value) != 0 && flags.HasFlag(value))
            .Select(value => value.ToString())
            .ToArray();

    public static bool TryParseFlags<T>(IReadOnlyList<string>? names, out T result, out string[] invalid)
        where T : struct, Enum
    {
        long combined = 0;
        var bad = new List<string>();
        foreach (var name in names ?? [])
        {
            if (Enum.TryParse<T>(name, ignoreCase: true, out var value) && Enum.IsDefined(value))
                combined |= Convert.ToInt64(value);
            else
                bad.Add(name);
        }
        result = (T)Enum.ToObject(typeof(T), combined);
        invalid = [.. bad];
        return bad.Count == 0;
    }

    public static bool TryParse<T>(string? name, T fallback, out T result) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            result = fallback;
            return true;
        }
        // Reject numeric strings so agents must use the documented names.
        if (!long.TryParse(name, out _)
            && Enum.TryParse<T>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            result = parsed;
            return true;
        }
        result = fallback;
        return false;
    }
}
