namespace Kotlet.Application.Images;

/// <summary>
/// Maximum output dimensions and encoder quality. The image is scaled down to fit within
/// <paramref name="MaxWidth"/> x <paramref name="MaxHeight"/> preserving aspect ratio; it is never upscaled or cropped.
/// </summary>
public sealed record ImageProcessingOptions(int MaxWidth, int MaxHeight, int Quality = ImageProcessingOptions.DefaultQuality)
{
    public const int DefaultQuality = 75;
}

public sealed record ImageProcessingResult(byte[] Content, string ContentType, int Width, int Height);
