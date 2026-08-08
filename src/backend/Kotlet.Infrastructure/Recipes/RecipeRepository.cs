using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeRepository(KotletDbContext dbContext) : IRecipeRepository
{
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
        Guid houseId, int page, int pageSize, string? search, MealSlot? mealType,
        IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(houseId, search, mealType, ingredientIds)
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<RecipeSummaryData> Items, int TotalCount)> GetPagedSummariesAsync(
        Guid houseId, int page, int pageSize, string? search, MealSlot? mealType,
        IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(houseId, search, mealType, ingredientIds);
        var total = await query.CountAsync(cancellationToken);
        var items = await SelectSummaries(query
                .OrderByDescending(r => r.UpdatedAtUtc)
                .ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
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

    public async Task<IReadOnlyList<RecipeSummaryData>> GetRecentSummariesAsync(
        Guid houseId, int limit, CancellationToken cancellationToken) =>
        await SelectSummaries(dbContext.Recipes
                .AsNoTracking()
                .Where(r => r.HouseId == houseId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ThenByDescending(r => r.Id)
                .Take(limit))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.HouseId == houseId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Where(r => r.HouseId == houseId)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.Id)
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

    public async Task<IReadOnlyDictionary<Guid, Recipe>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, Guid houseId, CancellationToken cancellationToken)
    {
        var requested = ids.Distinct().ToArray();
        if (requested.Length == 0)
        {
            return new Dictionary<Guid, Recipe>();
        }

        return await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Where(r => r.HouseId == houseId && requested.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
    }

    public Task<Recipe?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(Guid houseId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
        dbContext.Recipes.AnyAsync(
            r => r.HouseId == houseId
                && r.Slug == slug && r.Id != excludedId,
            cancellationToken);

    public void Add(Recipe recipe) => dbContext.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => dbContext.Recipes.Remove(recipe);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<Recipe> BuildFilteredQuery(
        Guid houseId, string? search, MealSlot? mealType, IReadOnlyCollection<Guid>? ingredientIds)
    {
        var query = dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.HouseId == houseId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
                ? query.Where(r => EF.Functions.ILike(r.Title, pattern, "\\"))
                : query.Where(r => EF.Functions.Like(r.Title, pattern, "\\"));
        }

        if (mealType is { } requestedMealType)
        {
            query = query.Where(r => r.MealType == requestedMealType);
        }

        var requiredIngredientIds = ingredientIds?.Distinct().ToArray() ?? [];
        if (requiredIngredientIds.Length > 0)
        {
            query = query.Where(r => r.Ingredients
                .Where(i => requiredIngredientIds.Contains(i.IngredientId))
                .Select(i => i.IngredientId)
                .Distinct()
                .Count() == requiredIngredientIds.Length);
        }

        return query;
    }

    private static IQueryable<RecipeSummaryData> SelectSummaries(IQueryable<Recipe> query) =>
        query.Select(recipe => new RecipeSummaryData(
            recipe.Id,
            recipe.Title,
            recipe.Slug,
            recipe.OwnerUserId,
            recipe.Ingredients.Count(),
            recipe.Servings,
            recipe.MealType,
            recipe.DescriptionMarkdown,
            recipe.IsAiAssisted,
            recipe.CreatedAtUtc,
            recipe.UpdatedAtUtc));

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
