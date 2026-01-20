using System.Globalization;
using System.Text.Json;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Chat;
using Hazina.Tools.Services.FileOps.Helpers;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace Hazina.Tools.Services.Images.LayeredImage.Services;

/// <summary>
/// Main service for generating layered images.
/// Orchestrates layer generation, composition, and export.
/// </summary>
public class LayeredImageService : ILayeredImageService
{
    private readonly IChatImageService _imageService;
    private readonly IGeneratedImageRepository _imageRepository;
    private readonly ProjectsRepository _projects;
    private readonly ProjectFileLocator _fileLocator;
    private readonly IReadOnlyDictionary<LayeredImageFormat, ILayeredImageExporter> _exporters;

    public LayeredImageService(
        IChatImageService imageService,
        IGeneratedImageRepository imageRepository,
        ProjectsRepository projects,
        IEnumerable<ILayeredImageExporter> exporters)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _imageRepository = imageRepository ?? throw new ArgumentNullException(nameof(imageRepository));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);

        // Build exporter dictionary
        _exporters = exporters.ToDictionary(e => e.Format, e => e);
    }

    public async Task<LayeredImageResult> GenerateFromJsonAsync(
        string projectId,
        string? userId,
        string jsonDefinition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<LayeredImageDefinition>(jsonDefinition, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (definition == null)
            {
                return new LayeredImageResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse layer definition JSON"
                };
            }

            return await GenerateAsync(projectId, userId, definition, cancellationToken);
        }
        catch (JsonException ex)
        {
            return new LayeredImageResult
            {
                Success = false,
                ErrorMessage = $"Invalid JSON format: {ex.Message}"
            };
        }
    }

    public async Task<LayeredImageResult> GenerateAsync(
        string projectId,
        string? userId,
        LayeredImageDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var result = new LayeredImageResult
        {
            Format = definition.GetFormat()
        };

        try
        {
            // Validate definition
            if (definition.Canvas.Width <= 0 || definition.Canvas.Height <= 0)
            {
                return new LayeredImageResult
                {
                    Success = false,
                    ErrorMessage = "Invalid canvas dimensions"
                };
            }

            if (definition.Layers.Count == 0)
            {
                return new LayeredImageResult
                {
                    Success = false,
                    ErrorMessage = "No layers defined"
                };
            }

            // Get the appropriate exporter
            var format = definition.GetFormat();
            if (!_exporters.TryGetValue(format, out var exporter))
            {
                // Fall back to ORA if requested format not available
                if (!_exporters.TryGetValue(LayeredImageFormat.Ora, out exporter))
                {
                    return new LayeredImageResult
                    {
                        Success = false,
                        ErrorMessage = $"No exporter available for format: {format}"
                    };
                }
                format = LayeredImageFormat.Ora;
            }

            // Generate each layer
            var layers = new List<LayerGenerationResult>();
            foreach (var layerDef in definition.Layers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var layerResult = await GenerateLayerAsync(projectId, userId, layerDef, cancellationToken);
                layers.Add(layerResult);

                result.Layers.Add(new LayerResultInfo
                {
                    Name = layerDef.Name,
                    Width = layerResult.Width,
                    Height = layerResult.Height,
                    Success = layerResult.Success,
                    ErrorMessage = layerResult.ErrorMessage
                });
            }

            // Check if any layer failed critically
            if (layers.All(l => !l.Success))
            {
                return new LayeredImageResult
                {
                    Success = false,
                    ErrorMessage = "All layers failed to generate",
                    Layers = result.Layers
                };
            }

            // Export to the selected format
            var exportedData = await exporter.ExportAsync(
                definition.Canvas.Width,
                definition.Canvas.Height,
                layers.Where(l => l.Success).ToList(),
                cancellationToken);

            // Generate filename and save
            var fileName = $"{Guid.NewGuid():N}{exporter.FileExtension}";
            await _imageRepository.SaveImageAsync(projectId, userId, fileName, exportedData);

            // Also save to uploads folder
            try
            {
                var uploadsFolder = Path.Combine(_fileLocator.GetProjectFolder(projectId), "uploads");
                FileHelper.EnsureDirectoryExists(uploadsFolder);
                var uploadPath = Path.Combine(uploadsFolder, fileName);
                await File.WriteAllBytesAsync(uploadPath, exportedData, cancellationToken);

                var listFilePath = Path.Combine(_fileLocator.GetProjectFolder(projectId), "uploadedFiles.json");
                var uploadedFile = FileHelper.GetUploadedFileDetails(uploadPath, fileName, 0);
                await FileHelper.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LayeredImageService] Failed to sync to uploads: {ex.Message}");
            }

            // Save metadata
            var metadata = new GeneratedImageInfo
            {
                ProjectId = projectId,
                FileName = fileName,
                Prompt = $"Layered image with {definition.Layers.Count} layers",
                Model = "LayeredImage",
                UserId = userId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                Tags = definition.Metadata?.Tags ?? new List<string> { "layered", "composite" },
                Description = definition.Metadata?.Description ?? definition.Metadata?.Title
            };
            _imageRepository.Add(metadata, projectId, userId);

            result.Success = true;
            result.FileName = fileName;
            result.FileUrl = $"/api/uploadeddocuments/file/{Uri.EscapeDataString(projectId)}/{Uri.EscapeDataString(fileName)}";
            result.Format = format;

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LayeredImageResult
            {
                Success = false,
                ErrorMessage = $"Failed to generate layered image: {ex.Message}",
                Layers = result.Layers
            };
        }
    }

    private async Task<LayerGenerationResult> GenerateLayerAsync(
        string projectId,
        string? userId,
        LayerDefinition layerDef,
        CancellationToken cancellationToken)
    {
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

        try
        {
            var layerType = layerDef.GetLayerType();

            byte[] imageData = layerType switch
            {
                LayerType.Generated => await GenerateAiLayerAsync(layerDef, cancellationToken),
                LayerType.Uploaded => await LoadUploadedLayerAsync(projectId, layerDef, cancellationToken),
                LayerType.SolidColor => GenerateSolidColorLayer(layerDef),
                LayerType.Text => GenerateTextLayer(layerDef),
                _ => throw new NotSupportedException($"Layer type {layerType} not supported")
            };

            result.ImageData = imageData;
            result.Success = true;

            // Update actual dimensions from the generated image
            using var image = Image.Load(imageData);
            result.Width = image.Width;
            result.Height = image.Height;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"[LayeredImageService] Layer '{layerDef.Name}' failed: {ex.Message}");
        }

        return result;
    }

    private async Task<byte[]> GenerateAiLayerAsync(LayerDefinition layerDef, CancellationToken cancellationToken)
    {
        // Use the ChatImageService to generate the image
        return await _imageService.GenerateImageBytesAsync(
            layerDef.Content,
            ImageModel.GptImage, // Default to GPT Image
            layerDef.Size.Width,
            layerDef.Size.Height,
            cancellationToken);
    }

    private async Task<byte[]> LoadUploadedLayerAsync(string projectId, LayerDefinition layerDef, CancellationToken cancellationToken)
    {
        var uploadsFolder = Path.Combine(_fileLocator.GetProjectFolder(projectId), "uploads");
        var filePath = Path.Combine(uploadsFolder, layerDef.Content);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Uploaded file not found: {layerDef.Content}");
        }

        var fileData = await File.ReadAllBytesAsync(filePath, cancellationToken);

        // Resize to requested dimensions if different
        using var image = Image.Load<Rgba32>(fileData);
        if (image.Width != layerDef.Size.Width || image.Height != layerDef.Size.Height)
        {
            image.Mutate(ctx => ctx.Resize(layerDef.Size.Width, layerDef.Size.Height));
        }

        using var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        return output.ToArray();
    }

    private static byte[] GenerateSolidColorLayer(LayerDefinition layerDef)
    {
        // Parse hex color
        var color = ParseHexColor(layerDef.Content);

        using var image = new Image<Rgba32>(layerDef.Size.Width, layerDef.Size.Height, color);
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static byte[] GenerateTextLayer(LayerDefinition layerDef)
    {
        var fontSize = layerDef.FontSize ?? 24;
        var fontColor = ParseHexColor(layerDef.FontColor ?? "#000000");

        // Create transparent image
        using var image = new Image<Rgba32>(layerDef.Size.Width, layerDef.Size.Height, new Rgba32(0, 0, 0, 0));

        // Try to get the font
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
                // Fallback to first available system font
                font = SystemFonts.Families.First().CreateFont(fontSize, FontStyle.Regular);
            }
        }
        catch
        {
            // Ultimate fallback
            font = SystemFonts.Families.First().CreateFont(fontSize, FontStyle.Regular);
        }

        // Draw text
        image.Mutate(ctx =>
        {
            ctx.DrawText(layerDef.Content, font, fontColor, new PointF(0, 0));
        });

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static Rgba32 ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return new Rgba32(0, 0, 0, 255);

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
}
