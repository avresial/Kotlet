namespace Kotlet.Domain.Common;

/// <summary>
/// A non-negative amount of food energy. Stored internally as kilocalories (the "Calories" used on
/// nutrition labels); <see cref="ToCalories"/>/<see cref="FromCalories"/> convert to and from the
/// (rarely used) small-calorie unit, where 1 kcal = 1000 cal.
/// </summary>
public readonly record struct Calories : IComparable<Calories>
{
    public static readonly Calories Zero = new(0m);

    public decimal Kilocalories { get; }

    public Calories(decimal kilocalories)
    {
        if (kilocalories < 0)
            throw new ArgumentOutOfRangeException(nameof(kilocalories), kilocalories, "Calories cannot be negative.");
        Kilocalories = kilocalories;
    }

    public static Calories FromKilocalories(decimal kilocalories) => new(kilocalories);
    public static Calories FromCalories(decimal calories) => new(calories / 1000m);

    public decimal ToCalories() => Kilocalories * 1000m;

    public static Calories operator +(Calories left, Calories right) => new(left.Kilocalories + right.Kilocalories);
    public static Calories operator -(Calories left, Calories right) => new(left.Kilocalories - right.Kilocalories);
    public static Calories operator *(Calories calories, decimal factor) => new(calories.Kilocalories * factor);
    public static Calories operator *(decimal factor, Calories calories) => calories * factor;
    public static Calories operator /(Calories calories, decimal divisor) => new(calories.Kilocalories / divisor);

    public static bool operator <(Calories left, Calories right) => left.Kilocalories < right.Kilocalories;
    public static bool operator >(Calories left, Calories right) => left.Kilocalories > right.Kilocalories;
    public static bool operator <=(Calories left, Calories right) => left.Kilocalories <= right.Kilocalories;
    public static bool operator >=(Calories left, Calories right) => left.Kilocalories >= right.Kilocalories;

    public int CompareTo(Calories other) => Kilocalories.CompareTo(other.Kilocalories);

    public override string ToString() => $"{Kilocalories:0.##} kcal";
}
