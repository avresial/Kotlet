using Kotlet.Application.MealPlanner;
using Kotlet.Domain.MealPlanner;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.MealPlanner;

internal sealed class MealPlanRepository(KotletDbContext dbContext) : IMealPlanRepository
{
    public async Task<IReadOnlyList<MealPlanItem>> GetByDateAsync(
        Guid houseId, DateOnly date, CancellationToken cancellationToken) =>
        await dbContext.MealPlanItems
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.Date == date && dbContext.Users.Any(u => u.Id == m.UserId && u.HouseId == houseId))
            .OrderBy(m => m.Slot)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<MealPlanItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.MealPlanItems
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(
                m => m.Id == id && dbContext.Users.Any(u => u.Id == m.UserId && u.HouseId == houseId),
                cancellationToken);

    public async Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(u => u.HouseId == houseId)
            .OrderBy(u => u.DisplayName ?? u.Email)
            .Select(u => new MealHouseMember(u.Id, u.DisplayName ?? u.Email))
            .ToListAsync(cancellationToken);

    public void Add(MealPlanItem item) => dbContext.MealPlanItems.Add(item);
    public void Remove(MealPlanItem item) => dbContext.MealPlanItems.Remove(item);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
