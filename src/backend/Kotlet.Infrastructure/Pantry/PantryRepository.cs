using Kotlet.Application.Pantry;
using Kotlet.Domain.Pantry;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Pantry;

internal sealed class PantryRepository(KotletDbContext dbContext) : IPantryRepository
{
    public async Task<IReadOnlyCollection<PantryItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.PantryItems.AsNoTracking().Include(x => x.Ingredient).Where(x => x.HouseId == houseId)
            .OrderBy(x => x.Quantity).ThenBy(x => x.Ingredient.Name).ToListAsync(cancellationToken);
    public Task<PantryItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.PantryItems.Include(x => x.Ingredient).SingleOrDefaultAsync(x => x.Id == id && x.HouseId == houseId, cancellationToken);
    public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken) =>
        dbContext.Ingredients.AnyAsync(x => x.Id == ingredientId, cancellationToken);
    public Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken) =>
        dbContext.PantryItems.AnyAsync(x => x.HouseId == houseId && x.IngredientId == ingredientId, cancellationToken);
    public void Add(PantryItem item) => dbContext.PantryItems.Add(item);
    public void Remove(PantryItem item) => dbContext.PantryItems.Remove(item);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
