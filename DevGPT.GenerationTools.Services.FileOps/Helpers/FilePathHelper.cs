using System;
using System.IO;

namespace DevGPT.GenerationTools.Services.FileOps.Helpers
{
    /// <summary>
    /// Helper class for file path operations
    /// Eliminates duplicate file path manipulation code
    /// </summary>
    public static class FilePathHelper
    {
        // Constants for file naming
        public const string UploadsFolder = "Uploads";
        public const string UploadedFilesMetadata = "uploadedFiles.json";
        public const string TextFileExtension = ".txt";

        /// <summary>
        /// Get the text file path for an uploaded file
        /// Format: {filename}.{extension}.txt
        /// Example: document.pdf -> document.pdf.txt
        /// </summary>
        public static string GetTextFilePath(string uploadedFilePath)
        {
            if (string.IsNullOrWhiteSpace(uploadedFilePath))
                throw new ArgumentException("File path cannot be empty", nameof(uploadedFilePath));

            var directory = Path.GetDirectoryName(uploadedFilePath);
            var fileName = Path.GetFileName(uploadedFilePath);
            var extension = GetFileExtension(fileName);

            var textFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.{extension}{TextFileExtension}";
            return Path.Combine(directory ?? string.Empty, textFileName);
        }

        /// <summary>
        /// Get file extension without the dot
        /// Returns "txt" for files without extension
        /// </summary>
        public static string GetFileExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "txt";

            var extension = Path.GetExtension(fileName)?.TrimStart('.');
            return string.IsNullOrWhiteSpace(extension) ? "txt" : extension;
        }

        /// <summary>
        /// Ensure filename is unique in folder by appending counter
        /// Example: file.pdf -> file_1.pdf -> file_2.pdf
        /// </summary>
        public static string EnsureUniqueFileName(string folder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Folder cannot be empty", nameof(folder));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            var filePath = Path.Combine(folder, fileName);

            if (!File.Exists(filePath))
                return fileName;

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var counter = 1;

            do
            {
                fileName = $"{nameWithoutExtension}_{counter}{extension}";
                filePath = Path.Combine(folder, fileName);
                counter++;
            }
            while (File.Exists(filePath));

            return fileName;
        }

        /// <summary>
        /// Get the uploads folder path for a project
        /// </summary>
        public static string GetUploadsFolder(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path cannot be empty", nameof(projectPath));

            return Path.Combine(projectPath, UploadsFolder);
        }

        /// <summary>
        /// Get the uploaded files metadata path for a project
        /// </summary>
        public static string GetUploadedFilesMetadataPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path cannot be empty", nameof(projectPath));

            return Path.Combine(projectPath, UploadedFilesMetadata);
        }

        /// <summary>
        /// Ensure directory exists, create if it doesn't
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
