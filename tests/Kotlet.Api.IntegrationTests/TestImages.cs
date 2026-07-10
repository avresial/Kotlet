using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Kotlet.Api.IntegrationTests;

internal static class TestImages
{
    public static byte[] Png(int width = 8, int height = 8) => Encode(width, height, new PngEncoder());
    public static byte[] Jpeg(int width = 8, int height = 8) => Encode(width, height, new JpegEncoder());
    public static byte[] Webp(int width = 8, int height = 8) => Encode(width, height, new WebpEncoder());

    public static bool IsWebp(byte[] content) =>
        content.Length >= 12 &&
        Encoding.ASCII.GetString(content, 0, 4) == "RIFF" &&
        Encoding.ASCII.GetString(content, 8, 4) == "WEBP";

    private static byte[] Encode(int width, int height, IImageEncoder encoder)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(210, 120, 60));
        using var stream = new MemoryStream();
        image.Save(stream, encoder);
        return stream.ToArray();
    }
}
