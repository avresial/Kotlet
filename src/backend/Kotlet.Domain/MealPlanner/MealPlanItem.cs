namespace Kotlet.Domain.MealPlanner;

public sealed class MealPlanItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public MealSlot Slot { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Explicit serving count. When null, the effective serving count is derived
    /// from the number of assigned participants (headcount).
    /// </summary>
    public int? Servings { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<MealPlanItemParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Effective serving count: the explicit override when set, otherwise the
    /// number of assigned participants.
    /// </summary>
    public int EffectiveServings => Servings ?? Participants.Count;
}
