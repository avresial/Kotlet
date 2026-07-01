namespace Kotlet.Domain.Common;

/// <summary>
/// A positive number of servings a recipe yields. Note that <c>default(ServingCount)</c> (Value 0) is
/// not a valid serving count — always go through the constructor, <see cref="One"/>, or <see cref="FromInt32"/>.
/// </summary>
public readonly record struct ServingCount : IComparable<ServingCount>
{
    public static readonly ServingCount One = new(1);

    public int Value { get; }

    public ServingCount(int value)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Serving count must be at least 1.");
        Value = value;
    }

    public static ServingCount FromInt32(int value) => new(value);

    public static bool operator <(ServingCount left, ServingCount right) => left.Value < right.Value;
    public static bool operator >(ServingCount left, ServingCount right) => left.Value > right.Value;
    public static bool operator <=(ServingCount left, ServingCount right) => left.Value <= right.Value;
    public static bool operator >=(ServingCount left, ServingCount right) => left.Value >= right.Value;

    public int CompareTo(ServingCount other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
