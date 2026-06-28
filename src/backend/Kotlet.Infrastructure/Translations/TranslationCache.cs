namespace Kotlet.Infrastructure.Translations;

/// <summary>Shared constants for the cached translation dictionary.</summary>
internal static class TranslationCache
{
    /// <summary>Cache entry holding the whole dictionary as a single key/value map.</summary>
    public const string Key = "translations:all";

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);
}
