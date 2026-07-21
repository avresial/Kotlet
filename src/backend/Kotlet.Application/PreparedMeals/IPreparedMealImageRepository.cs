using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Application.PreparedMeals;

public interface IPreparedMealImageRepository
{
    Task<bool> MealExistsAsync(Guid mealId, Guid houseId, CancellationToken ct);
    Task<IReadOnlyList<PreparedMealImage>> ListAsync(Guid mealId, CancellationToken ct);
    Task<PreparedMealImage?> GetAsync(Guid mealId, Guid imageId, CancellationToken ct);
    void Add(PreparedMealImage image);
    Task UpdateSortOrdersAsync(Guid mealId, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
