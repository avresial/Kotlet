using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Images;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImageRepository(KotletDbContext dbContext) : IRecipeImageRepository
{
    public Task<bool> RecipeExistsAsync(Guid recipeId, Guid houseId, CancellationToken ct) =>
        dbContext.Recipes.AnyAsync(
            r => r.Id == recipeId && r.HouseId == houseId,
            ct);
    public Task<int> CountAsync(Guid recipeId, CancellationToken ct) =>
        dbContext.RecipeImages.CountAsync(i => i.RecipeId == recipeId, ct);
    public async Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, CancellationToken ct) =>
        await dbContext.RecipeImages.AsNoTracking().Where(i => i.RecipeId == recipeId).OrderBy(i => i.SortOrder)
            .Select(i => new RecipeImage
            {
                Id = i.Id,
                RecipeId = i.RecipeId,
                Image = new StoredImage { Id = i.Image.Id, FileName = i.Image.FileName, ContentType = i.Image.ContentType,
                    FileSizeBytes = i.Image.FileSizeBytes, Content = Array.Empty<byte>(), AltText = i.Image.AltText,
                    CreatedAtUtc = i.Image.CreatedAtUtc, UpdatedAtUtc = i.Image.UpdatedAtUtc,
                    Sources = i.Image.Sources.Select(s => new RecipeImageSource { RecipeImageId = s.RecipeImageId,
                        SourceId = s.SourceId, Source = s.Source }).ToList() },
                SortOrder = i.SortOrder,
            }).ToListAsync(ct);
    public Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, CancellationToken ct) =>
        dbContext.RecipeImages.AsNoTracking().Where(i => i.RecipeId == recipeId && i.Id == imageId).Select(i => new RecipeImage
            {
                Id = i.Id,
                RecipeId = i.RecipeId,
                Image = new StoredImage { Id = i.Image.Id, FileName = i.Image.FileName, ContentType = i.Image.ContentType,
                    FileSizeBytes = i.Image.FileSizeBytes, Content = Array.Empty<byte>(), AltText = i.Image.AltText,
                    CreatedAtUtc = i.Image.CreatedAtUtc, UpdatedAtUtc = i.Image.UpdatedAtUtc,
                    Sources = i.Image.Sources.Select(s => new RecipeImageSource { RecipeImageId = s.RecipeImageId,
                        SourceId = s.SourceId, Source = s.Source }).ToList() },
                SortOrder = i.SortOrder,
            }).SingleOrDefaultAsync(ct);
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
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
