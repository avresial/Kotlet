namespace Kotlet.Domain.Common;

/// <summary>A non-negative amount of an ingredient, expressed in whatever unit the caller already agreed on.</summary>
public readonly record struct Quantity : IComparable<Quantity>
{
    public static readonly Quantity Zero = new(0m);

    public decimal Amount { get; }

    public Quantity(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Quantity cannot be negative.");
        Amount = amount;
    }

    public static Quantity FromAmount(decimal amount) => new(amount);

    public static Quantity operator +(Quantity left, Quantity right) => new(left.Amount + right.Amount);
    public static Quantity operator -(Quantity left, Quantity right) => new(left.Amount - right.Amount);
    public static Quantity operator *(Quantity quantity, decimal factor) => new(quantity.Amount * factor);
    public static Quantity operator *(decimal factor, Quantity quantity) => quantity * factor;
    public static Quantity operator /(Quantity quantity, decimal divisor) => new(quantity.Amount / divisor);

    public static bool operator <(Quantity left, Quantity right) => left.Amount < right.Amount;
    public static bool operator >(Quantity left, Quantity right) => left.Amount > right.Amount;
    public static bool operator <=(Quantity left, Quantity right) => left.Amount <= right.Amount;
    public static bool operator >=(Quantity left, Quantity right) => left.Amount >= right.Amount;

    public int CompareTo(Quantity other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString();
}
