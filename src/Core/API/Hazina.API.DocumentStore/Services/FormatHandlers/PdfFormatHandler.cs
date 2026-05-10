using Hazina.API.DocumentStore.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System.Text;

namespace Hazina.API.DocumentStore.Services.FormatHandlers;

/// <summary>
/// Handles PDF documents
/// </summary>
public class PdfFormatHandler : IFormatHandler
{
    public bool CanHandle(string mimeType, string extension)
    {
        return mimeType == "application/pdf" || extension == ".pdf";
    }

    public async Task<ExtractedContent> ExtractAsync(Stream stream, string filename)
    {
        var text = new StringBuilder();
        var pages = new List<string>();

        using (var pdfReader = new PdfReader(stream))
        using (var pdfDoc = new PdfDocument(pdfReader))
        {
            var numPages = pdfDoc.GetNumberOfPages();

            for (int i = 1; i <= numPages; i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageText = PdfTextExtractor.GetTextFromPage(page);

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    text.AppendLine(pageText);
                    pages.Add(pageText);
                }
            }
        }

        return await Task.FromResult(new ExtractedContent
        {
            Text = text.ToString(),
            Pages = pages,
            MimeType = "application/pdf",
            PageCount = pages.Count,
            Metadata = new Dictionary<string, object>
            {
                ["filename"] = filename,
                ["format"] = "pdf",
                ["pageCount"] = pages.Count
            }
        });
    }
}
