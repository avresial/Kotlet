using Kotlet.Domain.Common;
using Xunit;

namespace Kotlet.Application.UnitTests.Common;

public sealed class QuantityTests
{
    [Fact]
    public void FromAmount_RejectsNegativeValues() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantity.FromAmount(-1m));

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Quantity.FromAmount(250m), Quantity.FromAmount(250m));
        Assert.NotEqual(Quantity.FromAmount(250m), Quantity.FromAmount(251m));
    }

    [Fact]
    public void AdditionAndSubtractionCombineAmounts()
    {
        var total = Quantity.FromAmount(100m) + Quantity.FromAmount(50m);
        Assert.Equal(Quantity.FromAmount(150m), total);
        Assert.Equal(Quantity.FromAmount(90m), total - Quantity.FromAmount(60m));
    }

    [Fact]
    public void ComparisonOperatorsOrderByAmount()
    {
        var lower = Quantity.FromAmount(10m);
        var higher = Quantity.FromAmount(20m);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower <= lower);
        Assert.True(higher >= higher);
    }
}
