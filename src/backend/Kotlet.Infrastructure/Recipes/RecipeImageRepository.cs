using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImageRepository(KotletDbContext dbContext) : IRecipeImageRepository
{
    public Task<bool> RecipeExistsAsync(Guid recipeId, Guid houseId, CancellationToken ct) =>
        dbContext.Recipes.AnyAsync(
            r => r.Id == recipeId && dbContext.Users.Any(u => u.Id == r.OwnerUserId && u.HouseId == houseId),
            ct);
    public Task<int> CountAsync(Guid recipeId, CancellationToken ct) =>
        dbContext.RecipeImages.CountAsync(i => i.RecipeId == recipeId, ct);
    public async Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, bool tracked, CancellationToken ct)
    {
        if (tracked)
            return await dbContext.RecipeImages.Where(i => i.RecipeId == recipeId).OrderBy(i => i.SortOrder).ToListAsync(ct);
        return await dbContext.RecipeImages.AsNoTracking().Where(i => i.RecipeId == recipeId).OrderBy(i => i.SortOrder)
            .Select(i => new RecipeImage
            {
                Id = i.Id, RecipeId = i.RecipeId, FileName = i.FileName, ContentType = i.ContentType,
                FileSizeBytes = i.FileSizeBytes, Content = Array.Empty<byte>(), AltText = i.AltText,
                SortOrder = i.SortOrder, CreatedAtUtc = i.CreatedAtUtc, UpdatedAtUtc = i.UpdatedAtUtc
            }).ToListAsync(ct);
    }
    public Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, bool includeContent, CancellationToken ct)
    {
        var query = dbContext.RecipeImages.Where(i => i.RecipeId == recipeId && i.Id == imageId);
        return includeContent
            ? query.AsNoTracking().SingleOrDefaultAsync(ct)
            : query.AsNoTracking().Select(i => new RecipeImage
            {
                Id = i.Id, RecipeId = i.RecipeId, FileName = i.FileName, ContentType = i.ContentType,
                FileSizeBytes = i.FileSizeBytes, Content = Array.Empty<byte>(), AltText = i.AltText,
                SortOrder = i.SortOrder, CreatedAtUtc = i.CreatedAtUtc, UpdatedAtUtc = i.UpdatedAtUtc
            }).SingleOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken ct)
    {
        var images = await dbContext.RecipeImages
            .AsNoTracking()
            .Where(i => recipeIds.Contains(i.RecipeId))
            .Select(i => new { i.RecipeId, i.Id, i.SortOrder })
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);
        return images
            .GroupBy(i => i.RecipeId)
            .ToDictionary(g => g.Key, g => g.First().Id);
    }
    public Task<int> UpdateAltTextAsync(Guid recipeId, Guid imageId, string? altText, DateTimeOffset updatedAtUtc, CancellationToken ct) =>
        dbContext.RecipeImages.Where(i => i.RecipeId == recipeId && i.Id == imageId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AltText, altText).SetProperty(i => i.UpdatedAtUtc, updatedAtUtc), ct);
    public Task<int> DeleteAsync(Guid recipeId, Guid imageId, CancellationToken ct) =>
        dbContext.RecipeImages.Where(i => i.RecipeId == recipeId && i.Id == imageId).ExecuteDeleteAsync(ct);
    public async Task UpdateSortOrdersAsync(Guid recipeId, IReadOnlyList<Guid> imageIds, CancellationToken ct)
    {
        await dbContext.RecipeImages.Where(i => i.RecipeId == recipeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.SortOrder, i => i.SortOrder + RecipeImageService.MaxImages), ct);
        for (var index = 0; index < imageIds.Count; index++)
        {
            var id = imageIds[index];
            var position = index;
            await dbContext.RecipeImages.Where(i => i.RecipeId == recipeId && i.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.SortOrder, position), ct);
        }
    }
    public void Add(RecipeImage image) => dbContext.RecipeImages.Add(image);
    public void Remove(RecipeImage image) => dbContext.RecipeImages.Remove(image);
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
