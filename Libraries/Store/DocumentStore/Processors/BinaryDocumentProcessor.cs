using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class BinaryDocumentProcessor
{
    private readonly ILLMClient _llmClient;

    public BinaryDocumentProcessor(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    public bool IsBinary(string mimeType)
    {
        var textTypes = new[]
        {
            "text/",
            "application/json",
            "application/xml",
            "application/javascript",
            "application/x-javascript",
            "application/x-sh"
        };

        foreach (var textType in textTypes)
        {
            if (mimeType.StartsWith(textType, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<string> ExtractText(byte[] content, string mimeType)
    {
        // Try to extract text based on mime type
        if (mimeType.StartsWith("text/", StringComparison.InvariantCultureIgnoreCase))
        {
            return Encoding.UTF8.GetString(content);
        }

        // For PDFs, images, and other binaries, we'll use AI to generate a summary
        return await GenerateSummary(content, mimeType);
    }

    public async Task<string> GenerateSummary(byte[] content, string mimeType)
    {
        // For images, use vision API if available
        if (IsImage(mimeType))
        {
            return await GenerateImageSummary(content, mimeType);
        }

        // For PDFs and other documents, describe the file structure
        if (IsPdf(mimeType))
        {
            return await GeneratePdfSummary(content);
        }

        // For other binary files, provide basic info
        return GenerateBasicBinaryInfo(content, mimeType);
    }

    private bool IsImage(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase);
    }

    private bool IsPdf(string mimeType)
    {
        return mimeType.Equals("application/pdf", StringComparison.InvariantCultureIgnoreCase);
    }

    private async Task<string> GenerateImageSummary(byte[] content, string mimeType)
    {
        try
        {
            var messages = new List<DevGPTChatMessage>
            {
                new DevGPTChatMessage(
                    DevGPTMessageRole.User,
                    "Describe this image in detail. Include any text visible in the image, the main subjects, colors, composition, and any other relevant details."
                )
            };

            var imageData = new ImageData
            {
                MimeType = mimeType,
                BinaryData = new BinaryData(content)
            };

            var response = await _llmClient.GetResponse(messages, DevGPTChatResponseFormat.Text, null, new List<ImageData> { imageData }, CancellationToken.None);
            return response.Result;
        }
        catch
        {
            return $"Image file ({mimeType}), size: {content.Length} bytes. Vision analysis unavailable.";
        }
    }

    private async Task<string> GeneratePdfSummary(byte[] content)
    {
        // Basic PDF info - could be extended with actual PDF parsing
        var summary = new StringBuilder();
        summary.AppendLine("PDF Document");
        summary.AppendLine($"Size: {content.Length} bytes");

        // Try to detect PDF version
        if (content.Length > 8)
        {
            var header = Encoding.ASCII.GetString(content, 0, Math.Min(8, content.Length));
            if (header.StartsWith("%PDF-"))
            {
                summary.AppendLine($"PDF Version: {header}");
            }
        }

        // Future: Add actual PDF text extraction here
        summary.AppendLine("Note: Full text extraction not yet implemented. Use a PDF library for detailed content.");

        return summary.ToString();
    }

    private string GenerateBasicBinaryInfo(byte[] content, string mimeType)
    {
        var info = new StringBuilder();
        info.AppendLine($"Binary file: {mimeType}");
        info.AppendLine($"Size: {content.Length} bytes");

        // Add some basic file signature detection
        if (content.Length >= 4)
        {
            var signature = BitConverter.ToString(content, 0, Math.Min(4, content.Length));
            info.AppendLine($"File signature: {signature}");
        }

        return info.ToString();
    }

    public string DetectMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".cs" => "text/x-csharp",
            ".java" => "text/x-java",
            ".py" => "text/x-python",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            _ => "application/octet-stream"
        };
    }
}
