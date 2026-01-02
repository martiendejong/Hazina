using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;
using Hazina.Tools.Services.Images.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Hazina.Tools.Services.Images;

/// <summary>
/// Main implementation of IImageTool.
/// Orchestrates image editing by applying operations sequentially using appropriate providers.
/// </summary>
public class ImageTool : IImageTool
{
    private readonly IImageProviderResolver _resolver;

    /// <summary>
    /// Creates a new ImageTool with the specified provider resolver.
    /// </summary>
    /// <param name="resolver">The resolver for selecting providers.</param>
    public ImageTool(IImageProviderResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public async Task<ImageEditResult> EditAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.InputImage);

        // Check for cancellation before starting
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Operations.Count == 0)
        {
            // No operations - just convert to output format
            return await ConvertFormatAsync(request.InputImage, request.OutputFormat,
                request.ProviderOptions.Quality, cancellationToken);
        }

        try
        {
            // Reset input stream position
            if (request.InputImage.CanSeek)
            {
                request.InputImage.Position = 0;
            }

            // Start with a copy of the input stream to avoid mutating the original
            var currentStream = await CopyStreamAsync(request.InputImage, cancellationToken);

            // Apply each operation sequentially
            foreach (var operation in request.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var provider = _resolver.Resolve(operation, request.ProviderOptions);
                var resultStream = await provider.ApplyAsync(
                    currentStream,
                    operation,
                    request.ProviderOptions,
                    cancellationToken);

                // Dispose the previous stream (unless it's the original input)
                if (currentStream != request.InputImage)
                {
                    await currentStream.DisposeAsync();
                }

                currentStream = resultStream;
            }

            // Convert to output format if needed
            var finalResult = await ConvertFormatAsync(currentStream, request.OutputFormat,
                request.ProviderOptions.Quality, cancellationToken);

            // Dispose intermediate stream
            if (currentStream != request.InputImage)
            {
                await currentStream.DisposeAsync();
            }

            return finalResult;
        }
        catch (Exception ex)
        {
            return ImageEditResult.Failed($"Image editing failed: {ex.Message}");
        }
    }

    private async Task<ImageEditResult> ConvertFormatAsync(
        Stream sourceStream,
        ImageOutputFormat format,
        int quality,
        CancellationToken cancellationToken)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        using var image = await Image.LoadAsync(sourceStream, cancellationToken);
        var outputStream = new MemoryStream();

        switch (format)
        {
            case ImageOutputFormat.Png:
                await image.SaveAsync(outputStream, new PngEncoder(), cancellationToken);
                break;

            case ImageOutputFormat.Jpeg:
                await image.SaveAsync(outputStream, new JpegEncoder { Quality = quality }, cancellationToken);
                break;

            case ImageOutputFormat.Webp:
                await image.SaveAsync(outputStream, new WebpEncoder { Quality = quality }, cancellationToken);
                break;

            case ImageOutputFormat.Gif:
                await image.SaveAsync(outputStream, new GifEncoder(), cancellationToken);
                break;

            default:
                await image.SaveAsync(outputStream, new PngEncoder(), cancellationToken);
                break;
        }

        outputStream.Position = 0;
        return new ImageEditResult(outputStream);
    }

    private static async Task<Stream> CopyStreamAsync(Stream source, CancellationToken cancellationToken)
    {
        var copy = new MemoryStream();
        await source.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return copy;
    }
}
