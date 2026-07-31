using Kotlet.Api.Localization;
using ModelContextProtocol.Protocol;

namespace Kotlet.Api.Mcp;

/// <summary>
/// Passes the negotiated request language to the MCP Apps rendering the tool results.
/// <para>
/// The embedded UIs are static HTML documents, so they cannot be translated on the server the way
/// tool payloads are. Instead every tool result carries the language the request was served in
/// under <c>_meta["kotlet/locale"]</c>, and the UI selects its dictionary from it. The key is only
/// written when the host actually negotiated a language (see
/// <see cref="ILanguageContext.RequestedLanguage"/>) — otherwise the UI is free to fall back to
/// the host's own locale, which is a better guess than the server's default.
/// </para>
/// </summary>
internal static class McpUiLocale
{
    public const string LocaleKey = "kotlet/locale";

    public static void Apply(IServiceProvider? requestServices, CallToolResult result)
    {
        if (requestServices?.GetService<ILanguageContext>()?.RequestedLanguage is not { } language)
            return;

        result.Meta ??= [];
        result.Meta[LocaleKey] = language;
    }
}
