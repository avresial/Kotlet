using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Application.PreparedMeals;

public interface IPreparedMealRepository
{
    Task<IReadOnlyList<PreparedMeal>> ListAsync(Guid houseId, bool includeArchived, CancellationToken cancellationToken);
    Task<PreparedMeal?> GetAsync(Guid id, Guid houseId, bool tracked, CancellationToken cancellationToken);
    void Add(PreparedMeal meal);
    void Remove(PreparedMeal meal);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
