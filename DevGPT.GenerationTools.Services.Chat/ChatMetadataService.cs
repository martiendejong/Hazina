using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using System;
using System.IO;
using System.Linq;

namespace DevGPT.GenerationTools.Services.Chat
{
    /// <summary>
    /// Service responsible for managing chat metadata (chat lists, names, pins, etc.)
    /// </summary>
    public class ChatMetadataService : ChatServiceBase, IChatMetadataService
    {
        public ChatMetadataService(ProjectsRepository projects, ProjectFileLocator fileLocator) : base(projects, fileLocator)
        {
        }

        #region Get Chat Metadata

        public SerializableList<ChatConversation> GetChats(string projectId, string userId = null)
        {
            var chatMetas = string.IsNullOrEmpty(userId)
                ? GetChatMetaData(projectId)
                : GetChatMetaDataUser(projectId, userId);

            return new SerializableList<ChatConversation>(
                chatMetas.Select(m => new ChatConversation { MetaData = m })
                        .OrderByDescending(m => m.MetaData.Modified)
                        .ToList());
        }

        public ChatMetadata GetChatMetaData(string projectId, string chatId)
        {
            var metas = GetChatMetaData(projectId);
            return metas.SingleOrDefault(c => c.Id == chatId);
        }

        public ChatMetadata GetChatMetaDataUser(string projectId, string chatId, string userId)
        {
            var metas = GetChatMetaDataUser(projectId, userId);
            return metas.SingleOrDefault(c => c.Id == chatId);
        }

        public SerializableList<ChatMetadata> GetChatMetaData(string projectId)
        {
            if (projectId == ProjectsRepository.GLOBAL_PROJECT_ID)
                return GetGlobalChatMetaData();

            var chatsFile = GetChatsFile(projectId);
            return LoadListFileOrDefault<ChatMetadata>(chatsFile);
        }

        public SerializableList<ChatMetadata> GetChatMetaDataUser(string projectId, string userId)
        {
            var chatsFile = GetChatsFile(projectId, userId);
            return LoadListFileOrDefault<ChatMetadata>(chatsFile);
        }

        private SerializableList<ChatMetadata> GetGlobalChatMetaData()
        {
            var chatsFile = Path.Combine(GetGlobalChatsFolder(), "chats.json");
            return LoadListFileOrDefault<ChatMetadata>(chatsFile);
        }

        #endregion

        #region Save Chat Metadata

        public void SaveChatMetaData(string projectId, SerializableList<ChatMetadata> metas, string userId = null)
        {
            if (string.IsNullOrEmpty(userId))
                SaveChatMetaData(projectId, metas);
            else
                SaveChatMetaData(projectId, userId, metas);
        }

        private void SaveChatMetaData(string projectId, SerializableList<ChatMetadata> metas)
        {
            if (projectId == ProjectsRepository.GLOBAL_PROJECT_ID)
            {
                StoreGlobalChatMetadata(metas);
                return;
            }

            var chatsFile = GetChatsFile(projectId);
            SaveListFile(chatsFile, metas);
        }

        private void SaveChatMetaData(string projectId, string userId, SerializableList<ChatMetadata> metas)
        {
            var chatsFile = GetChatsFile(projectId, userId);
            SaveListFile(chatsFile, metas);
        }

        private void StoreGlobalChatMetadata(SerializableList<ChatMetadata> metas)
        {
            var chatsFile = Path.Combine(GetGlobalChatsFolder(), "chats.json");
            SaveListFile(chatsFile, metas);
        }

        #endregion

        #region Update Chat Metadata

        public ChatMetadata UpdateChatName(string projectId, string chatId, string name, string userId = null)
        {
            if (string.IsNullOrEmpty(userId))
                return UpdateChatNameInternal(projectId, chatId, name);
            else
                return UpdateChatNameInternal(projectId, chatId, userId, name);
        }

        private ChatMetadata UpdateChatNameInternal(string projectId, string chatId, string name)
        {
            var metas = GetChatMetaData(projectId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Name = name;
            }
            SaveChatMetaData(projectId, metas);
            return meta;
        }

