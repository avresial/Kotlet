using Kotlet.Application.Translations;
using Kotlet.Domain.Translations;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Translations;

/// <summary>
/// Stores the translation dictionary and keeps the whole table cached as a single key/value map.
/// Because keys are flat strings, the entire dictionary can be cached and looked up in memory.
/// Cache eviction is handled by <see cref="TranslationCacheInterceptor"/> at the commit boundary,
/// so this repository only stages writes and serves cached reads.
/// </summary>
internal sealed class TranslationRepository(KotletDbContext dbContext, IMemoryCache cache) : ITranslationRepository
{
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(TranslationCache.Key, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        var dictionary = await dbContext.Translations
            .AsNoTracking()
            .ToDictionaryAsync(translation => translation.Key, translation => translation.Value, cancellationToken);

        cache.Set(TranslationCache.Key, (IReadOnlyDictionary<string, string>)dictionary, TranslationCache.Duration);
        return dictionary;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Translations.FirstOrDefaultAsync(translation => translation.Key == key, cancellationToken);
        if (existing is null)
            dbContext.Translations.Add(new Translation { Key = key, Value = value });
        else
            existing.Value = value;
    }

    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
    {
        var matches = await dbContext.Translations
            .Where(translation => translation.Key.StartsWith(keyPrefix))
            .ToListAsync(cancellationToken);
        if (matches.Count > 0)
            dbContext.Translations.RemoveRange(matches);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
