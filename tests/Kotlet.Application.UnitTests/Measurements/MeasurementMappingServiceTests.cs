using Kotlet.Application.Measurements;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Measurements;

public sealed class MeasurementMappingServiceTests
{
    private readonly MeasurementMappingService _service = new();

    [Theory]
    [InlineData(1, "tsp", 5)]
    [InlineData(2, "tbsp", 30)]
    [InlineData(0.5, "cup", 125)]
    public void Normalize_KitchenUnit_ReturnsBaseQuantity(decimal quantity, string unit, decimal expected)
    {
        var result = _service.Normalize(quantity, unit, Ingredient());

        Assert.NotNull(result);
        Assert.Equal(expected, result.Quantity);
        Assert.Equal("g", result.Unit);
    }

    [Fact]
    public void ToDisplay_ChoosesLargestUnitWithWholeQuantity()
    {
        var result = _service.ToDisplay(30m, "g", Ingredient());

        Assert.Equal(2m, result.Quantity);
        Assert.Equal("tbsp", result.Unit);
    }

    [Fact]
    public void ToDisplay_FallsBackToNormalizedMeasurement_WhenNoUnitDividesCleanly()
    {
        var result = _service.ToDisplay(30.1m, "g", Ingredient());

        Assert.Equal(30.1m, result.Quantity);
        Assert.Equal("g", result.Unit);
    }

    [Fact]
    public void PieceConversion_RequiresWholePieceCountOnRead()
    {
        var ingredient = Ingredient(isCountable: true, pieceSize: 150m);

        Assert.Equal(new DisplayMeasurement(2m, "piece"), _service.ToDisplay(300m, "g", ingredient));
        Assert.Equal(new DisplayMeasurement(375m, "g"), _service.ToDisplay(375m, "g", ingredient));
    }

    [Fact]
    public void Normalize_RejectsPieceForUncountableIngredient()
    {
        Assert.Null(_service.Normalize(1m, "piece", Ingredient()));
    }

    private static Ingredient Ingredient(bool isCountable = false, decimal? pieceSize = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Ingredient",
        MeasurementUnit = "g",
        IsCountable = isCountable,
        MeasurementUnitsPerPiece = pieceSize
    };
}
