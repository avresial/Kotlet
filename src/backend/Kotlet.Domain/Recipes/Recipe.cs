using Kotlet.Domain.Common;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Domain.Recipes;

public sealed class Recipe
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? DescriptionMarkdown { get; set; }

    /// <summary>
    /// Number of adult portions the recipe yields. One serving is a single adult
    /// portion. The ingredient quantities describe the amounts needed for this many
    /// servings, so this value is the basis for scaling ingredient amounts when
    /// calculating meal-prep prices and the units to buy for a shopping list.
    /// </summary>
    public ServingCount Servings { get; set; } = ServingCount.One;
    public MealSlot? MealType { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
    public ICollection<RecipeImage> Images { get; set; } = [];
}
