using Kotlet.Application.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Kotlet.Infrastructure.Images;

/// <summary>
/// Decodes the input, applies EXIF orientation, downscales it to fit the requested bounds,
/// strips metadata, and re-encodes it as lossy WebP.
/// </summary>
public sealed class ImageSharpImageProcessor : IImageProcessor
{
    public async Task<ImageProcessingResult> ProcessAsync(
        Stream image, ImageProcessingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Quality, 100);

        Image decoded;
        try
        {
            decoded = await Image.LoadAsync(image, cancellationToken);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidImageException("The supplied content is not a valid image in a supported format.", ex);
        }

        using (decoded)
        {
            decoded.Mutate(context =>
            {
                context.AutoOrient();
                var size = context.GetCurrentSize();
                if (size.Width > options.MaxWidth || size.Height > options.MaxHeight)
                    context.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(options.MaxWidth, options.MaxHeight)
                    });
            });

            // ICC profiles are kept so colors stay faithful; everything else is privacy/size overhead.
            decoded.Metadata.ExifProfile = null;
            decoded.Metadata.XmpProfile = null;
            decoded.Metadata.IptcProfile = null;
            decoded.Metadata.CicpProfile = null;

            using var output = new MemoryStream();
            var encoder = new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = options.Quality };
            await decoded.SaveAsync(output, encoder, cancellationToken);
            return new ImageProcessingResult(output.ToArray(), "image/webp", decoded.Width, decoded.Height);
        }
    }
}
