namespace Kotlet.Domain.Common;

/// <summary>A non-negative monetary amount.</summary>
public readonly record struct Price : IComparable<Price>
{
    public static readonly Price Zero = new(0m);

    public decimal Amount { get; }

    public Price(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Price cannot be negative.");
        Amount = amount;
    }

    public static Price FromAmount(decimal amount) => new(amount);

    public Price RoundedToCents() => new(decimal.Round(Amount, 2));

    public static Price operator +(Price left, Price right) => new(left.Amount + right.Amount);
    public static Price operator -(Price left, Price right) => new(left.Amount - right.Amount);
    public static Price operator *(Price price, decimal factor) => new(price.Amount * factor);
    public static Price operator *(decimal factor, Price price) => price * factor;
    public static Price operator /(Price price, decimal divisor) => new(price.Amount / divisor);

    public static bool operator <(Price left, Price right) => left.Amount < right.Amount;
    public static bool operator >(Price left, Price right) => left.Amount > right.Amount;
    public static bool operator <=(Price left, Price right) => left.Amount <= right.Amount;
    public static bool operator >=(Price left, Price right) => left.Amount >= right.Amount;

    public int CompareTo(Price other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("0.00");
}
