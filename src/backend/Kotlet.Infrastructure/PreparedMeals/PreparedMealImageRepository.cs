using Kotlet.Application.PreparedMeals;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Images;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealImageRepository(KotletDbContext db) : IPreparedMealImageRepository
{
    public Task<bool> MealExistsAsync(Guid mealId, Guid houseId, CancellationToken ct) => db.PreparedMeals.AnyAsync(x => x.Id == mealId && x.HouseId == houseId, ct);
    public async Task<IReadOnlyList<PreparedMealImage>> ListAsync(Guid mealId, CancellationToken ct) => await db.PreparedMealImages.AsNoTracking().Where(x => x.PreparedMealId == mealId).OrderBy(x => x.SortOrder).Select(x => new PreparedMealImage { Id = x.Id, PreparedMealId = x.PreparedMealId, SortOrder = x.SortOrder, Image = new StoredImage { Id = x.Image.Id, FileName = x.Image.FileName, ContentType = x.Image.ContentType, FileSizeBytes = x.Image.FileSizeBytes, Content = Array.Empty<byte>(), AltText = x.Image.AltText, CreatedAtUtc = x.Image.CreatedAtUtc, UpdatedAtUtc = x.Image.UpdatedAtUtc } }).ToListAsync(ct);
    public Task<PreparedMealImage?> GetAsync(Guid mealId, Guid imageId, CancellationToken ct) => db.PreparedMealImages.AsNoTracking().Where(x => x.PreparedMealId == mealId && x.Id == imageId).Select(x => new PreparedMealImage { Id = x.Id, PreparedMealId = x.PreparedMealId, SortOrder = x.SortOrder, Image = new StoredImage { Id = x.Image.Id, FileName = x.Image.FileName, ContentType = x.Image.ContentType, FileSizeBytes = x.Image.FileSizeBytes, Content = Array.Empty<byte>(), AltText = x.Image.AltText, CreatedAtUtc = x.Image.CreatedAtUtc, UpdatedAtUtc = x.Image.UpdatedAtUtc } }).SingleOrDefaultAsync(ct);
    public void Add(PreparedMealImage image) => db.PreparedMealImages.Add(image);
    public async Task UpdateSortOrdersAsync(Guid mealId, IReadOnlyList<Guid> ids, CancellationToken ct) { await db.PreparedMealImages.Where(x => x.PreparedMealId == mealId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + PreparedMealImageService.MaxImages), ct); for (var i = 0; i < ids.Count; i++) { var id = ids[i]; var order = i; await db.PreparedMealImages.Where(x => x.PreparedMealId == mealId && x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, order), ct); } }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
