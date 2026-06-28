using Kotlet.Application.Ingredients;
using Kotlet.Domain.Ingredients;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Ingredients;

internal sealed class IngredientRepository(KotletDbContext dbContext) : IIngredientRepository
{
    public async Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Ingredients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Ingredients.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

    public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.Ingredients : dbContext.Ingredients.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.RecipeIngredients.AnyAsync(x => x.IngredientId == id, cancellationToken)
        || await dbContext.PantryItems.AnyAsync(x => x.IngredientId == id, cancellationToken)
        || await dbContext.ShoppingListItems.AnyAsync(x => x.IngredientId == id, cancellationToken);

    public void Add(Ingredient ingredient) => dbContext.Ingredients.Add(ingredient);
    public void Remove(Ingredient ingredient) => dbContext.Ingredients.Remove(ingredient);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
