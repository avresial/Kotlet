using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

public interface IMealPlanRepository
{
    Task<IReadOnlyList<MealPlanItem>> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);
    Task<MealPlanItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(Guid houseId, CancellationToken cancellationToken);
    void Add(MealPlanItem item);
    void Remove(MealPlanItem item);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
