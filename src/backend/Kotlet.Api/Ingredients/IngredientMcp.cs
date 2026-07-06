using System.ComponentModel;
using Kotlet.Api.Localization;
using Kotlet.Api.Mcp;
using Kotlet.Application.Ingredients;
using Kotlet.Domain.Ingredients;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Ingredients;

/// <summary>MCP tools and resources for the shared ingredient catalog.</summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class IngredientMcp
{
    [McpServerTool(Name = "get_ingredients", ReadOnly = true, OpenWorld = false),
     Description("Finds ingredients and returns links to their complete MCP resources.")]
    public static async Task<IReadOnlyList<ResourceLinkBlock>> GetIngredients(
        IngredientService service,
        ILanguageContext language,
        [Description("Optional text to search for in ingredient names.")] string? search = null,
        CancellationToken cancellationToken = default) =>
        (await service.GetAllAsync(language.Language, cancellationToken))
        .Where(ingredient => string.IsNullOrWhiteSpace(search)
            || ingredient.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
        .Select(ingredient => Link(
            $"kotlet://ingredients/{ingredient.Id}", ingredient.Name,
            $"Ingredient measured in {ingredient.MeasurementUnit}."))
        .ToList();

    [McpServerTool(Name = "get_ingredient", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one ingredient with complete details: measurement unit, calories, price, category, allergens, attributes, and dietary suitability.")]
    public static async Task<McpIngredient> GetIngredient(
        [Description("Ingredient ID from get_ingredients.")] Guid ingredientId,
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        McpIngredient.From(await service.GetByIdAsync(ingredientId, language.Language, cancellationToken)
                           ?? throw new McpException("Ingredient not found."));

    [McpServerTool(Name = "create_ingredient", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates a new ingredient in the Kotlet catalog. The catalog is shared by every household, so ALWAYS search get_ingredients first and only create an ingredient when no existing one matches. Typical use: importing a recipe that needs an ingredient Kotlet does not know yet.")]
    public static async Task<McpIngredientOperationResult> CreateIngredient(
        [Description("Complete ingredient to create. Category, allergens, attributes, and suitability use the documented names, not numbers.")]
        CreateIngredientMcpRequest request,
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!McpEnum.TryParse(request.Category, FoodCategory.Unknown, out var category))
            errors["category"] = [$"Unknown category '{request.Category}'. Use one of: {string.Join(", ", Enum.GetNames<FoodCategory>())}."];
        if (!McpEnum.TryParseFlags<Allergen>(request.Allergens, out var allergens, out var badAllergens))
            errors["allergens"] = [$"Unknown allergen(s): {string.Join(", ", badAllergens)}. Use: {string.Join(", ", Enum.GetNames<Allergen>().Where(n => n != nameof(Allergen.None)))}."];
        if (!McpEnum.TryParseFlags<FoodAttribute>(request.Attributes, out var attributes, out var badAttributes))
            errors["attributes"] = [$"Unknown attribute(s): {string.Join(", ", badAttributes)}. Use: {string.Join(", ", Enum.GetNames<FoodAttribute>().Where(n => n != nameof(FoodAttribute.None)))}."];
        if (!McpEnum.TryParseFlags<DietarySuitability>(request.Suitability, out var suitability, out var badSuitability))
            errors["suitability"] = [$"Unknown suitability value(s): {string.Join(", ", badSuitability)}. Use: {string.Join(", ", Enum.GetNames<DietarySuitability>().Where(n => n != nameof(DietarySuitability.None)))}."];
        if (errors.Count > 0)
            return new(nameof(IngredientOperationStatus.ValidationFailed), ValidationErrors: errors);

        var command = new SaveIngredientCommand(
            request.Name, request.MeasurementUnit, request.IsCountable, request.MeasurementUnitsPerPiece,
            request.CaloriesPer100BaseUnits, request.PricePer100BaseUnits,
            Category: category, Allergens: allergens, Attributes: attributes, Suitability: suitability);
        return McpIngredientOperationResult.From(
            await service.CreateAsync(command, language.Language, cancellationToken));
    }

    [McpServerTool(Name = "resolve_ingredients_batch", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Resolves many ingredient names to catalog ingredient IDs in one call, replacing repeated get_ingredients/create_ingredient roundtrips when importing a recipe. Exact (case-insensitive, singular/plural-tolerant) matches are preferred; when several catalog ingredients could match, the name is returned as ambiguous instead of guessing — pick the right one and resolve it yourself. With createMissing=true, names with no match at all are created using the optional hints (category defaults to Unknown, calories and price to 0).")]
    public static async Task<McpIngredientBatchResolutionResult> ResolveIngredientsBatch(
        [Description("Ingredient candidates to resolve, at most 100 per call.")]
        IReadOnlyList<McpIngredientCandidate> ingredients,
        IngredientBatchResolutionService service,
        ILanguageContext language,
        [Description("When true, ingredients without any catalog match are created automatically. Ambiguous names are never auto-created.")]
        bool createMissing = false,
        CancellationToken cancellationToken = default)
    {
        if (ingredients is not { Count: > 0 })
            throw new McpException("Provide at least one ingredient candidate.");
        if (ingredients.Count > 100)
            throw new McpException("At most 100 ingredient candidates are allowed per call.");
        return McpIngredientBatchResolutionResult.From(await service.ResolveAsync(
            ingredients
                .Select(candidate => new IngredientResolutionCandidate(
                    candidate.Name, candidate.ExpectedUnit, candidate.CategoryHint,
                    candidate.CaloriesPer100BaseUnits, candidate.MeasurementUnitsPerPiece))
                .ToList(),
            createMissing, language.Language, cancellationToken));
    }

    [McpServerResource(UriTemplate = "kotlet://ingredients/{ingredientId}", Name = "ingredient",
        Title = "Kotlet ingredient", MimeType = "application/json"),
     Description("Complete ingredient details, including measurement, calories, price, and localization.")]
    public static async Task<string> Ingredient(
        Guid ingredientId, IngredientService service, ILanguageContext language, CancellationToken cancellationToken) =>
        Json(await service.GetByIdAsync(ingredientId, language.Language, cancellationToken)
             ?? throw new KeyNotFoundException("Ingredient not found."));
}
