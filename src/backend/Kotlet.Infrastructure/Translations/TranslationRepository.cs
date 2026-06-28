using Kotlet.Application.Translations;
using Kotlet.Domain.Translations;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Translations;

/// <summary>
/// Stores the translation dictionary and keeps the whole table cached as a single key/value map.
/// Because keys are flat strings, the entire dictionary can be cached and looked up in memory.
/// </summary>
internal sealed class TranslationRepository(KotletDbContext dbContext, IMemoryCache cache) : ITranslationRepository
{
    private const string CacheKey = "translations:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private bool _cacheInvalidationPending;

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        var dictionary = await dbContext.Translations
            .AsNoTracking()
            .ToDictionaryAsync(translation => translation.Key, translation => translation.Value, cancellationToken);

        cache.Set(CacheKey, (IReadOnlyDictionary<string, string>)dictionary, CacheDuration);
        return dictionary;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Translations.FirstOrDefaultAsync(translation => translation.Key == key, cancellationToken);
        if (existing is null)
            dbContext.Translations.Add(new Translation { Key = key, Value = value });
        else
            existing.Value = value;
        _cacheInvalidationPending = true;
    }

    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
    {
        var matches = await dbContext.Translations
            .Where(translation => translation.Key.StartsWith(keyPrefix))
            .ToListAsync(cancellationToken);
        if (matches.Count == 0)
            return;
        dbContext.Translations.RemoveRange(matches);
        _cacheInvalidationPending = true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!_cacheInvalidationPending)
            return;
        cache.Remove(CacheKey);
        _cacheInvalidationPending = false;
    }
}
