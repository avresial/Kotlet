using Kotlet.Domain.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Translations;

/// <summary>
/// Evicts the cached translation dictionary at the actual <see cref="DbContext"/> commit boundary,
/// so any successful save that touches <see cref="Translation"/> entities — regardless of which
/// repository staged the change — keeps the cache consistent. Registered per scope so the
/// "changes pending" flag is isolated to a single unit of work.
/// </summary>
internal sealed class TranslationCacheInterceptor(IMemoryCache cache) : SaveChangesInterceptor
{
    private bool _translationsChanged;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Detect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Detect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Evict();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Evict();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Detect(DbContext? context)
    {
        if (context is null)
            return;
        _translationsChanged |= context.ChangeTracker.Entries<Translation>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    private void Evict()
    {
        if (!_translationsChanged)
            return;
        cache.Remove(TranslationCache.Key);
        _translationsChanged = false;
    }
}
