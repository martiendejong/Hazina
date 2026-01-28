using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Hazina.Tools.Services.Images.LayeredImage.Exporters;

/// <summary>
/// Exports layered images to OpenRaster (ORA) format.
/// ORA is a simple ZIP-based format supported by GIMP, Krita, Paint.NET, and MyPaint.
/// </summary>
public class OraExporter : ILayeredImageExporter
{
    private readonly ILayerCompositor _compositor;

    public OraExporter(ILayerCompositor compositor)
    {
        _compositor = compositor;
    }

    public LayeredImageFormat Format => LayeredImageFormat.Ora;
    public string FileExtension => ".ora";

    public async Task<byte[]> ExportAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default)
    {
        using var outputStream = new MemoryStream();

        using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. Write mimetype file first (must be uncompressed and first entry)
            await WriteMimetypeAsync(archive, cancellationToken);

            // 2. Write layer PNG files
            var layerPaths = await WriteLayerFilesAsync(archive, layers, cancellationToken);

            // 3. Generate and write merged image
            var mergedImage = await _compositor.CompositeAsync(canvasWidth, canvasHeight, layers, cancellationToken);
            await WriteEntryAsync(archive, "mergedimage.png", mergedImage, CompressionLevel.Optimal, cancellationToken);

            // 4. Generate and write thumbnail
            var thumbnail = await _compositor.GenerateThumbnailAsync(canvasWidth, canvasHeight, layers, 256, cancellationToken);
            await WriteEntryAsync(archive, "Thumbnails/thumbnail.png", thumbnail, CompressionLevel.Optimal, cancellationToken);

            // 5. Write stack.xml
            var stackXml = GenerateStackXml(canvasWidth, canvasHeight, layers, layerPaths);
            await WriteEntryAsync(archive, "stack.xml", Encoding.UTF8.GetBytes(stackXml), CompressionLevel.Optimal, cancellationToken);
        }

        return outputStream.ToArray();
    }

    private static async Task WriteMimetypeAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        // mimetype must be the first entry and stored uncompressed
        var entry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        await using var stream = entry.Open();
        var bytes = Encoding.ASCII.GetBytes("image/openraster");
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task<List<string>> WriteLayerFilesAsync(
        ZipArchive archive,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken)
    {
        var layerPaths = new List<string>();

        for (var i = 0; i < layers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var layer = layers[i];
            var path = $"data/layer{i}.png";
            layerPaths.Add(path);

            // Convert layer data to PNG if not already
            byte[] pngData;
            using (var image = Image.Load<Rgba32>(layer.ImageData))
            {
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms, cancellationToken);
                pngData = ms.ToArray();
            }

            await WriteEntryAsync(archive, path, pngData, CompressionLevel.Optimal, cancellationToken);
        }

        return layerPaths;
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string path,
        byte[] data,
        CompressionLevel compressionLevel,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(path, compressionLevel);
        await using var stream = entry.Open();
        await stream.WriteAsync(data, cancellationToken);
    }

    private static string GenerateStackXml(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        List<string> layerPaths)
    {
        var image = new XElement("image",
            new XAttribute("version", "0.0.3"),
            new XAttribute("w", canvasWidth),
            new XAttribute("h", canvasHeight));

        var stack = new XElement("stack");

        // Layers are stored bottom to top in the definition, but ORA stack.xml
        // lists them top to bottom, so we reverse
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            var path = layerPaths[i];

            var layerElement = new XElement("layer",
                new XAttribute("name", layer.Name),
                new XAttribute("src", path),
                new XAttribute("x", layer.PositionX),
                new XAttribute("y", layer.PositionY),
                new XAttribute("opacity", (layer.Opacity / 255.0).ToString("F2")),
                new XAttribute("visibility", layer.Visible ? "visible" : "hidden"),
                new XAttribute("composite-op", BlendModeToCompositeOp(layer.BlendMode)));

            stack.Add(layerElement);
        }

        image.Add(stack);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            image);

        return doc.ToString();
    }

    private static string BlendModeToCompositeOp(LayerBlendMode blendMode)
    {
        return blendMode switch
        {
            LayerBlendMode.Normal => "svg:src-over",
            LayerBlendMode.Multiply => "svg:multiply",
            LayerBlendMode.Screen => "svg:screen",
            LayerBlendMode.Overlay => "svg:overlay",
            LayerBlendMode.Darken => "svg:darken",
            LayerBlendMode.Lighten => "svg:lighten",
            _ => "svg:src-over"
        };
    }
}
