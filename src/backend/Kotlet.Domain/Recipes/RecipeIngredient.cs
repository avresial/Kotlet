namespace Kotlet.Domain.Recipes;

using Kotlet.Domain.Ingredients;

public sealed class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public int SortOrder { get; set; }
    public decimal NormalizedQuantity { get; set; }
    public required string NormalizedUnit { get; set; }
    public string? Note { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
