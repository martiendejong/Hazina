using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using OpenAI.Chat;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Store
{
    public class FileSystemAnalysisFieldsProvider : IAnalysisFieldsProvider
    {
        private readonly ProjectsRepository _projects;
        private readonly ProjectFileLocator _fileLocator;

        public FileSystemAnalysisFieldsProvider(ProjectsRepository projects)
        {
            _projects = projects;
            _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
        }

        public FileSystemAnalysisFieldsProvider(ProjectsRepository projects, ProjectFileLocator fileLocator)
        {
            _projects = projects;
            _fileLocator = fileLocator ?? new ProjectFileLocator(projects.ProjectsFolder);
        }

        public Task<IReadOnlyList<AnalysisFieldInfo>> GetFieldsAsync(string projectId)
        {
            var result = AnalysisFieldConfigLoader.LoadFields(_projects.ProjectsFolder, createDefaultConfigFile: true);
            return Task.FromResult(result);
        }

        public async Task<bool> SaveFieldAsync(string projectId, string key, string content, string feedback = null, string chatId = null, string userId = null)
        {
            var fields = await GetFieldsAsync(projectId);
            var info = fields.FirstOrDefault(f => f.Key.Equals(key, System.StringComparison.OrdinalIgnoreCase));
            if (info == null) return false;

            // Save to the analysis field file
            var relFile = info.File;
            var abs = _fileLocator.GetPath(projectId, relFile);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            await File.WriteAllTextAsync(abs, content ?? string.Empty);

            // Persist to chat file if chatId is provided
            if (!string.IsNullOrWhiteSpace(chatId))
            {
                await PersistToChatFileAsync(projectId, chatId, userId, key, info.DisplayName ?? key, content, feedback);
            }

            return true;
        }

        private async Task PersistToChatFileAsync(string projectId, string chatId, string userId, string key, string title, string content, string feedback)
        {
            try
            {
                var chatFile = string.IsNullOrWhiteSpace(userId)
                    ? _fileLocator.GetChatFile(projectId, chatId)
                    : _fileLocator.GetChatFile(projectId, chatId, userId);

                // Load existing messages
                SerializableList<ConversationMessage> messages;
                if (File.Exists(chatFile))
                {
                    var json = await File.ReadAllTextAsync(chatFile);
                    messages = SerializableList<ConversationMessage>.Deserialize(json);
                }
                else
                {
                    messages = new SerializableList<ConversationMessage>();
                }

                // Create analysis data payload
                var payload = new
                {
                    type = "analysis-data",
                    componentName = "view/analysis/AnalysisData",
                    key,
                    title,
                    content,
                    feedback
                };

                // Add message with payload
                var message = new ConversationMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Text = $"I will generate the {title}",
                    Payload = payload
                };
                messages.Add(message);

                // Save back to file
                await File.WriteAllTextAsync(chatFile, messages.Serialize());
            }
            catch
            {
                // Best effort - don't fail the save operation if chat persistence fails
            }
        }
    }
}

