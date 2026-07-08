using Kotlet.Application.Pantry;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Pantry;

internal static class PantryRecipeMatchCache
{
    public static string Key(Guid houseId) => $"pantry-recipe-matches:{houseId}";

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Caches each house's recipe suggestions under its own key. Eviction is handled by
/// <see cref="PantryRecipeMatchCacheInterceptor"/> at the commit boundary; the expiration
/// is only a safety net.
/// </summary>
internal sealed class PantryRecipeMatchMemoryCache(IMemoryCache cache) : IPantryRecipeMatchCache
{
    public bool TryGet(Guid houseId, out IReadOnlyList<PantryRecipeMatchDto>? matches) =>
        cache.TryGetValue(PantryRecipeMatchCache.Key(houseId), out matches) && matches is not null;

    public void Set(Guid houseId, IReadOnlyList<PantryRecipeMatchDto> matches) =>
        cache.Set(PantryRecipeMatchCache.Key(houseId), matches, PantryRecipeMatchCache.Duration);
}
