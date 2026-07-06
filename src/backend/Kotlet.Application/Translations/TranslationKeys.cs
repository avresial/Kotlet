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

    /// <summary>
    /// Every language the app serves content in, including <see cref="DefaultLanguage"/>. This is the
    /// single source of truth so request negotiation and background translation agree on the set.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["en", "pl"];

    /// <summary>
    /// The languages that need a translation dictionary entry: every supported language except the
    /// default one (whose value is stored directly on the entity). These are the targets the
    /// ingredient-translation worker fills in.
    /// </summary>
    public static readonly IReadOnlyList<string> TranslatedLanguages =
        SupportedLanguages.Where(language => !IsDefaultLanguage(language)).ToArray();

    public static string Ingredient(Guid ingredientId, string languageCode) =>
        $"Ingredients_{ingredientId}_{languageCode}";

    public static string IngredientPrefix(Guid ingredientId) =>
        $"Ingredients_{ingredientId}_";

    public static bool IsDefaultLanguage(string languageCode) =>
        string.Equals(languageCode, DefaultLanguage, StringComparison.OrdinalIgnoreCase);
}
