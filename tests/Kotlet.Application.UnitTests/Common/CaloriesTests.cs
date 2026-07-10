using Kotlet.Domain.Common;
using Xunit;

namespace Kotlet.Application.UnitTests.Common;

public sealed class CaloriesTests
{
    [Fact]
    public void FromKilocalories_RejectsNegativeValues() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Calories.FromKilocalories(-1m));

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Calories.FromKilocalories(150m), Calories.FromKilocalories(150m));
        Assert.NotEqual(Calories.FromKilocalories(150m), Calories.FromKilocalories(151m));
    }

    [Fact]
    public void FromCalories_ConvertsSmallCaloriesToKilocalories() =>
        Assert.Equal(1m, Calories.FromCalories(1000m).Kilocalories);

    [Fact]
    public void ToCalories_ConvertsKilocaloriesToSmallCalories() =>
        Assert.Equal(2000m, Calories.FromKilocalories(2m).ToCalories());

    [Fact]
    public void AdditionAndSubtractionCombineAmounts()
    {
        var total = Calories.FromKilocalories(120m) + Calories.FromKilocalories(30m);
        Assert.Equal(Calories.FromKilocalories(150m), total);
        Assert.Equal(Calories.FromKilocalories(90m), total - Calories.FromKilocalories(60m));
    }

    [Fact]
    public void MultiplicationScalesByAFactor()
    {
        var caloriesPer100BaseUnits = Calories.FromKilocalories(250m);
        var caloriesForQuantity = caloriesPer100BaseUnits * 150m / 100m;
        Assert.Equal(Calories.FromKilocalories(375m), caloriesForQuantity);
    }

    [Fact]
    public void ComparisonOperatorsOrderByKilocalories()
    {
        var lower = Calories.FromKilocalories(50m);
        var higher = Calories.FromKilocalories(100m);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower <= higher);
        Assert.True(higher >= lower);
    }
}
