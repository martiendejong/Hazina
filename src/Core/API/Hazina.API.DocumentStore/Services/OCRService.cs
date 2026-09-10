using Hazina.API.DocumentStore.Configuration;
using Hazina.API.DocumentStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Tesseract;

namespace Hazina.API.DocumentStore.Services;

/// <summary>
/// OCR extraction result
/// </summary>
public class OCRResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Enhanced OCR service with better error handling and metadata extraction
/// </summary>
public class OCRService
{
    private readonly string _tessdataPath;
    private readonly ILogger<OCRService> _logger;
    private readonly double _minConfidenceThreshold = 0.7;

    // VAT amount patterns for multiple formats
    private static readonly Regex[] VatPatterns = new[]
    {
        new Regex(@"(?:VAT|BTW)\s*:?\s*€?\s*(\d+[.,]\d{2})", RegexOptions.IgnoreCase),
        new Regex(@"(?:VAT|BTW)\s*[Aa]mount\s*:?\s*€?\s*(\d+[.,]\d{2})", RegexOptions.IgnoreCase),
        new Regex(@"(?:Omzetbelasting|OB)\s*:?\s*€?\s*(\d+[.,]\d{2})", RegexOptions.IgnoreCase),
        new Regex(@"(?:VAT|BTW)\s+(\d+[.,]\d{2})", RegexOptions.IgnoreCase)
    };

    private static readonly Regex[] InvoiceNumberPatterns = new[]
    {
        new Regex(@"(?:Invoice|Factuur)\s*(?:Number|Nr|#)\s*:?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase),
        new Regex(@"(?:INV|FACT)\s*[:\-]?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase)
    };

    private static readonly Regex[] DatePatterns = new[]
    {
        new Regex(@"(?:Date|Datum)\s*:?\s*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})", RegexOptions.IgnoreCase),
        new Regex(@"(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})")
    };

    public OCRService(IOptions<DocumentStoreApiOptions> options, ILogger<OCRService> logger)
    {
        _tessdataPath = options.Value.TesseractDataPath;
        _logger = logger;
    }

    /// <summary>
    /// Process image with OCR and extract metadata
    /// </summary>
    public async Task<OCRResult> ProcessImageAsync(Stream imageStream, string language = "eng")
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Save stream to temp file
            using (var fileStream = File.Create(tempFile))
            {
                await imageStream.CopyToAsync(fileStream);
            }

            return await Task.Run(() => ProcessImageFile(tempFile, language));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR processing failed");
            return new OCRResult
            {
                Success = false,
                ErrorMessage = $"OCR processing failed: {ex.Message}"
            };
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Process image file with OCR
    /// </summary>
    private OCRResult ProcessImageFile(string imagePath, string language)
    {
        try
        {
            // Support multiple languages (Dutch and English)
            var langString = language.Contains('+') ? language : $"{language}+eng";

            using var engine = new TesseractEngine(_tessdataPath, langString, EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);

            var text = page.GetText();
            var confidence = page.GetMeanConfidence();

            // Check confidence threshold
            if (confidence < _minConfidenceThreshold)
            {
                _logger.LogWarning("OCR confidence {Confidence} below threshold {Threshold}",
                    confidence, _minConfidenceThreshold);
            }

            // Extract metadata
            var metadata = ExtractMetadata(text);

            return new OCRResult
            {
                ExtractedText = text,
                ConfidenceScore = confidence,
                Metadata = metadata,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract OCR failed for file {FilePath}", imagePath);
            return new OCRResult
            {
                Success = false,
                ErrorMessage = $"Tesseract OCR failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extract structured metadata from OCR text
    /// </summary>
    private Dictionary<string, object> ExtractMetadata(string text)
    {
        var metadata = new Dictionary<string, object>();

        // Extract VAT amount
        foreach (var pattern in VatPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success && match.Groups.Count > 1)
            {
                var vatAmount = match.Groups[1].Value.Replace(',', '.');
                if (decimal.TryParse(vatAmount, out var amount))
                {
                    metadata["vatAmount"] = amount;
                    metadata["vatAmountRaw"] = match.Groups[1].Value;
                    break;
                }
            }
        }

        // Extract invoice number
        foreach (var pattern in InvoiceNumberPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success && match.Groups.Count > 1)
            {
                metadata["invoiceNumber"] = match.Groups[1].Value;
                break;
            }
        }

        // Extract date
        foreach (var pattern in DatePatterns)
        {
            var match = pattern.Match(text);
            if (match.Success && match.Groups.Count > 1)
            {
                metadata["dateFound"] = match.Groups[1].Value;
                break;
            }
        }

        // Add word count and line count
        metadata["wordCount"] = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        metadata["lineCount"] = text.Split('\n').Length;

        return metadata;
    }

    /// <summary>
    /// Validate OCR result quality
    /// </summary>
    public bool IsValidOCRResult(OCRResult result)
    {
        if (!result.Success)
            return false;

        if (result.ConfidenceScore < _minConfidenceThreshold)
            return false;

        if (string.IsNullOrWhiteSpace(result.ExtractedText))
            return false;

        return true;
    }
}
