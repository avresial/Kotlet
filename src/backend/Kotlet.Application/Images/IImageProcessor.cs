namespace Kotlet.Application.Images;

/// <summary>Transforms raw image data into an optimized representation for storage.</summary>
public interface IImageProcessor
{
    /// <exception cref="InvalidImageException">The stream does not contain a valid, supported image.</exception>
    Task<ImageProcessingResult> ProcessAsync(
        Stream image,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default);
}
