using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;

namespace Hazina.Tools.Services.Images.LayeredImage.Exporters;

/// <summary>
/// Exports layered images to Photoshop (PSD) format.
/// Note: PSD export is optional and requires Magick.NET library.
/// Currently implemented as a stub that falls back to ORA format.
/// </summary>
public class PsdExporter : ILayeredImageExporter
{
    private readonly ILayeredImageExporter _fallbackExporter;

    public PsdExporter(OraExporter fallbackExporter)
    {
        _fallbackExporter = fallbackExporter;
    }

    public LayeredImageFormat Format => LayeredImageFormat.Psd;
    public string FileExtension => ".psd";

    public async Task<byte[]> ExportAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default)
    {
        // PSD export requires Magick.NET which is not included by default.
        // Fall back to ORA format which is widely supported.
        // To enable PSD support:
        // 1. Add PackageReference to Magick.NET-Q8-AnyCPU
        // 2. Implement using ImageMagick.MagickImage with layers

        Console.WriteLine("[PsdExporter] PSD export not fully implemented, falling back to ORA format.");

        // For now, use ORA as fallback
        return await _fallbackExporter.ExportAsync(canvasWidth, canvasHeight, layers, cancellationToken);
    }
}
