namespace Kotlet.Application.Images;

/// <summary>Thrown when supplied content is not a valid image in a supported format.</summary>
public sealed class InvalidImageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
