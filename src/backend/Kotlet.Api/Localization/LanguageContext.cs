using Kotlet.Application.Translations;

namespace Kotlet.Api.Localization;

/// <summary>
/// Resolves the language the current request should be served in, based on the
/// <c>Accept-Language</c> header sent by the client. Falls back to the default language
/// when the header is missing or unsupported.
/// </summary>
public interface ILanguageContext
{
    string Language { get; }
}

public sealed class LanguageContext(IHttpContextAccessor accessor) : ILanguageContext
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase) { "en", "pl" };

    public string Language
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
                return TranslationKeys.DefaultLanguage;

            foreach (var entry in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var code = entry.Split(';', 2)[0].Trim();
                if (code.Length > 2)
                    code = code[..2];
                if (Supported.Contains(code))
                    return code.ToLowerInvariant();
            }

            return TranslationKeys.DefaultLanguage;
        }
    }
}
