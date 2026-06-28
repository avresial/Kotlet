namespace Kotlet.Application.Translations;

/// <summary>
/// Builds the flat keys used in the translation dictionary. Keeping the key shape in one place
/// means the storage layout (and therefore the cache layout) is defined once.
/// </summary>
public static class TranslationKeys
{
    /// <summary>
    /// The canonical language. Values stored directly on an entity (for example
    /// <c>Ingredient.Name</c>) are assumed to be in this language, so no translation row is
    /// created for it and it is used as the fallback when a translation is missing.
    /// </summary>
    public const string DefaultLanguage = "en";

    public static string Ingredient(Guid ingredientId, string languageCode) =>
        $"Ingredients_{ingredientId}_{languageCode}";

    public static string IngredientPrefix(Guid ingredientId) =>
        $"Ingredients_{ingredientId}_";

    public static bool IsDefaultLanguage(string languageCode) =>
        string.Equals(languageCode, DefaultLanguage, StringComparison.OrdinalIgnoreCase);
}
