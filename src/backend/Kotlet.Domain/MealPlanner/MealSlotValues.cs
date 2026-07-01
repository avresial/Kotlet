namespace Kotlet.Domain.MealPlanner;

public static class MealSlotValues
{
    public static bool TryParse(string? value, out MealSlot slot) => Enum.TryParse(
        value?.Replace("-", "", StringComparison.Ordinal), true, out slot) && Enum.IsDefined(slot);

    public static string ToApiValue(this MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.SecondBreakfast => "second-breakfast",
        MealSlot.Dinner => "dinner",
        MealSlot.Snack => "snack",
        MealSlot.Supper => "supper",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
