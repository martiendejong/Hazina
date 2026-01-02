using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.Images.Operations;

/// <summary>
/// Base type for all image editing operations.
/// Concrete operations inherit from this abstract record.
/// </summary>
[JsonDerivedType(typeof(AddTextOperation), typeDiscriminator: "addText")]
[JsonDerivedType(typeof(CropOperation), typeDiscriminator: "crop")]
[JsonDerivedType(typeof(ResizeOperation), typeDiscriminator: "resize")]
[JsonDerivedType(typeof(RotateOperation), typeDiscriminator: "rotate")]
[JsonDerivedType(typeof(MaskReplaceOperation), typeDiscriminator: "maskReplace")]
[JsonDerivedType(typeof(AdjustBrightnessOperation), typeDiscriminator: "adjustBrightness")]
[JsonDerivedType(typeof(AdjustContrastOperation), typeDiscriminator: "adjustContrast")]
[JsonDerivedType(typeof(ApplyFilterOperation), typeDiscriminator: "applyFilter")]
public abstract record ImageEditOperation;

/// <summary>
/// Adds text to an image at specified coordinates.
/// </summary>
/// <param name="Text">The text content to add.</param>
/// <param name="X">Horizontal position in pixels from the left edge.</param>
/// <param name="Y">Vertical position in pixels from the top edge.</param>
/// <param name="FontSize">Font size in pixels.</param>
/// <param name="ColorHex">Color in hex format (e.g., "#FF0000" for red).</param>
/// <param name="FontFamily">Font family name. Defaults to "Arial".</param>
public record AddTextOperation(
    string Text,
    int X,
    int Y,
    int FontSize,
    string ColorHex,
    string FontFamily = "Arial"
) : ImageEditOperation;

/// <summary>
/// Crops an image to a specified rectangular region.
/// </summary>
/// <param name="X">Horizontal offset in pixels from the left edge.</param>
/// <param name="Y">Vertical offset in pixels from the top edge.</param>
/// <param name="Width">Width of the crop region in pixels.</param>
/// <param name="Height">Height of the crop region in pixels.</param>
public record CropOperation(
    int X,
    int Y,
    int Width,
    int Height
) : ImageEditOperation;

/// <summary>
/// Resizes an image to specified dimensions.
/// </summary>
/// <param name="Width">Target width in pixels.</param>
/// <param name="Height">Target height in pixels.</param>
/// <param name="MaintainAspectRatio">If true, maintains aspect ratio using the smaller dimension.</param>
public record ResizeOperation(
    int Width,
    int Height,
    bool MaintainAspectRatio = true
) : ImageEditOperation;

/// <summary>
/// Rotates an image by a specified angle.
/// </summary>
/// <param name="Degrees">Rotation angle in degrees (clockwise).</param>
public record RotateOperation(
    float Degrees
) : ImageEditOperation;

/// <summary>
/// Replaces a masked region using AI-based inpainting.
/// Requires an AI provider to be configured.
/// </summary>
/// <param name="MaskImage">A stream containing the mask image (white = area to replace).</param>
/// <param name="Prompt">Description of what should replace the masked area.</param>
public record MaskReplaceOperation(
    Stream MaskImage,
    string Prompt
) : ImageEditOperation;

/// <summary>
/// Adjusts the brightness of an image.
/// </summary>
/// <param name="Amount">Brightness adjustment (-1.0 to 1.0, where 0 is no change).</param>
public record AdjustBrightnessOperation(
    float Amount
) : ImageEditOperation;

/// <summary>
/// Adjusts the contrast of an image.
/// </summary>
/// <param name="Amount">Contrast adjustment (-1.0 to 1.0, where 0 is no change).</param>
public record AdjustContrastOperation(
    float Amount
) : ImageEditOperation;

/// <summary>
/// Applies a predefined filter to an image.
/// </summary>
/// <param name="FilterName">Name of the filter to apply.</param>
public record ApplyFilterOperation(
    ImageFilter FilterName
) : ImageEditOperation;

/// <summary>
/// Available image filters.
/// </summary>
public enum ImageFilter
{
    Grayscale,
    Sepia,
    Blur,
    Sharpen,
    Invert
}
