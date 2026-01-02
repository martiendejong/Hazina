namespace Hazina.Tools.Services.Images.Abstractions;

/// <summary>
/// Result of an image edit operation.
/// </summary>
public sealed class ImageEditResult
{
    /// <summary>
    /// The edited image as a stream.
    /// Position is reset to 0 for immediate reading.
    /// </summary>
    public Stream Image { get; }

    /// <summary>
    /// Creates a new image edit result.
    /// </summary>
    /// <param name="image">The edited image stream.</param>
    public ImageEditResult(Stream image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static ImageEditResult Failed(string errorMessage)
    {
        return new ImageEditResult(Stream.Null)
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
