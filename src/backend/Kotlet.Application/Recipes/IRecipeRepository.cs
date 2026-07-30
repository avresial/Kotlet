using Kotlet.Domain.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.Recipes;

public interface IRecipeRepository
{
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
        Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType,
        IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> GetRecentAsync(
        Guid ownerUserId, int limit, CancellationToken cancellationToken);
    Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken);
    async Task<IReadOnlyDictionary<Guid, Recipe>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, Guid houseId, CancellationToken cancellationToken)
    {
        var recipes = new Dictionary<Guid, Recipe>();
        foreach (var id in ids.Distinct())
        {
            if (await GetByIdAsync(id, houseId, tracked: false, cancellationToken) is { } recipe)
                recipes[id] = recipe;
        }
        return recipes;
    }
    Task<Recipe?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Lightweight recipe list (no ingredients/images) for duplicate detection.</summary>
    Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(Guid ownerUserId, CancellationToken cancellationToken);
    /// <summary>All recipes of the house with their ingredients, newest first, for pantry matching.</summary>
    Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(Guid houseId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken);
    void Add(Recipe recipe);
    void Remove(Recipe recipe);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
