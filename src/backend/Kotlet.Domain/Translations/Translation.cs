namespace Kotlet.Domain.Translations;

/// <summary>
/// A single entry in the translation dictionary. Keys are flat strings (for example
/// "Ingredients_{id}_pl") so the whole table can be loaded and cached as a key/value map.
/// </summary>
public sealed class Translation
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
