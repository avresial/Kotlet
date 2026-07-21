using Kotlet.Domain.Ingredients;

namespace Kotlet.Domain.PreparedMeals;

public sealed class PreparedMeal
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Store { get; set; }
    public string? Category { get; set; }
    public decimal? PackageQuantity { get; set; }
    public string? PackageUnit { get; set; }
    public int Servings { get; set; } = 1;
    public decimal CaloriesPerServing { get; set; }
    public decimal? Price { get; set; }
    public string? PreparationInstructions { get; set; }
    public Guid? ShoppingIngredientId { get; set; }
    public Ingredient? ShoppingIngredient { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<PreparedMealAddon> Addons { get; set; } = [];
    public ICollection<PreparedMealImage> Images { get; set; } = [];
}
