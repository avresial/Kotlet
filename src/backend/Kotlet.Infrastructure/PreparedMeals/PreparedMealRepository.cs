using Kotlet.Application.PreparedMeals;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealRepository(KotletDbContext db) : IPreparedMealRepository
{
    public async Task<IReadOnlyList<PreparedMeal>> ListAsync(Guid houseId, bool includeArchived, CancellationToken ct) =>
        await db.PreparedMeals.AsNoTracking().Include(x => x.Addons).ThenInclude(x => x.Ingredient)
            .Where(x => x.HouseId == houseId && (includeArchived || !x.IsArchived)).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<PreparedMeal?> GetAsync(Guid id, Guid houseId, bool tracked, CancellationToken ct) =>
        (tracked ? db.PreparedMeals.AsQueryable() : db.PreparedMeals.AsNoTracking()).Include(x => x.Addons).ThenInclude(x => x.Ingredient)
            .SingleOrDefaultAsync(x => x.Id == id && x.HouseId == houseId, ct);
    public void Add(PreparedMeal meal) => db.PreparedMeals.Add(meal);
    public void Remove(PreparedMeal meal) => db.PreparedMeals.Remove(meal);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
