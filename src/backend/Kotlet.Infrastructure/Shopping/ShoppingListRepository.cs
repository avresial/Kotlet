using Kotlet.Application.Shopping;
using Kotlet.Domain.Shopping;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Shopping;

internal sealed class ShoppingListRepository(KotletDbContext dbContext) : IShoppingListRepository
{
    public async Task<IReadOnlyCollection<ShoppingListItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.ShoppingListItems.Include(x => x.Ingredient).Where(x => x.HouseId == houseId)
            .OrderBy(x => x.IsPurchased).ThenBy(x => x.Ingredient.Name).ToListAsync(cancellationToken);
    public Task<ShoppingListItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.ShoppingListItems.Include(x => x.Ingredient).SingleOrDefaultAsync(x => x.Id == id && x.HouseId == houseId, cancellationToken);
    public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken) => dbContext.Ingredients.AnyAsync(x => x.Id == ingredientId, cancellationToken);
    public Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken) => dbContext.ShoppingListItems.AnyAsync(x => x.HouseId == houseId && x.IngredientId == ingredientId, cancellationToken);
    public async Task<IReadOnlyList<PlannedIngredient>> GetPlannedIngredientsAsync(
        Guid houseId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.MealPlanItems.AsNoTracking()
            .Where(meal => meal.HouseId == houseId && meal.Date >= from && meal.Date <= to && meal.RecipeId != null)
            .SelectMany(meal => dbContext.RecipeIngredients
                .Where(recipeIngredient => recipeIngredient.RecipeId == meal.RecipeId)
                .Select(recipeIngredient => new
                {
                    recipeIngredient.IngredientId,
                    Quantity = recipeIngredient.NormalizedQuantity.Amount
                        * (meal.Servings ?? meal.Participants.Count + meal.Guests)
                        / recipeIngredient.Recipe.Servings.Value
                }))
            .GroupBy(item => item.IngredientId)
            .Select(group => new PlannedIngredient(group.Key, group.Sum(item => item.Quantity)))
            .ToListAsync(cancellationToken);
    public void Add(ShoppingListItem item) => dbContext.ShoppingListItems.Add(item);
    public void Remove(ShoppingListItem item) => dbContext.ShoppingListItems.Remove(item);
    public Task<int> RemovePurchasedAsync(Guid houseId, CancellationToken cancellationToken) =>
        dbContext.ShoppingListItems.Where(x => x.HouseId == houseId && x.IsPurchased).ExecuteDeleteAsync(cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
