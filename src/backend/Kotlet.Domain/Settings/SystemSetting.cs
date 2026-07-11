namespace Kotlet.Domain.Settings;

public sealed class SystemSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
}
