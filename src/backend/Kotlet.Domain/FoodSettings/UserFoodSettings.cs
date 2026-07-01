using Kotlet.Domain.Auth;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Domain.FoodSettings;

public sealed class UserFoodSettings
{
    public Guid UserId { get; set; }
    public Allergen AvoidedAllergens { get; set; }
    public FoodAttribute AvoidedAttributes { get; set; }
    public DietarySuitability RequiredSuitability { get; set; }
    public User User { get; set; } = null!;
    public ICollection<UserExcludedIngredient> ExcludedIngredients { get; set; } = [];
}
