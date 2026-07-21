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
            .Where(m => m.Date == date && m.HouseId == houseId)
            .OrderBy(m => m.Slot)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<MealPlanItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.MealPlanItems
            .Include(m => m.Participants)
            .Include(m => m.AddonItems)
            .FirstOrDefaultAsync(
                m => m.Id == id && m.HouseId == houseId,
                cancellationToken);

    public async Task<IReadOnlyList<MealPlanItem>> GetByDateRangeAsync(
        Guid houseId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.MealPlanItems
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.Date >= from && m.Date <= to &&
                m.HouseId == houseId)
            .OrderBy(m => m.Date)
            .ThenBy(m => m.Slot)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.HouseMemberships
            .AsNoTracking()
            .Where(membership => membership.HouseId == houseId)
            .Select(membership => membership.User)
            .OrderBy(u => u.DisplayName ?? u.Email)
            .Select(u => new MealHouseMember(u.Id, u.DisplayName ?? u.Email))
            .ToListAsync(cancellationToken);

    public void Add(MealPlanItem item) => dbContext.MealPlanItems.Add(item);
    public void Remove(MealPlanItem item) => dbContext.MealPlanItems.Remove(item);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
