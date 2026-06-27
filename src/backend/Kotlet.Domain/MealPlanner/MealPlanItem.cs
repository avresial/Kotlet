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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
