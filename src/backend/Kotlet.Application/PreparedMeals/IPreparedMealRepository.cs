using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Application.PreparedMeals;

public interface IPreparedMealRepository
{
    Task<IReadOnlyList<PreparedMeal>> ListAsync(
        Guid houseId,
        bool includeArchived,
        CancellationToken cancellationToken);
    Task<PreparedMeal?> GetAsync(Guid id, Guid houseId, bool tracked, CancellationToken cancellationToken);
    async Task<IReadOnlyDictionary<Guid, PreparedMeal>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, Guid houseId, CancellationToken cancellationToken)
    {
        var meals = new Dictionary<Guid, PreparedMeal>();
        foreach (var id in ids.Distinct())
        {
            if (await GetAsync(id, houseId, tracked: false, cancellationToken) is { } meal)
                meals[id] = meal;
        }
        return meals;
    }
    void Add(PreparedMeal meal);
    void Remove(PreparedMeal meal);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
