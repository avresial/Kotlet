using Kotlet.Domain.Images;

namespace Kotlet.Domain.Recipes;

public sealed class RecipeImage
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public StoredImage Image { get; set; } = new() { FileName = string.Empty, ContentType = string.Empty, Content = [] };
    public int SortOrder { get; set; }
}
