using Kotlet.Domain.Common;
using Xunit;

namespace Kotlet.Application.UnitTests.Common;

public sealed class ServingCountTests
{
    [Fact]
    public void FromInt32_RejectsZeroOrNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ServingCount.FromInt32(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ServingCount.FromInt32(-1));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(ServingCount.FromInt32(4), ServingCount.FromInt32(4));
        Assert.NotEqual(ServingCount.FromInt32(4), ServingCount.FromInt32(5));
    }

    [Fact]
    public void One_HasValueOfOne() => Assert.Equal(1, ServingCount.One.Value);

    [Fact]
    public void ComparisonOperatorsOrderByValue()
    {
        var lower = ServingCount.FromInt32(2);
        var higher = ServingCount.FromInt32(4);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower <= higher);
        Assert.True(higher >= lower);
    }
}
