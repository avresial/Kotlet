using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeRepository(KotletDbContext dbContext) : IRecipeRepository
{
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
        Guid ownerUserId, int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => r.OwnerUserId == ownerUserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.Title.ToLower().Contains(term));
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
        Guid ownerUserId, int limit, CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked
            ? dbContext.Recipes.Include(r => r.Ingredients)
            : dbContext.Recipes.AsNoTracking().Include(r => r.Ingredients);
        return query.SingleOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
        dbContext.Recipes.AnyAsync(
            r => r.OwnerUserId == ownerUserId && r.Slug == slug && r.Id != excludedId,
            cancellationToken);

    public void Add(Recipe recipe) => dbContext.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => dbContext.Recipes.Remove(recipe);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
