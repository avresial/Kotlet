using Kotlet.Application.Settings;
using Kotlet.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Persistence;

public sealed class SystemSettingsStore(KotletDbContext dbContext) : ISystemSettingsStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        dbContext.SystemSettings
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await dbContext.SystemSettings.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (setting is null)
            dbContext.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        else
            setting.Value = value;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
