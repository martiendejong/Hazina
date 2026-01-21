using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Hazina.Tools.Services.Images.LayeredImage.Services;

/// <summary>
/// Composites multiple layers into a single image using ImageSharp.
/// Supports blend modes, positioning, and opacity.
/// </summary>
public class LayerCompositor : ILayerCompositor
{
    public async Task<byte[]> CompositeAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default)
    {
        // Create transparent canvas
        using var canvas = new Image<Rgba32>(canvasWidth, canvasHeight, new Rgba32(0, 0, 0, 0));

        // Composite each layer (bottom to top)
        foreach (var layer in layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!layer.Visible || layer.ImageData.Length == 0)
                continue;

            using var layerImage = Image.Load<Rgba32>(layer.ImageData);

            // Apply opacity to layer
            if (layer.Opacity < 255)
            {
                layerImage.Mutate(ctx => ctx.Opacity(layer.Opacity / 255f));
            }

            // Get the pixel blending mode
            var blendMode = GetPixelColorBlendingMode(layer.BlendMode);

            // Draw layer onto canvas at specified position
            var point = new Point(layer.PositionX, layer.PositionY);

            canvas.Mutate(ctx =>
            {
                var options = new GraphicsOptions
                {
                    ColorBlendingMode = blendMode,
                    AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver
                };
                ctx.DrawImage(layerImage, point, options);
            });
        }

        // Export to PNG
        using var output = new MemoryStream();
        await canvas.SaveAsPngAsync(output, cancellationToken);
        return output.ToArray();
    }

    public async Task<byte[]> GenerateThumbnailAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        int maxDimension = 256,
        CancellationToken cancellationToken = default)
    {
        // First, composite the full image
        var fullImage = await CompositeAsync(canvasWidth, canvasHeight, layers, cancellationToken);

        using var image = Image.Load<Rgba32>(fullImage);

        // Calculate thumbnail dimensions maintaining aspect ratio
        var ratio = Math.Min((float)maxDimension / canvasWidth, (float)maxDimension / canvasHeight);
        var thumbWidth = (int)(canvasWidth * ratio);
        var thumbHeight = (int)(canvasHeight * ratio);

        // Resize
        image.Mutate(ctx => ctx.Resize(thumbWidth, thumbHeight));

        // Export to PNG
        using var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        return output.ToArray();
    }

    private static PixelColorBlendingMode GetPixelColorBlendingMode(LayerBlendMode blendMode)
    {
        return blendMode switch
        {
            LayerBlendMode.Normal => PixelColorBlendingMode.Normal,
            LayerBlendMode.Multiply => PixelColorBlendingMode.Multiply,
            LayerBlendMode.Screen => PixelColorBlendingMode.Screen,
            LayerBlendMode.Overlay => PixelColorBlendingMode.Overlay,
            LayerBlendMode.Darken => PixelColorBlendingMode.Darken,
            LayerBlendMode.Lighten => PixelColorBlendingMode.Lighten,
            _ => PixelColorBlendingMode.Normal
        };
    }
}
