namespace Kotlet.Domain.Recipes;

public sealed class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int SortOrder { get; set; }
    public required string Name { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
    public Recipe Recipe { get; set; } = null!;
}
