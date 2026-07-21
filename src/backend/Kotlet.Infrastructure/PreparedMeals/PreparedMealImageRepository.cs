using Kotlet.Application.PreparedMeals;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Images;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealImageRepository(KotletDbContext db) : IPreparedMealImageRepository
{
    public Task<bool> MealExistsAsync(Guid mealId, Guid houseId, CancellationToken ct) =>
        db.PreparedMeals.AnyAsync(meal => meal.Id == mealId && meal.HouseId == houseId, ct);

    public async Task<IReadOnlyList<PreparedMealImage>> ListAsync(Guid mealId, CancellationToken ct) =>
        await db.PreparedMealImages
            .AsNoTracking()
            .Where(image => image.PreparedMealId == mealId)
            .OrderBy(image => image.SortOrder)
            .Select(image => new PreparedMealImage
            {
                Id = image.Id,
                PreparedMealId = image.PreparedMealId,
                SortOrder = image.SortOrder,
                Image = new StoredImage
                {
                    Id = image.Image.Id,
                    FileName = image.Image.FileName,
                    ContentType = image.Image.ContentType,
                    FileSizeBytes = image.Image.FileSizeBytes,
                    Content = Array.Empty<byte>(),
                    AltText = image.Image.AltText,
                    CreatedAtUtc = image.Image.CreatedAtUtc,
                    UpdatedAtUtc = image.Image.UpdatedAtUtc
                }
            })
            .ToListAsync(ct);

    public Task<PreparedMealImage?> GetAsync(Guid mealId, Guid imageId, CancellationToken ct) =>
        db.PreparedMealImages
            .AsNoTracking()
            .Where(image => image.PreparedMealId == mealId && image.Id == imageId)
            .Select(image => new PreparedMealImage
            {
                Id = image.Id,
                PreparedMealId = image.PreparedMealId,
                SortOrder = image.SortOrder,
                Image = new StoredImage
                {
                    Id = image.Image.Id,
                    FileName = image.Image.FileName,
                    ContentType = image.Image.ContentType,
                    FileSizeBytes = image.Image.FileSizeBytes,
                    Content = Array.Empty<byte>(),
                    AltText = image.Image.AltText,
                    CreatedAtUtc = image.Image.CreatedAtUtc,
                    UpdatedAtUtc = image.Image.UpdatedAtUtc
                }
            })
            .SingleOrDefaultAsync(ct);

    public void Add(PreparedMealImage image) => db.PreparedMealImages.Add(image);

    public async Task UpdateSortOrdersAsync(Guid mealId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await db.PreparedMealImages
            .Where(image => image.PreparedMealId == mealId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                image => image.SortOrder,
                image => image.SortOrder + PreparedMealImageService.MaxImages), ct);

        for (var index = 0; index < ids.Count; index++)
        {
            var imageId = ids[index];
            var sortOrder = index;
            await db.PreparedMealImages
                .Where(image => image.PreparedMealId == mealId && image.Id == imageId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(image => image.SortOrder, sortOrder),
                    ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
