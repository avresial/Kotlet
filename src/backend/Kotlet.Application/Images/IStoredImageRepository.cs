using Kotlet.Domain.Images;

namespace Kotlet.Application.Images;

public interface IStoredImageRepository
{
    Task<StoredImage?> GetAsync(Guid id, bool includeContent, CancellationToken ct);
    void Add(StoredImage image);
    Task UpdateAltTextAsync(Guid id, string? altText, DateTimeOffset updatedAt, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
