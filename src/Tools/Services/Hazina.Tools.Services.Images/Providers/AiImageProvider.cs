using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;

namespace Hazina.Tools.Services.Images.Providers;

/// <summary>
/// AI-based image provider for operations requiring generative AI.
/// Currently handles MaskReplaceOperation using external AI services.
///
/// NOTE: This is a provider abstraction - actual AI integration should be
/// implemented in consuming applications that configure the appropriate
/// AI service (OpenAI, StabilityAI, etc.)
/// </summary>
public class AiImageProvider : IImageProvider
{
    private readonly Func<Stream, Stream, string, ImageProviderOptions, CancellationToken, Task<Stream>>? _maskReplaceHandler;

    /// <summary>
    /// Creates an AI provider with no handler configured.
    /// Operations will throw NotSupportedException until a handler is provided.
    /// </summary>
    public AiImageProvider()
    {
    }

    /// <summary>
    /// Creates an AI provider with a custom mask replacement handler.
    /// </summary>
    /// <param name="maskReplaceHandler">
    /// Handler function that takes (sourceImage, maskImage, prompt, options, cancellationToken)
    /// and returns the edited image stream.
    /// </param>
    public AiImageProvider(
        Func<Stream, Stream, string, ImageProviderOptions, CancellationToken, Task<Stream>> maskReplaceHandler)
    {
        _maskReplaceHandler = maskReplaceHandler;
    }

    /// <inheritdoc />
    public string Name => "AI";

    /// <inheritdoc />
    public bool CanHandle(ImageEditOperation operation)
    {
        return operation switch
        {
            MaskReplaceOperation => _maskReplaceHandler != null,
            _ => false
        };
    }

    /// <inheritdoc />
    public async Task<Stream> ApplyAsync(
        Stream image,
        ImageEditOperation operation,
        ImageProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        if (operation is MaskReplaceOperation maskReplace)
        {
            return await ApplyMaskReplaceAsync(image, maskReplace, options, cancellationToken);
        }

        throw new NotSupportedException(
            $"Operation '{operation.GetType().Name}' is not supported by AiImageProvider. " +
            "Use LocalImageProvider for basic image operations.");
    }

    private async Task<Stream> ApplyMaskReplaceAsync(
        Stream image,
        MaskReplaceOperation operation,
        ImageProviderOptions options,
        CancellationToken cancellationToken)
    {
        if (_maskReplaceHandler == null)
        {
            throw new InvalidOperationException(
                "AI-based mask replacement is not configured. " +
                "To use MaskReplaceOperation, configure an AI provider with a mask replace handler. " +
                "Example: services.AddAiImageProvider(handler => ...)");
        }

        // Reset stream positions
        if (image.CanSeek)
        {
            image.Position = 0;
        }

        if (operation.MaskImage.CanSeek)
        {
            operation.MaskImage.Position = 0;
        }

        var result = await _maskReplaceHandler(
            image,
            operation.MaskImage,
            operation.Prompt,
            options,
            cancellationToken);

        // Ensure result stream position is reset
        if (result.CanSeek)
        {
            result.Position = 0;
        }

        return result;
    }
}

/// <summary>
/// Builder for configuring AI image provider with external AI service integration.
/// </summary>
public class AiImageProviderBuilder
{
    private Func<Stream, Stream, string, ImageProviderOptions, CancellationToken, Task<Stream>>? _maskReplaceHandler;

    /// <summary>
    /// Configures the handler for mask replacement operations.
    /// </summary>
    /// <param name="handler">
    /// Async function that takes (sourceImage, maskImage, prompt, options, cancellationToken)
    /// and returns the edited image stream.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    public AiImageProviderBuilder WithMaskReplaceHandler(
        Func<Stream, Stream, string, ImageProviderOptions, CancellationToken, Task<Stream>> handler)
    {
        _maskReplaceHandler = handler;
        return this;
    }

    /// <summary>
    /// Builds the configured AI image provider.
    /// </summary>
    public AiImageProvider Build()
    {
        if (_maskReplaceHandler != null)
        {
            return new AiImageProvider(_maskReplaceHandler);
        }

        return new AiImageProvider();
    }
}
