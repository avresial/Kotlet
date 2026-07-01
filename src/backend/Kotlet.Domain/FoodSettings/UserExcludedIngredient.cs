using Kotlet.Domain.Ingredients;

namespace Kotlet.Domain.FoodSettings;

public sealed class UserExcludedIngredient
{
    public Guid UserId { get; set; }
    public Guid IngredientId { get; set; }
    public UserFoodSettings Settings { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
