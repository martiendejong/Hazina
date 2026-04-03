using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Hazina.API.DocumentStore.Models;
using System.Text;

namespace Hazina.API.DocumentStore.Services.FormatHandlers;

/// <summary>
/// Handles Microsoft Word (.docx) documents
/// </summary>
public class DocxFormatHandler : IFormatHandler
{
    public bool CanHandle(string mimeType, string extension)
    {
        return mimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
               extension == ".docx";
    }

    public async Task<ExtractedContent> ExtractAsync(Stream stream, string filename)
    {
        var text = new StringBuilder();
        var pageCount = 0;

        using (var doc = WordprocessingDocument.Open(stream, false))
        {
            if (doc.MainDocumentPart?.Document.Body != null)
            {
                foreach (var para in doc.MainDocumentPart.Document.Body.Elements<Paragraph>())
                {
                    var paraText = para.InnerText;
                    if (!string.IsNullOrWhiteSpace(paraText))
                    {
                        text.AppendLine(paraText);
                    }
                }

                pageCount = doc.MainDocumentPart.Document.Body.Elements<Paragraph>().Count() / 40 + 1;
            }
        }

        return await Task.FromResult(new ExtractedContent
        {
            Text = text.ToString(),
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            PageCount = pageCount,
            Metadata = new Dictionary<string, object>
            {
                ["filename"] = filename,
                ["format"] = "docx"
            }
        });
    }
}
