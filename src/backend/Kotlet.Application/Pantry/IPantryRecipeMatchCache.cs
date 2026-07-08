namespace Kotlet.Application.Pantry;

/// <summary>
/// Per-house cache for pantry recipe suggestions. Entries hold language-neutral matches
/// (ingredient names are localized on read) and are invalidated by the infrastructure
/// whenever the house's pantry contents or recipe collection change.
/// </summary>
public interface IPantryRecipeMatchCache
{
    bool TryGet(Guid houseId, out IReadOnlyList<PantryRecipeMatchDto>? matches);
    void Set(Guid houseId, IReadOnlyList<PantryRecipeMatchDto> matches);
}
