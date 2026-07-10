using Kotlet.Domain.Common;
using Xunit;

namespace Kotlet.Application.UnitTests.Common;

public sealed class PriceTests
{
    [Fact]
    public void FromAmount_RejectsNegativeValues() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Price.FromAmount(-0.01m));

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Price.FromAmount(2.50m), Price.FromAmount(2.50m));
        Assert.NotEqual(Price.FromAmount(2.50m), Price.FromAmount(2.51m));
    }

    [Fact]
    public void MultiplicationScalesByAFactor()
    {
        var pricePer100BaseUnits = Price.FromAmount(2.50m);
        var totalPrice = pricePer100BaseUnits * 250m / 100m;
        Assert.Equal(Price.FromAmount(6.25m), totalPrice);
    }

    [Fact]
    public void RoundedToCents_RoundsToTwoDecimalPlaces() =>
        Assert.Equal(Price.FromAmount(3.33m), Price.FromAmount(100m / 30m).RoundedToCents());

    [Fact]
    public void ComparisonOperatorsOrderByAmount()
    {
        var lower = Price.FromAmount(1m);
        var higher = Price.FromAmount(2m);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower <= higher);
        Assert.True(higher >= lower);
    }
}
