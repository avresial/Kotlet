using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Domain.PreparedMeals;

public sealed class PreparedMealAddon
{
    public Guid Id { get; set; }
    public Guid PreparedMealId { get; set; }
    public Guid IngredientId { get; set; }
    public Quantity DefaultQuantity { get; set; }
    public required string Unit { get; set; }
    public bool IsSelectedByDefault { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public PreparedMeal PreparedMeal { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
