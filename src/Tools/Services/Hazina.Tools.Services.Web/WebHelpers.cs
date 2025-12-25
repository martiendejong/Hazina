using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HazinaStore.Models;

namespace Hazina.Tools.Services.Web
{
    internal static class WebHelpers
    {
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static UploadedFile MakeUploadedFile(string path, string filename, int tokenCount)
        {
            var fi = new FileInfo(path);
            return new UploadedFile
            {
                Extension = fi.Extension,
                Filename = filename,
                TextFilename = filename + ".txt",
                TokenCount = tokenCount,
                Label = Path.GetFileNameWithoutExtension(filename)
            };
        }

        public static async Task UpdateUploadedFilesListAsync(string listFilePath, UploadedFile uploadedFile)
        {
            List<UploadedFile> list;
            if (File.Exists(listFilePath))
            {
                var existing = await File.ReadAllTextAsync(listFilePath);
                list = JsonSerializer.Deserialize<List<UploadedFile>>(existing) ?? new List<UploadedFile>();
            }
            else
            {
                list = new List<UploadedFile>();
            }
            list.RemoveAll(f => f.TextFilename == uploadedFile.TextFilename || f.Filename == uploadedFile.Filename);
            list.Insert(0, uploadedFile);
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(listFilePath, json);
        }

        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, text.Length / 4);
        }

        public static Task<List<string>> SplitFiles(string filePath, string content)
        {
            // Minimal no-op splitter: keep single file
            return Task.FromResult(new List<string> { filePath });
        }
    }
}
