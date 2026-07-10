using Kotlet.Application.Images;
using Kotlet.Infrastructure.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Images;

public sealed class ImageSharpImageProcessorTests
{
    private static readonly ImageProcessingOptions RecipeOptions = new(1200, 900);
    private readonly ImageSharpImageProcessor _processor = new();

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    public async Task Process_ConvertsInputToWebp(string format)
    {
        var input = format == "jpeg" ? TestImages.Jpeg(20, 10) : TestImages.Png(20, 10);

        var result = await Process(input);

        Assert.Equal("image/webp", result.ContentType);
        Assert.True(TestImages.IsWebp(result.Content));
        Assert.Equal(20, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public async Task Process_ResizesLargeImageWithinBounds()
    {
        var result = await Process(TestImages.Jpeg(2400, 1800));

        Assert.Equal(1200, result.Width);
        Assert.Equal(900, result.Height);
        AssertDecodedSize(result.Content, 1200, 900);
    }

    [Theory]
    [InlineData(4000, 1000, 1200, 300)] // wide: constrained by width
    [InlineData(800, 1200, 600, 900)]   // tall: constrained by height
    public async Task Process_PreservesAspectRatioWithoutCropping(int width, int height, int expectedWidth, int expectedHeight)
    {
        var result = await Process(TestImages.Png(width, height));

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    public async Task Process_DoesNotUpscaleSmallImage()
    {
        var result = await Process(TestImages.Jpeg(600, 400));

        Assert.Equal(600, result.Width);
        Assert.Equal(400, result.Height);
    }

    [Fact]
    public async Task Process_AppliesExifOrientationBeforeResizing()
    {
        var input = EncodeJpegWithExif(100, 50, exif =>
            exif.SetValue(ExifTag.Orientation, (ushort)6)); // rotate 90° clockwise

        var result = await Process(input);

        Assert.Equal(50, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public async Task Process_StripsExifMetadata()
    {
        var input = EncodeJpegWithExif(40, 40, exif =>
        {
            exif.SetValue(ExifTag.Artist, "Someone");
            exif.SetValue(ExifTag.GPSLatitude, new[] { Rational.FromDouble(52), Rational.FromDouble(13), Rational.FromDouble(0) });
        });

        var result = await Process(input);

        using var output = Image.Load(result.Content);
        Assert.Null(output.Metadata.ExifProfile);
        Assert.Null(output.Metadata.XmpProfile);
        Assert.Null(output.Metadata.IptcProfile);
    }

    [Fact]
    public async Task Process_RejectsNonImageContent()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5]);

        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _processor.ProcessAsync(stream, RecipeOptions, CancellationToken.None));
    }

    [Fact]
    public async Task Process_RejectsInvalidOptions()
    {
        using var stream = new MemoryStream(TestImages.Png());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _processor.ProcessAsync(stream, new ImageProcessingOptions(0, 900), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _processor.ProcessAsync(stream, new ImageProcessingOptions(1200, 900, Quality: 101), CancellationToken.None));
    }

    private async Task<ImageProcessingResult> Process(byte[] input)
    {
        using var stream = new MemoryStream(input);
        return await _processor.ProcessAsync(stream, RecipeOptions, CancellationToken.None);
    }

    private static void AssertDecodedSize(byte[] content, int width, int height)
    {
        using var image = Image.Load(content);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    private static byte[] EncodeJpegWithExif(int width, int height, Action<ExifProfile> configure)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(90, 140, 200));
        var exif = new ExifProfile();
        configure(exif);
        image.Metadata.ExifProfile = exif;
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }
}
