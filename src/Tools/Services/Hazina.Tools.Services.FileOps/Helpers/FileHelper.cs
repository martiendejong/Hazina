using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.Threading;
using Hazina.Tools.Data;
using HazinaStore.Models;

namespace Hazina.Tools.Services.FileOps.Helpers
{

public static class FileHelper
{
    // File-level locking to prevent concurrent access issues
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

    private static SemaphoreSlim GetFileLock(string filePath)
    {
        return _fileLocks.GetOrAdd(filePath.ToLowerInvariant(), _ => new SemaphoreSlim(1, 1));
    }
    // @todo remove this 
    public async static Task<List<string>> SplitFiles(string filePath, string filetext, int tokenCount, ProjectsRepository Projects, string openApiKey)
    {
        // Embedding generation removed to avoid hard dependency on specific LLM packages

        var files = new List<string>();
        if (tokenCount < 1000)
        {
            files.Add(filePath);
        }
        else
        {
            var extension = new FileInfo(filePath).Extension;
            var estNumParts = tokenCount / 500;
            var remainingText = filetext;
            var amount = remainingText.Length / estNumParts;
            //var remainingTokens = tokenCount;
            var filenr = 1;
            while (remainingText.Length > 0)
            {
                var ii = amount >= remainingText.Length ? remainingText.Length : remainingText.IndexOf("\n", amount);
                if (ii < 1)
                    ii = remainingText.Length;
                var text = remainingText.Substring(0, ii);
                var tempfile = new UploadedFile() { Content = text };
                tempfile.Embedding = new List<double>();

                var index2 = filePath.LastIndexOf(".");
                if (index2 < 0) index2 = filePath.Length;
                var partFilePath = $"{filePath.Substring(0, index2)}.p{filenr}{extension}.txt";
                File.WriteAllText(partFilePath, text);

                files.Add(partFilePath);
                filenr++;
                remainingText = remainingText.Substring(ii);

                //remainingTokens = tokenCounter.CountTokens(remainingText);
            }
        }
        return files;
    }

    public static async Task UpdateUploadedFileAsync(string projectFolder, string filename, string newLabel)
    {
        var uploadsFolder = Path.Combine(projectFolder, "Uploads");
        var listFilePath = Path.Combine(projectFolder, "uploadedFiles.json");

        var fileLock = GetFileLock(listFilePath);
        await fileLock.WaitAsync();
        try
        {
            var uploadedFilesList = await GetUploadedFilesListInternalAsync(listFilePath);

            var fileToUpdate = uploadedFilesList.FirstOrDefault(f => f.Filename == filename);
            fileToUpdate.Label = newLabel;

            var jsonContent = JsonSerializer.Serialize(uploadedFilesList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(listFilePath, jsonContent);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }


    public static UploadedFile GetScrapedFileDetails(string path, string filename, int tokencount)
    {
        var fi = new FileInfo(path);
        return new UploadedFile
        {
            Extension = fi.Extension,
            Filename = filename,
            TextFilename = filename,
            TokenCount = tokencount,
            Label = Path.GetFileNameWithoutExtension(filename)
        };
    }


    public static UploadedFile GetUploadedFileDetails(string path, string filename, int tokencount)
    {
        var fi = new FileInfo(path);
        return new UploadedFile
        {
            Extension = fi.Extension,
            Filename = filename,
            TextFilename = filename + ".txt",
            TokenCount = tokencount,
            Label = Path.GetFileNameWithoutExtension(filename)
        };
    }
    

    public static async Task UpdateUploadedFilesListAsync(string listFilePath, UploadedFile uploadedFile)
    {
        var fileLock = GetFileLock(listFilePath);
        await fileLock.WaitAsync();
        try
        {
            var uploadedFilesList = await GetUploadedFilesListInternalAsync(listFilePath);

            // Remove any existing entries with same TextFilename OR same Filename
            // This ensures no duplicates even if TextFilename is null or inconsistent
            uploadedFilesList = uploadedFilesList.Where(f =>
                f.TextFilename != uploadedFile.TextFilename &&
                f.Filename != uploadedFile.Filename
            ).ToList();

            // Always prepend the new file to the beginning of the list
            uploadedFilesList = uploadedFilesList.Prepend(uploadedFile).ToList();

            var jsonContent = JsonSerializer.Serialize(uploadedFilesList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(listFilePath, jsonContent);
        }
        finally
        {
            fileLock.Release();
        }
    }

    // Internal method without locking (for use within already-locked contexts)
    private static async Task<List<UploadedFile>> GetUploadedFilesListInternalAsync(string listFilePath)
    {
        if (File.Exists(listFilePath))
        {
            var existingContent = await File.ReadAllTextAsync(listFilePath);
            return JsonSerializer.Deserialize<List<UploadedFile>>(existingContent) ?? new List<UploadedFile>();
        }
        return new List<UploadedFile>();
    }

    public static async Task<List<UploadedFile>> GetUploadedFilesListAsync(string listFilePath)
    {
        var fileLock = GetFileLock(listFilePath);
        await fileLock.WaitAsync();
        try
        {
            return await GetUploadedFilesListInternalAsync(listFilePath);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public static async Task DeleteUploadedFileAsync(string filePath, string listFilePath, string fileNameToDelete)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var fileLock = GetFileLock(listFilePath);
        await fileLock.WaitAsync();
        try
        {
            var uploadedFilesList = await GetUploadedFilesListInternalAsync(listFilePath);
            var fileToDelete = uploadedFilesList.FirstOrDefault(f => f.Filename == fileNameToDelete);

            if (fileToDelete != null)
            {
                uploadedFilesList.Remove(fileToDelete);
                var jsonContent = JsonSerializer.Serialize(uploadedFilesList, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(listFilePath, jsonContent);
            }
        }
        finally
        {
            fileLock.Release();
        }
    }
}
}
