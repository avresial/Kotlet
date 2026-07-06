using System.Globalization;
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
    private static readonly HashSet<string> Supported =
        new(TranslationKeys.SupportedLanguages, StringComparer.OrdinalIgnoreCase);

    public string Language
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
                return TranslationKeys.DefaultLanguage;

            string? best = null;
            var bestQuality = double.NegativeInfinity;
            foreach (var entry in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var code = parts[0];
                if (code.Length > 2)
                    code = code[..2];
                if (!Supported.Contains(code))
                    continue;

                var quality = ResolveQuality(parts);
                if (quality > bestQuality)
                {
                    bestQuality = quality;
                    best = code.ToLowerInvariant();
                }
            }

            return best ?? TranslationKeys.DefaultLanguage;
        }
    }

    // Parses the optional ";q=" weight of an Accept-Language entry. Entries explicitly marked
    // unacceptable (q=0) are skipped so a weighted header like "en;q=0, pl" resolves to "pl".
    private static double ResolveQuality(string[] entryParts)
    {
        foreach (var parameter in entryParts.Skip(1))
        {
            if (parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(parameter.AsSpan(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var quality))
                return quality > 0 ? quality : double.NegativeInfinity;
        }

        return 1.0;
    }
}
