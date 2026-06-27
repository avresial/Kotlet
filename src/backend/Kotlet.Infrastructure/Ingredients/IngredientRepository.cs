using Kotlet.Application.Ingredients;
using Kotlet.Domain.Ingredients;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Ingredients;

internal sealed class IngredientRepository(KotletDbContext dbContext) : IIngredientRepository
{
    public async Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Ingredients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.Ingredients : dbContext.Ingredients.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludedId, CancellationToken cancellationToken) =>
        dbContext.Ingredients.AnyAsync(
            x => x.Name.ToLower() == name.ToLower() && x.Id != excludedId,
            cancellationToken);

    public void Add(Ingredient ingredient) => dbContext.Ingredients.Add(ingredient);
    public void Remove(Ingredient ingredient) => dbContext.Ingredients.Remove(ingredient);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
