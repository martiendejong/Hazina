namespace Hazina.Tools.Services.Images.LayeredImage.Models;

/// <summary>
/// Supported layered image export formats.
/// </summary>
public enum LayeredImageFormat
{
    /// <summary>
    /// Paint.NET format (PDN3) - Default format.
    /// Uses .NET BinaryFormatter serialization with Gzip compression.
    /// Best for Paint.NET users.
    /// </summary>
    Pdn,

    /// <summary>
    /// OpenRaster format (ORA) - Fallback format.
    /// Simple ZIP-based format supported by GIMP, Krita, Paint.NET, MyPaint.
    /// </summary>
    Ora,

    /// <summary>
    /// Photoshop format (PSD) - Optional format.
    /// Limited layer support via Magick.NET.
    /// </summary>
    Psd
}
