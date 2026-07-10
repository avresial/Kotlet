using Kotlet.Domain.Sources;

namespace Kotlet.Domain.Recipes;

/// <summary>Associates a stored image with the source metadata describing where it came from (attribution).</summary>
public sealed class RecipeImageSource
{
    public Guid RecipeImageId { get; set; }
    public RecipeImage RecipeImage { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
}
