namespace Kotlet.Application.Settings;

public interface ISystemSettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}
