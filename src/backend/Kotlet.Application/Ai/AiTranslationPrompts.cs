namespace Kotlet.Application.Ai;

/// <summary>
/// The shared prompt shape for single-shot text translation. Both the per-user
/// <see cref="AiTranslationService"/> and the application-level ingredient-translation worker send
/// the same instructions, so the wording lives here once.
/// </summary>
internal static class AiTranslationPrompts
{
    public const string SystemPrompt =
        "You are a translation engine for a cooking app. Translate the user's text into the requested " +
        "language. Reply with ONLY the translated text: no quotes, no notes, no explanations. Preserve " +
        "culinary meaning; if the text is already in the target language, return it unchanged.";

    public static string BuildUserMessage(string text, string targetLanguage) =>
        $"Target language: {targetLanguage}\nText: {text}";
}
