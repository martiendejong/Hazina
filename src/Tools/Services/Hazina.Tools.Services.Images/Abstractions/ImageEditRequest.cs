using Hazina.Tools.Services.Images.Operations;

namespace Hazina.Tools.Services.Images.Abstractions;

/// <summary>
/// Request object for image editing operations.
/// </summary>
public sealed class ImageEditRequest
{
    /// <summary>
    /// The source image stream to edit.
    /// </summary>
    public Stream InputImage { get; init; } = default!;

    /// <summary>
    /// The sequence of operations to apply to the image.
    /// Operations are applied in order.
    /// </summary>
    public IReadOnlyList<ImageEditOperation> Operations { get; init; } = [];

    /// <summary>
    /// The desired output format for the edited image.
    /// </summary>
    public ImageOutputFormat OutputFormat { get; init; } = ImageOutputFormat.Png;

    /// <summary>
    /// Provider-specific options for the edit operations.
    /// </summary>
    public ImageProviderOptions ProviderOptions { get; init; } = new();
}

/// <summary>
/// Supported image output formats.
/// </summary>
public enum ImageOutputFormat
{
    Png,
    Jpeg,
    Webp,
    Gif
}
