using Kotlet.Application.Images;
using Kotlet.Domain.Images;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Images;

internal sealed class StoredImageRepository(KotletDbContext db) : IStoredImageRepository
{
    public Task<StoredImage?> GetAsync(Guid id, bool includeContent, CancellationToken ct) => includeContent
        ? db.Images.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
        : db.Images.AsNoTracking().Select(x => new StoredImage { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType,
            FileSizeBytes = x.FileSizeBytes, Content = Array.Empty<byte>(), AltText = x.AltText, CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc }).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(StoredImage image) => db.Images.Add(image);
    public Task UpdateAltTextAsync(Guid id, string? altText, DateTimeOffset updatedAt, CancellationToken ct) => db.Images.Where(x => x.Id == id)
        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AltText, altText).SetProperty(x => x.UpdatedAtUtc, updatedAt), ct);
    public Task DeleteAsync(Guid id, CancellationToken ct) => db.Images.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
}
