using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.MealPlanner;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeRepository(KotletDbContext dbContext) : IRecipeRepository
{
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
        Guid houseId, int page, int pageSize, string? search, MealSlot? mealType,
        IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken)
    {
        var query = dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Where(r => r.HouseId == houseId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.Title.ToLower().Contains(term));
        }
        if (mealType is not null) query = query.Where(r => r.MealType == mealType);
        var requiredIngredientIds = ingredientIds?.Distinct().ToArray() ?? [];
        if (requiredIngredientIds.Length > 0)
        {
            query = query.Where(r => r.Ingredients
                .Where(i => requiredIngredientIds.Contains(i.IngredientId))
                .Select(i => i.IngredientId)
                .Distinct()
                .Count() == requiredIngredientIds.Length);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Recipe>> GetRecentAsync(
        Guid houseId, int limit, CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Where(r => r.HouseId == houseId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.HouseId == houseId)
            .ToListAsync(cancellationToken);

    public Task<Recipe?> GetByIdAsync(Guid id, Guid houseId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked
            ? dbContext.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            : dbContext.Recipes.AsNoTracking().Include(r => r.Ingredients).ThenInclude(i => i.Ingredient);
        return query.SingleOrDefaultAsync(
            r => r.Id == id && r.HouseId == houseId,
            cancellationToken);
    }

    public Task<bool> SlugExistsAsync(Guid houseId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
        dbContext.Recipes.AnyAsync(
            r => r.HouseId == houseId
                && r.Slug == slug && r.Id != excludedId,
            cancellationToken);

    public void Add(Recipe recipe) => dbContext.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => dbContext.Recipes.Remove(recipe);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
