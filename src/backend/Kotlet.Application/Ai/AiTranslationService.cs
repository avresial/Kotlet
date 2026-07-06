using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

public enum AiTranslationStatus
{
    Translated,
    InvalidRequest,
    NotConfigured,
    Failed
}

public sealed record AiTranslationResult(AiTranslationStatus Status, string? Translation = null, string? Message = null);

/// <summary>
/// Translates short text (such as an ingredient name) into a target language using the requesting
/// user's configured AI provider. This is the first in-process AI feature. It uses a single
/// plain-text chat call rather than tools or structured output, which keeps it compatible with any
/// OpenAI-compatible endpoint a user might configure.
/// </summary>
public sealed class AiTranslationService(IUserChatClientResolver resolver)
{
    private const int MaxTextLength = 500;

    private const string SystemPrompt =
        "You are a translation engine for a cooking app. Translate the user's text into the requested " +
        "language. Reply with ONLY the translated text: no quotes, no notes, no explanations. Preserve " +
        "culinary meaning; if the text is already in the target language, return it unchanged.";

    public async Task<AiTranslationResult> TranslateAsync(
        Guid userId, string? text, string? targetLanguage, CancellationToken cancellationToken)
    {
        text = text?.Trim();
        targetLanguage = targetLanguage?.Trim();
        if (string.IsNullOrEmpty(text))
            return new(AiTranslationStatus.InvalidRequest, Message: "Text to translate is required.");
        if (string.IsNullOrEmpty(targetLanguage))
            return new(AiTranslationStatus.InvalidRequest, Message: "Target language is required.");
        if (text.Length > MaxTextLength)
            return new(AiTranslationStatus.InvalidRequest, Message: $"Text cannot exceed {MaxTextLength} characters.");

        using var client = await resolver.ResolveAsync(userId, cancellationToken);
        if (client is null)
            return new(AiTranslationStatus.NotConfigured,
                Message: "No enabled AI provider is configured. Configure one under AI provider settings.");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"Target language: {targetLanguage}\nText: {text}")
        };

        try
        {
            var response = await client.GetResponseAsync(
                messages, new ChatOptions { Temperature = 0f }, cancellationToken);
            var translation = response.Text?.Trim();
            return string.IsNullOrEmpty(translation)
                ? new(AiTranslationStatus.Failed, Message: "The AI provider returned an empty response.")
                : new(AiTranslationStatus.Translated, translation);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The provider call can fail for many reasons the user controls (invalid key, unreachable
            // endpoint, unknown model). Return a generic failure rather than leaking provider internals.
            return new(AiTranslationStatus.Failed, Message: "The AI provider request failed.");
        }
    }
}
