using Kotlet.Domain.Sources;

namespace Kotlet.Domain.Recipes;

/// <summary>Associates a recipe with the source metadata describing where it came from.</summary>
public sealed class RecipeSource
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
}
