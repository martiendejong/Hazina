using System.Text.Json;
using Hazina.Tools.Services.Images.LayeredImage.Exporters;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Globalization;

Console.WriteLine("=== LayeredImage Demo - Text Layer Test ===\n");

// Create a simple test with SolidColor background + Text layer
var definition = new LayeredImageDefinition
{
    Format = "pdn",
    Canvas = new CanvasDimensions { Width = 1920, Height = 1080 },
    Layers = new List<LayerDefinition>
    {
        new LayerDefinition
        {
            Name = "Background",
            Type = "SolidColor",
            Content = "#1a1a2e",  // Dark blue
            Size = new LayerSize { Width = 1920, Height = 1080 },
            Position = new LayerPosition { X = 0, Y = 0 }
        },
        new LayerDefinition
        {
            Name = "Gradient Overlay",
            Type = "SolidColor",
            Content = "#16213e",  // Slightly lighter blue
            Size = new LayerSize { Width = 1920, Height = 540 },
            Position = new LayerPosition { X = 0, Y = 540 },
            Opacity = 0.7f
        },
        new LayerDefinition
        {
            Name = "Accent Line",
            Type = "SolidColor",
            Content = "#e94560",  // Red accent
            Size = new LayerSize { Width = 400, Height = 4 },
            Position = new LayerPosition { X = 760, Y = 420 }
        },
        new LayerDefinition
        {
            Name = "Platform Title",
            Type = "Text",
            Content = "Sociaal Cognitief Platform",
            FontFamily = "Segoe UI",
            FontSize = 72,
            FontColor = "#FFFFFF",
            Size = new LayerSize { Width = 1200, Height = 120 },
            Position = new LayerPosition { X = 360, Y = 450 }
        },
        new LayerDefinition
        {
            Name = "Subtitle",
            Type = "Text",
            Content = "Connecting Minds Through Technology",
            FontFamily = "Segoe UI",
            FontSize = 32,
            FontColor = "#a0a0a0",
            Size = new LayerSize { Width = 800, Height = 60 },
            Position = new LayerPosition { X = 560, Y = 560 }
        }
    },
    Metadata = new ImageMetadata
    {
        Title = "Sociaal Cognitief Platform",
        Description = "Test layered image with text",
        Tags = new List<string> { "test", "text", "branding" }
    }
};

Console.WriteLine($"Canvas: {definition.Canvas.Width}x{definition.Canvas.Height}");
Console.WriteLine($"Layers: {definition.Layers.Count}");
Console.WriteLine();

// Generate layers manually (without AI service)
var layers = new List<LayerGenerationResult>();

foreach (var layerDef in definition.Layers)
{
    Console.WriteLine($"Generating layer: {layerDef.Name} ({layerDef.Type})");

    var result = new LayerGenerationResult
    {
        Name = layerDef.Name,
        PositionX = layerDef.Position.X,
        PositionY = layerDef.Position.Y,
        Width = layerDef.Size.Width,
        Height = layerDef.Size.Height,
        Opacity = (byte)(layerDef.Opacity * 255),
        BlendMode = layerDef.GetBlendMode(),
        Visible = layerDef.Visible
    };

    byte[] imageData;

    if (layerDef.Type == "SolidColor")
    {
        imageData = GenerateSolidColorLayer(layerDef);
    }
    else if (layerDef.Type == "Text")
    {
        imageData = GenerateTextLayer(layerDef);
    }
    else
    {
        Console.WriteLine($"  Skipping AI-generated layer (requires OpenAI)");
        continue;
    }

    result.ImageData = imageData;
    result.Success = true;
    layers.Add(result);

    Console.WriteLine($"  Size: {result.Width}x{result.Height}, Position: ({result.PositionX}, {result.PositionY})");
}

Console.WriteLine();

// Export to PDN
Console.WriteLine("Exporting to PDN format...");
var exporter = new PdnExporter();
var exportedData = await exporter.ExportAsync(
    definition.Canvas.Width,
    definition.Canvas.Height,
    layers,
    CancellationToken.None);

// Save to file
var outputPath = Path.Combine(Environment.CurrentDirectory, "sociaal-cognitief-platform.pdn");
await File.WriteAllBytesAsync(outputPath, exportedData);
Console.WriteLine($"Saved to: {outputPath}");
Console.WriteLine($"File size: {exportedData.Length / 1024} KB");

// Also create a flattened PNG preview
Console.WriteLine("\nCreating PNG preview...");
using var canvas = new Image<Rgba32>(definition.Canvas.Width, definition.Canvas.Height, new Rgba32(0, 0, 0, 255));

foreach (var layer in layers.Where(l => l.Success && l.Visible))
{
    using var layerImage = Image.Load<Rgba32>(layer.ImageData);

    // Apply opacity
    if (layer.Opacity < 255)
    {
        layerImage.Mutate(ctx => ctx.Opacity(layer.Opacity / 255f));
    }

    canvas.Mutate(ctx => ctx.DrawImage(layerImage, new Point(layer.PositionX, layer.PositionY), 1f));
}

var pngPath = Path.Combine(Environment.CurrentDirectory, "sociaal-cognitief-platform.png");
await canvas.SaveAsPngAsync(pngPath);
Console.WriteLine($"PNG preview saved to: {pngPath}");

Console.WriteLine("\n=== Done! ===");
Console.WriteLine($"Open {outputPath} in Paint.NET to see all layers separately.");

// Helper methods
static byte[] GenerateSolidColorLayer(LayerDefinition layerDef)
{
    var color = ParseHexColor(layerDef.Content);
    using var image = new Image<Rgba32>(layerDef.Size.Width, layerDef.Size.Height, color);
    using var output = new MemoryStream();
    image.SaveAsPng(output);
    return output.ToArray();
}

static byte[] GenerateTextLayer(LayerDefinition layerDef)
{
    var fontSize = layerDef.FontSize ?? 24;
    var fontColor = ParseHexColor(layerDef.FontColor ?? "#000000");

    using var image = new Image<Rgba32>(layerDef.Size.Width, layerDef.Size.Height, new Rgba32(0, 0, 0, 0));

    Font font;
    try
    {
        var fontFamily = layerDef.FontFamily ?? "Arial";
        if (SystemFonts.TryGet(fontFamily, out var family))
        {
            font = family.CreateFont(fontSize, FontStyle.Regular);
        }
        else
        {
            font = SystemFonts.Families.First().CreateFont(fontSize, FontStyle.Regular);
        }
    }
    catch
    {
        font = SystemFonts.Families.First().CreateFont(fontSize, FontStyle.Regular);
    }

    image.Mutate(ctx => ctx.DrawText(layerDef.Content, font, fontColor, new PointF(0, 0)));

    using var output = new MemoryStream();
    image.SaveAsPng(output);
    return output.ToArray();
}

static Rgba32 ParseHexColor(string hex)
{
    if (string.IsNullOrEmpty(hex)) return new Rgba32(0, 0, 0, 255);
    hex = hex.TrimStart('#');

    if (hex.Length == 6)
    {
        var r = byte.Parse(hex[..2], NumberStyles.HexNumber);
        var g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
        var b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
        return new Rgba32(r, g, b, 255);
    }

    if (hex.Length == 8)
    {
        var r = byte.Parse(hex[..2], NumberStyles.HexNumber);
        var g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
        var b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
        var a = byte.Parse(hex[6..8], NumberStyles.HexNumber);
        return new Rgba32(r, g, b, a);
    }

    return new Rgba32(0, 0, 0, 255);
}
