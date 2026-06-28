using Kotlet.Domain.Auth;

namespace Kotlet.Domain.MealPlanner;

public sealed class MealPlanItemParticipant
{
    public Guid MealPlanItemId { get; set; }
    public Guid UserId { get; set; }
    public MealPlanItem MealPlanItem { get; set; } = null!;
    public User User { get; set; } = null!;
}
