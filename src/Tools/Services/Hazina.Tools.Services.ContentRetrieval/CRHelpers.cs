using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DevGPTStore.Models;

namespace DevGPTStore.ContentRetrieval
{
    internal static class CRHelpers
    {
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static UploadedFile MakeUploadedFile(string path, string filename)
        {
            var fi = new FileInfo(path);
            return new UploadedFile
            {
                Extension = fi.Extension,
                Filename = filename,
                TextFilename = filename,
                TokenCount = 0,
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
    }
}
