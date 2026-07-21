namespace Kotlet.Domain.MealPlanner;

using Kotlet.Domain.PreparedMeals;

public sealed class MealPlanItem
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public MealSlot Slot { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? PreparedMealId { get; set; }
    public PreparedMeal? PreparedMeal { get; set; }
    // MVP: prepared-meal add-ons are stored as regular ingredient meal-plan items
    // linked to their prepared-meal item. Introduce PlannedMealAddon only if add-ons
    // later require behavior that cannot be represented by normal ingredient items.
    public Guid? ParentMealPlanItemId { get; set; }
    public MealPlanItem? ParentMealPlanItem { get; set; }
    public ICollection<MealPlanItem> AddonItems { get; set; } = [];
    public decimal? IngredientQuantity { get; set; }
    public string? IngredientUnit { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Explicit serving count. When null, the effective serving count is derived
    /// from the number of assigned participants plus any guests (headcount).
    /// </summary>
    public int? Servings { get; set; }

    /// <summary>
    /// Number of extra guests joining the meal who are not house members.
    /// Adds to the headcount used to derive the serving count.
    /// </summary>
    public int Guests { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<MealPlanItemParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Effective serving count: the explicit override when set, otherwise the
    /// number of assigned participants plus guests.
    /// </summary>
    public decimal EffectiveServings => Participants.Count > 0
        ? Participants.Sum(participant => participant.PortionPercent) / 100m + Guests
        : Servings ?? Guests;
}
