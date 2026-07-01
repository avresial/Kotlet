using Kotlet.Application.FoodSettings;
using Kotlet.Domain.FoodSettings;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.FoodSettings;

internal sealed class UserFoodSettingsRepository(KotletDbContext db) : IUserFoodSettingsRepository
{
    public Task<UserFoodSettings?> GetAsync(Guid userId, bool tracked, CancellationToken ct)
    {
        var query = db.UserFoodSettings.Include(x => x.ExcludedIngredients).AsQueryable();
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(x => x.UserId == userId, ct);
    }

    public Task<Guid[]> ExistingIngredientIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        db.Ingredients.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);

    public void Add(UserFoodSettings settings) => db.UserFoodSettings.Add(settings);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
