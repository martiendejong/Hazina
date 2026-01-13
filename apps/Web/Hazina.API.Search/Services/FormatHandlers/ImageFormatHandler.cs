using Hazina.API.Search.Models;
using Tesseract;

namespace Hazina.API.Search.Services.FormatHandlers;

/// <summary>
/// Handles image documents with OCR
/// </summary>
public class ImageFormatHandler : IFormatHandler
{
    private readonly string _tessdataPath;

    public ImageFormatHandler(IConfiguration configuration)
    {
        _tessdataPath = configuration["Tesseract:DataPath"] ?? "./tessdata";
    }

    public bool CanHandle(string mimeType, string extension)
    {
        return mimeType.StartsWith("image/") ||
               extension == ".png" ||
               extension == ".jpg" ||
               extension == ".jpeg" ||
               extension == ".gif" ||
               extension == ".bmp" ||
               extension == ".tiff";
    }

    public async Task<ExtractedContent> ExtractAsync(Stream stream, string filename)
    {
        // Save to temp file for Tesseract
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream);
            }

            string text = "";
            try
            {
                // Try to use Tesseract if available
                using (var engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default))
                using (var img = Pix.LoadFromFile(tempFile))
                using (var page = engine.Process(img))
                {
                    text = page.GetText();
                }
            }
            catch
            {
                // Fallback: return metadata-only extraction
                text = $"[Image file: {filename}. OCR not available or failed. Please ensure Tesseract is installed.]";
            }

            return new ExtractedContent
            {
                Text = text,
                MimeType = "image/*",
                PageCount = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["filename"] = filename,
                    ["format"] = "image",
                    ["ocrUsed"] = !string.IsNullOrEmpty(text)
                }
            };
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
