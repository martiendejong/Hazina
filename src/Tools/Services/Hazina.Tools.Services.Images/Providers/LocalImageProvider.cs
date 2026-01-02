using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace Hazina.Tools.Services.Images.Providers;

/// <summary>
/// Local image provider using SixLabors.ImageSharp.
/// Handles deterministic operations that don't require AI.
/// </summary>
public class LocalImageProvider : IImageProvider
{
    private static readonly Lazy<FontCollection> FontCollection = new(() =>
    {
        var collection = new FontCollection();
        // System fonts are accessed via SystemFonts
        return collection;
    });

    /// <inheritdoc />
    public string Name => "Local";

    /// <inheritdoc />
    public bool CanHandle(ImageEditOperation operation)
    {
        return operation switch
        {
            AddTextOperation => true,
            CropOperation => true,
            ResizeOperation => true,
            RotateOperation => true,
            AdjustBrightnessOperation => true,
            AdjustContrastOperation => true,
            ApplyFilterOperation => true,
            MaskReplaceOperation => false, // Requires AI
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
        // Reset input stream position
        if (image.CanSeek)
        {
            image.Position = 0;
        }

        using var sourceImage = await Image.LoadAsync<Rgba32>(image, cancellationToken);

        ApplyOperation(sourceImage, operation);

        var outputStream = new MemoryStream();
        await sourceImage.SaveAsPngAsync(outputStream, cancellationToken);
        outputStream.Position = 0;

        return outputStream;
    }

    private void ApplyOperation(Image<Rgba32> image, ImageEditOperation operation)
    {
        switch (operation)
        {
            case AddTextOperation addText:
                ApplyAddText(image, addText);
                break;

            case CropOperation crop:
                ApplyCrop(image, crop);
                break;

            case ResizeOperation resize:
                ApplyResize(image, resize);
                break;

            case RotateOperation rotate:
                ApplyRotate(image, rotate);
                break;

            case AdjustBrightnessOperation brightness:
                ApplyBrightness(image, brightness);
                break;

            case AdjustContrastOperation contrast:
                ApplyContrast(image, contrast);
                break;

            case ApplyFilterOperation filter:
                ApplyFilter(image, filter);
                break;

            default:
                throw new NotSupportedException(
                    $"Operation '{operation.GetType().Name}' is not supported by LocalImageProvider.");
        }
    }

    private void ApplyAddText(Image<Rgba32> image, AddTextOperation op)
    {
        // Try to get system font, fall back to default
        Font font;
        try
        {
            font = SystemFonts.CreateFont(op.FontFamily, op.FontSize);
        }
        catch
        {
            // Fallback to first available system font
            var families = SystemFonts.Collection.Families.ToList();
            var fallbackFamily = families.FirstOrDefault();
            font = fallbackFamily.CreateFont(op.FontSize);
        }

        var color = ParseColor(op.ColorHex);

        image.Mutate(ctx => ctx.DrawText(op.Text, font, color, new PointF(op.X, op.Y)));
    }

    private void ApplyCrop(Image<Rgba32> image, CropOperation op)
    {
        var rect = new Rectangle(op.X, op.Y, op.Width, op.Height);
        image.Mutate(ctx => ctx.Crop(rect));
    }

    private void ApplyResize(Image<Rgba32> image, ResizeOperation op)
    {
        var resizeOptions = new ResizeOptions
        {
            Size = new Size(op.Width, op.Height),
            Mode = op.MaintainAspectRatio ? ResizeMode.Max : ResizeMode.Stretch
        };
        image.Mutate(ctx => ctx.Resize(resizeOptions));
    }

    private void ApplyRotate(Image<Rgba32> image, RotateOperation op)
    {
        image.Mutate(ctx => ctx.Rotate(op.Degrees));
    }

    private void ApplyBrightness(Image<Rgba32> image, AdjustBrightnessOperation op)
    {
        // ImageSharp brightness: 1.0 = no change, 0 = black, >1 = brighter
        // Convert from -1 to 1 range to 0 to 2 range
        var brightness = 1.0f + op.Amount;
        image.Mutate(ctx => ctx.Brightness(brightness));
    }

    private void ApplyContrast(Image<Rgba32> image, AdjustContrastOperation op)
    {
        // ImageSharp contrast: 1.0 = no change, 0 = gray, >1 = more contrast
        // Convert from -1 to 1 range to 0 to 2 range
        var contrast = 1.0f + op.Amount;
        image.Mutate(ctx => ctx.Contrast(contrast));
    }

    private void ApplyFilter(Image<Rgba32> image, ApplyFilterOperation op)
    {
        switch (op.FilterName)
        {
            case ImageFilter.Grayscale:
                image.Mutate(ctx => ctx.Grayscale());
                break;

            case ImageFilter.Sepia:
                image.Mutate(ctx => ctx.Sepia());
                break;

            case ImageFilter.Blur:
                image.Mutate(ctx => ctx.GaussianBlur(3f));
                break;

            case ImageFilter.Sharpen:
                image.Mutate(ctx => ctx.GaussianSharpen(3f));
                break;

            case ImageFilter.Invert:
                image.Mutate(ctx => ctx.Invert());
                break;

            default:
                throw new NotSupportedException($"Filter '{op.FilterName}' is not supported.");
        }
    }

    private static Color ParseColor(string hex)
    {
        // Remove # prefix if present
        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return Color.FromRgb(r, g, b);
        }

        if (hex.Length == 8)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            var a = Convert.ToByte(hex[6..8], 16);
            return Color.FromRgba(r, g, b, a);
        }

        return Color.Black;
    }
}
