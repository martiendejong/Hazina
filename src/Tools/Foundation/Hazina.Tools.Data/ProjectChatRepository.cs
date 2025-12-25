using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevGPT.GenerationTools.Data
{
    /// <summary>
    /// Service responsible for chat metadata and file operations.
    /// Handles chat metadata loading/saving and chat file paths.
    /// </summary>
    public class ProjectChatRepository
    {
        private readonly ProjectFileLocator _fileLocator;

        public ProjectChatRepository(ProjectFileLocator fileLocator)
        {
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        public SerializableList<ChatMetadata> GetChatMetaData(string projectId)
        {
            var chatsFile = _fileLocator.GetChatsFile(projectId);
            return LoadListFileOrDefault<ChatMetadata>(chatsFile);
        }

        public SerializableList<ChatMetadata> GetChatMetaDataUser(string projectId, string userId)
        {
            var chatsFile = _fileLocator.GetChatsFile(projectId, userId);
            return LoadListFileOrDefault<ChatMetadata>(chatsFile);
        }

        public ChatMetadata GetChatMetaDataById(string projectId, string chatId)
        {
            return GetChatMetaData(projectId).FirstOrDefault(m => m.Id == chatId);
        }

        public ChatMetadata GetChatMetaDataUserById(string projectId, string chatId, string userId)
        {
            return GetChatMetaDataUser(projectId, userId).FirstOrDefault(m => m.Id == chatId);
        }

        public void SaveChatMetaData(string projectId, SerializableList<ChatMetadata> metas)
        {
            var chatsFile = _fileLocator.GetChatsFile(projectId);
            SaveListFile<ChatMetadata>(chatsFile, metas);
        }

        public void SaveChatMetaData(string projectId, string userId, SerializableList<ChatMetadata> metas)
        {
            var chatsFile = _fileLocator.GetChatsFile(projectId, userId);
            SaveListFile<ChatMetadata>(chatsFile, metas);
        }

        public SerializableList<T> LoadListFileOrDefault<T>(string filePath) where T : Serializer<T>
        {
            if (File.Exists(filePath))
            {
                var data = Serializer<T[]>.Load(filePath);
                return new SerializableList<T>(data);
            }
            return new SerializableList<T>();
        }

        private void SaveListFile<T>(string filePath, SerializableList<T> items) where T : Serializer<T>
        {
            items.Save(filePath);
        }
    }
}

