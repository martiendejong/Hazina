using System.IO;
using System.Threading.Tasks;
using Spire.Doc;
using Spire.Doc.Documents;
using Spire.Xls;
using System.Text;
using System;
using OpenAI.Chat;
using PDFiumCore;
using PDFiumCoreDemo;
using HeyRed.Mime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

public class TextFileExtractor
{
    public ILLMClient Api;
    public TextFileExtractor(ILLMClient api)
    {
        Api = api;
    }

    public async Task ExtractTextFromDocument(string filePath, string textFilePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        switch (extension)
        {
            case ".gif":
            case ".bmp":
            case ".webp":
            case ".jpg":
            case ".jpeg":
            case ".png":
                await ExtractTextFromImage(filePath, textFilePath);
                break;
            case ".pdf":
                await ExtractTextFromPdf(filePath, textFilePath);
                break;
            case ".doc":
            case ".docx":
                await ExtractTextFromWord(filePath, textFilePath);
                break;
            case ".xlsx":
                await ExtractTextFromExcel(filePath, textFilePath);
                break;
            case ".txt":
                File.Copy(filePath, textFilePath, true);
                break;
            default:
                throw new NotSupportedException($"File type {extension} is not supported.");
        }
    }

    public static readonly string[] SupportedMimeTypes =
    {
        "image/png", "image/jpeg", "image/webp", "image/gif", "image/bmp", "image/tiff"
    };

    public async Task ExtractTextFromImage(string filePath, string textFilePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("File not found.");
            return;
        }

        string mimeType = MimeTypesMap.GetMimeType(filePath);
        if (!Array.Exists(SupportedMimeTypes, m => m == mimeType))
        {
            Console.WriteLine($"Unsupported image type: {mimeType}");
            return;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        var binaryData = BinaryData.FromBytes(fileData);
        var name = Path.GetFileName(filePath);
        var data = new ImageData { BinaryData = binaryData, MimeType = mimeType, Name = name };

        var messages = new List<HazinaChatMessage>() { new HazinaChatMessage(HazinaMessageRole.User, "Geef een omschrijving van de afbeelding. Als er tekst in de afbeelding voorkomt geef dan de tekst letterlijk weer in de omschrijving. Je reactie bestaat enkel uit de door jouw gegeven omschrijving gevolgd door de letterlijke tekst.") };
        
        var tokenSource = new CancellationTokenSource();
        var reaction = await Api.GetResponse(messages, HazinaChatResponseFormat.CreateTextFormat(), null, [data], tokenSource.Token);

        await File.WriteAllTextAsync(textFilePath, reaction.Result);
    }

    public async Task ExtractTextFromPdf(string filePath, string textFilePath)
    {
        await PdfToImage.ExtractTextFromPdf(filePath, textFilePath);
    }

    public async Task AnalyzePdfAsImages(string filePath, string textFilePath)
    {
        var images = new List<ImageData>();
        var pages = PdfToImage.RenderPageToImage(filePath);

        for(var i = 0; i < pages; ++i)
        {
            using (var stream = new MemoryStream())
            {
                var pngPath = filePath + "_" + i + ".png";
                using (var image = Image.Load(pngPath))
                {
                    image.Save(stream, new PngEncoder());
                    var binaryData = BinaryData.FromBytes(stream.ToArray());
                    images.Add(new ImageData { BinaryData = binaryData, MimeType = "image/png", Name = "" });
                }
            }
        }

        var response = "";
        for(var i = 0; i < pages; i = i + 1)
        {
            var messages = new List<HazinaChatMessage>() { new HazinaChatMessage(HazinaMessageRole.User, "Geef een omschrijving van de afbeelding. Als er tekst in de afbeelding voorkomt geef dan de tekst letterlijk weer in de omschrijving. Je reactie bestaat enkel uit de door jouw gegeven omschrijving gevolgd door de letterlijke tekst.") };
            var tokenSource = new CancellationTokenSource();
            var reaction = await Api.GetResponse(messages, HazinaChatResponseFormat.CreateTextFormat(), null, images.Skip(i).Take(1).ToList(), tokenSource.Token);
            response += "Analyse pagina " + i + ":\n" + reaction + "\n\n";
        }

        await File.WriteAllTextAsync(textFilePath, response);
        return;
    }

    private static string RemoveMessage(string result)
    {
        var i = result.IndexOf("Spire.Doc for .NET.");
        if (i >= 0)
            result = result.Substring(i + 19);
        return result;
    }

    private Task ExtractTextFromWord(string filePath, string textFilePath)
    {
        var document = new Document();
        document.LoadFromFile(filePath);
        string text = document.GetText();
        text = RemoveMessage(text);
        return File.WriteAllTextAsync(textFilePath, text);
    }

    private Task ExtractTextFromExcel(string filePath, string textFilePath)
    {
        var workbook = new Workbook();
        workbook.LoadFromFile(filePath);
        var sb = new StringBuilder();

        foreach (var sheet in workbook.Worksheets)
        {
            sb.AppendLine(sheet.Name);
            foreach (var row in sheet.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    sb.Append(cell.DisplayedText + "\t");
                }
                sb.AppendLine();
            }
        }
        var text = RemoveMessage(sb.ToString());

        return File.WriteAllTextAsync(textFilePath, text);
    }
}
