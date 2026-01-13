using Hazina.API.Search.Models;

namespace Hazina.API.Search.Services.FormatHandlers;

/// <summary>
/// Handles plain text and chat messages
/// </summary>
public class TextFormatHandler : IFormatHandler
{
    public bool CanHandle(string mimeType, string extension)
    {
        return mimeType.StartsWith("text/") ||
               extension == ".txt" ||
               extension == ".md" ||
               extension == ".json" ||
               extension == ".xml" ||
               extension == ".csv";
    }

    public async Task<ExtractedContent> ExtractAsync(Stream stream, string filename)
    {
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        return new ExtractedContent
        {
            Text = text,
            MimeType = "text/plain",
            PageCount = 1,
            Metadata = new Dictionary<string, object>
            {
                ["filename"] = filename,
                ["format"] = "text"
            }
        };
    }
}
