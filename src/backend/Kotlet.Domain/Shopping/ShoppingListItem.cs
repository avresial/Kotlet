using Kotlet.Domain.Common;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Domain.Shopping;

public sealed class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? PreparedMealId { get; set; }
    public Quantity Quantity { get; set; }
    public bool IsPurchased { get; set; }
    public string? Note { get; set; }
    public House House { get; set; } = null!;
    public Ingredient? Ingredient { get; set; }
    public PreparedMeal? PreparedMeal { get; set; }
}
