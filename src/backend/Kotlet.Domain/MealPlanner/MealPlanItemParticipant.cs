using Kotlet.Domain.Auth;

namespace Kotlet.Domain.MealPlanner;

public sealed class MealPlanItemParticipant
{
    public const int MinPortionPercent = 50;
    public const int MaxPortionPercent = 150;

    public Guid MealPlanItemId { get; set; }
    public Guid UserId { get; set; }
    public int PortionPercent { get; set; } = 100;
    public MealPlanItem MealPlanItem { get; set; } = null!;
    public User User { get; set; } = null!;
}