        private ChatMetadata UpdateChatNameInternal(string projectId, string chatId, string userId, string name)
        {
            var metas = GetChatMetaDataUser(projectId, userId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Name = name;
            }
            SaveChatMetaData(projectId, userId, metas);
            return meta;
        }

        public ChatMetadata UpdateChatPinState(string projectId, string chatId, bool isPinned, string userId = null)
        {
            if (string.IsNullOrEmpty(userId))
                return UpdateChatPinStateInternal(projectId, chatId, isPinned);
            else
                return UpdateChatPinStateInternal(projectId, chatId, userId, isPinned);
        }

        private ChatMetadata UpdateChatPinStateInternal(string projectId, string chatId, bool isPinned)
        {
            var metas = GetChatMetaData(projectId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.IsPinned = isPinned;
            }
            SaveChatMetaData(projectId, metas);
            return meta;
        }

        private ChatMetadata UpdateChatPinStateInternal(string projectId, string chatId, string userId, bool isPinned)
        {
            var metas = GetChatMetaDataUser(projectId, userId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.IsPinned = isPinned;
            }
            SaveChatMetaData(projectId, userId, metas);
            return meta;
        }

        public ChatMetadata UpdateChatMetadataModified(string projectId, string chatId, string name, string userId = null)
        {
            if (string.IsNullOrEmpty(userId))
                return UpdateChatMetadataModifiedInternal(projectId, chatId, name);
            else
                return UpdateChatMetadataModifiedInternal(projectId, chatId, userId, name);
        }

        private ChatMetadata UpdateChatMetadataModifiedInternal(string projectId, string chatId, string name)
        {
            var metas = GetChatMetaData(projectId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Modified = DateTime.Now;
                if (!string.IsNullOrEmpty(name))
                    meta.Name = name;
            }
            SaveChatMetaData(projectId, metas);
            return meta;
        }

        private ChatMetadata UpdateChatMetadataModifiedInternal(string projectId, string chatId, string userId, string name)
        {
            var metas = GetChatMetaDataUser(projectId, userId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Modified = DateTime.Now;
                if (!string.IsNullOrEmpty(name))
                    meta.Name = name;
            }
            SaveChatMetaData(projectId, userId, metas);
            return meta;
        }

        #endregion

        #region Get All Chats (Global)

        public SerializableList<ChatConversation> GetAllChats()
        {
            var allChats = new System.Collections.Generic.List<ChatConversation>();

            // Add global chats with unique composite ids
            var globalMetas = GetGlobalChatMetaData();
            var globalId = ProjectsRepository.GLOBAL_PROJECT_ID;
            allChats.AddRange(globalMetas.Select(m => new ChatConversation
            {
                MetaData = new ChatMetadata
                {
                    Id = $"{globalId}.{m.Id}",
                    Name = m.Name,
                    Modified = m.Modified,
                    Created = m.Created,
                    LastUpdated = m.LastUpdated,
                    IsPinned = m.IsPinned,
                    ProjectId = globalId,
                    LastMessagePreview = m.LastMessagePreview
                }
            }));

            // Add project chats with unique composite ids
            var projects = Directory.GetDirectories(Projects.ProjectsFolder);
            foreach (var projectPath in projects)
            {
                var projectId = Path.GetFileName(projectPath);
                if (projectId == ".chats") continue; // Skip the global chats folder

                try
                {
                    var projectMetas = GetChatMetaData(projectId);
                    allChats.AddRange(projectMetas.Select(m => new ChatConversation
                    {
                        MetaData = new ChatMetadata
                        {
                            Id = $"{projectId}.{m.Id}",
                            Name = m.Name,
                            Modified = m.Modified,
                            Created = m.Created,
                            LastUpdated = m.LastUpdated,
                            IsPinned = m.IsPinned,
                            ProjectId = projectId,
                            LastMessagePreview = m.LastMessagePreview
                        }
                    }));
                }
                catch
                {
                    // Skip projects that don't have valid chat metadata
                }
            }

            return new SerializableList<ChatConversation>(allChats
                .GroupBy(c => c.MetaData.Id) // de-dup if any
                .Select(g => g.First())
                .OrderByDescending(c => c.MetaData.Modified)
                .ToList());
        }

        #endregion
    }
}
