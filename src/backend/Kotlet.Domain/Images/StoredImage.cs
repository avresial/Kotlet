using Kotlet.Domain.Recipes;

namespace Kotlet.Domain.Images;

public sealed class StoredImage
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public required byte[] Content { get; set; }
    public string? AltText { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public ICollection<RecipeImageSource> Sources { get; set; } = [];
}
