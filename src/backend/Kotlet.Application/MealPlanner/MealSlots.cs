using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Parses and formats the API names of meal slots.</summary>
internal static class MealSlots
{
    private static readonly HashSet<string> ValidSlots = ["breakfast", "second-breakfast", "dinner", "snack", "supper"];

    /// <summary>
    /// Validates a slot name, returning the slot validation error dictionary when it is
    /// missing or unknown, or null when it is valid. Shared by every meal-plan operation so the
    /// set of valid slots and the message stay in one place.
    /// </summary>
    public static Dictionary<string, string[]>? Error(string? slot) =>
        string.IsNullOrWhiteSpace(slot) || !ValidSlots.Contains(slot.ToLower())
            ? new Dictionary<string, string[]> { ["slot"] = [$"Slot must be one of: {string.Join(", ", ValidSlots)}."] }
            : null;

    public static MealSlot Parse(string slot) => slot.ToLower() switch
    {
        "breakfast" => MealSlot.Breakfast,
        "second-breakfast" => MealSlot.SecondBreakfast,
        "dinner" => MealSlot.Dinner,
        "snack" => MealSlot.Snack,
        "supper" => MealSlot.Supper,
        _ => throw new InvalidOperationException($"Invalid slot: {slot}")
    };

    public static string ToName(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.SecondBreakfast => "second-breakfast",
        MealSlot.Dinner => "dinner",
        MealSlot.Snack => "snack",
        MealSlot.Supper => "supper",
        _ => throw new InvalidOperationException($"Unknown slot: {slot}")
    };
}
