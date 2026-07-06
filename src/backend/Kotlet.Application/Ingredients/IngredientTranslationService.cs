using System.Globalization;
using Kotlet.Application.Ai;
using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kotlet.Application.Ingredients;

/// <summary>
/// Outcome of a translation pass. <see cref="ProviderConfigured"/> is <see langword="false"/> when no
/// application AI credentials are set, in which case the pass is a no-op.
/// </summary>
public sealed record IngredientTranslationResult(
    bool ProviderConfigured, int Written, int Failed)
{
    public static readonly IngredientTranslationResult NotConfigured = new(false, 0, 0);
}

/// <summary>
/// Fills in missing ingredient-name translations using the application-level AI credentials. An
/// ingredient is "missing" a translation for a language when no dictionary entry exists for its
/// <see cref="TranslationKeys.Ingredient(Guid, string)"/> key. The canonical (default-language) name
/// stored on the ingredient is the source text; the default language itself is never translated.
/// </summary>
public sealed class IngredientTranslationService(
    IIngredientRepository ingredients,
    ITranslationRepository translations,
    IApplicationChatClientResolver clientResolver,
    ILogger<IngredientTranslationService> logger)
{
    // Ingredients created in a non-default language keep this placeholder canonical name until an
    // English name is supplied (see IngredientService). There is nothing meaningful to translate from,
    // so they are skipped rather than translating the literal word "Unknown".
    private const string UnknownName = "Unknown";

    public async Task<IngredientTranslationResult> BackfillMissingTranslationsAsync(CancellationToken cancellationToken)
    {
        using var client = clientResolver.Resolve();
        if (client is null)
            return IngredientTranslationResult.NotConfigured;

        var allIngredients = await ingredients.GetAllAsync(cancellationToken);
        var dictionary = await translations.GetAllAsync(cancellationToken);

        var written = 0;
        var failed = 0;
        foreach (var ingredient in allIngredients)
        {
            if (string.IsNullOrWhiteSpace(ingredient.Name) ||
                string.Equals(ingredient.Name, UnknownName, StringComparison.Ordinal))
                continue;

            foreach (var language in TranslationKeys.TranslatedLanguages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = TranslationKeys.Ingredient(ingredient.Id, language);
                if (dictionary.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
                    continue;

                var translation = await TranslateAsync(client, ingredient.Name, language, cancellationToken);
                if (translation is null)
                {
                    failed++;
                    continue;
                }

                translation = char.ToUpper(translation[0], CultureInfo.InvariantCulture) + translation.Substring(1);
               
                logger.LogInformation(
                    "Translated ingredient {IngredientId} ({Source}) to {Language}: {Translation}",
                    ingredient.Id, ingredient.Name, language, translation);

                await translations.SetAsync(key, translation, cancellationToken);
                written++;

                if(written%10 == 0) await translations.SaveChangesAsync(cancellationToken);   
            }
        }

        // A single commit for the whole pass keeps the write cheap and lets the translation-cache
        // interceptor evict once rather than per key.
        if (written > 0)
            await translations.SaveChangesAsync(cancellationToken);

        return new IngredientTranslationResult(ProviderConfigured: true, written, failed);
    }

    private static async Task<string?> TranslateAsync(
        IChatClient client, string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AiTranslationPrompts.SystemPrompt),
            new(ChatRole.User, AiTranslationPrompts.BuildUserMessage(text, targetLanguage))
        };

        try
        {
            var response = await client.GetResponseAsync(
                messages, new ChatOptions { Temperature = 0f }, cancellationToken);
            var translation = response.Text?.Trim();
            return string.IsNullOrEmpty(translation) ? null : translation;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One ingredient failing (rate limit, transient provider error) must not abort the whole
            // pass. Count it as failed; the next pass retries because no entry was written.
            return null;
        }
    }
}
