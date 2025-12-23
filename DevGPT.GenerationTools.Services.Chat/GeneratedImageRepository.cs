using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public class GeneratedImageRepository : IGeneratedImageRepository
    {
        private const string FolderName = "generatedimages";
        private const string MetadataFileName = "generatedimages.json";
        private readonly ProjectFileLocator _fileLocator;
        private readonly object _metadataLock = new();

        public GeneratedImageRepository(ProjectsRepository projects, ProjectFileLocator fileLocator)
        {
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        public string GetFolder(string projectId, string? userId)
        {
            var baseFolder = string.IsNullOrWhiteSpace(userId)
                ? _fileLocator.GetProjectFolder(projectId)
                : _fileLocator.GetProjectFolder(projectId, userId);

            var folder = Path.Combine(baseFolder, FolderName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        public string GetMetadataFile(string projectId, string? userId)
        {
            return Path.Combine(GetFolder(projectId, userId), MetadataFileName);
        }

        public async Task<string> SaveImageAsync(string projectId, string? userId, string fileName, byte[] data)
        {
            var folder = GetFolder(projectId, userId);
            var path = Path.Combine(folder, fileName);

            Console.WriteLine($"Saving image to: {path}");
            Console.WriteLine($"Image size: {data.Length} bytes");
            Console.WriteLine($"Folder exists: {Directory.Exists(folder)}");

            await File.WriteAllBytesAsync(path, data);

            Console.WriteLine($"Image saved successfully. File exists: {File.Exists(path)}");

            return path;
        }

        public void Add(GeneratedImageInfo info, string projectId, string? userId)
        {
            lock (_metadataLock)
            {
                var metadata = LoadMetadata(projectId, userId);
                metadata.Add(info);
                SerializableList<GeneratedImageInfo>.Save(metadata, GetMetadataFile(projectId, userId));
            }
        }

        public SerializableList<GeneratedImageInfo> GetAll(string projectId, string? userId)
        {
            var metadata = LoadMetadata(projectId, userId);
            var sorted = new SerializableList<GeneratedImageInfo>(metadata.OrderByDescending(i => i.CreatedAt));
            return sorted;
        }

        private SerializableList<GeneratedImageInfo> LoadMetadata(string projectId, string? userId)
        {
            var metadataFile = GetMetadataFile(projectId, userId);
            if (!File.Exists(metadataFile))
            {
                return new SerializableList<GeneratedImageInfo>();
            }

            try
            {
                return SerializableList<GeneratedImageInfo>.Load(metadataFile);
            }
            catch (Exception)
            {
                return new SerializableList<GeneratedImageInfo>();
            }
        }
    }
}
