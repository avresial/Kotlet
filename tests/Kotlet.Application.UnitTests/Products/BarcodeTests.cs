using Kotlet.Application.Products;
using Xunit;

namespace Kotlet.Application.UnitTests.Products;

public sealed class BarcodeTests
{
    [Theory]
    [InlineData("96385074", "00000096385074")]
    [InlineData("036000291452", "00036000291452")]
    [InlineData("0036000291452", "00036000291452")]
    [InlineData(" 3017620422003 ", "03017620422003")]
    [InlineData("03017620422003", "03017620422003")]
    public void Normalize_PreservesLeadingZerosAndMergesEquivalentCodes(string input, string expected)
    {
        Assert.True(Barcode.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000000")]
    [InlineData("3017620422004")]
    [InlineData("٣٠١٧٦٢٠٤٢٢٠٠٣")]
    [InlineData("3017 620422003")]
    [InlineData("123456")]
    [InlineData("../../../etc")]
    public void Normalize_RejectsInvalidInput(string? input) => Assert.False(Barcode.TryNormalize(input, out _));

    [Theory]
    [InlineData("00000096385074", "96385074")]
    [InlineData("00036000291452", "0036000291452")]
    [InlineData("03017620422003", "3017620422003")]
    [InlineData("13017620422000", "13017620422000")]
    public void OffCode_UsesOffNormalization(string input, string expected) => Assert.Equal(expected, Barcode.ToOpenFoodFactsCode(input));
}
