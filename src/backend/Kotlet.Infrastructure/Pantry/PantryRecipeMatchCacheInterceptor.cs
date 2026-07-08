using Kotlet.Domain.Pantry;
using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Pantry;

/// <summary>
/// Evicts a house's cached recipe suggestions at the actual <see cref="DbContext"/> commit
/// boundary. Suggestions depend on the house's pantry contents and its recipe collection, so
/// any successful save touching <see cref="PantryItem"/> or <see cref="Recipe"/> entities
/// invalidates that house's entry (recipe ingredient edits always mark their recipe modified).
/// Registered per scope so the pending house ids are isolated to a single unit of work.
/// </summary>
internal sealed class PantryRecipeMatchCacheInterceptor(IMemoryCache cache) : SaveChangesInterceptor
{
    private readonly HashSet<Guid> _changedHouseIds = [];

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
        foreach (var entry in context.ChangeTracker.Entries<PantryItem>())
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                _changedHouseIds.Add(entry.Entity.HouseId);
        foreach (var entry in context.ChangeTracker.Entries<Recipe>())
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                _changedHouseIds.Add(entry.Entity.HouseId);
    }

    private void Evict()
    {
        foreach (var houseId in _changedHouseIds)
            cache.Remove(PantryRecipeMatchCache.Key(houseId));
        _changedHouseIds.Clear();
    }
}
