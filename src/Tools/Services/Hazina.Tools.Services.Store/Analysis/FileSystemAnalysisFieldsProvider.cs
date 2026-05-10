using Hazina.Tools.Data;
using Hazina.Tools.Models;
using OpenAI.Chat;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Store
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

        public async Task<bool> SaveFieldAsync(string projectId, string key, string content, string? feedback = null, string? chatId = null, string? userId = null)
        {
            var fields = await GetFieldsAsync(projectId);
            var info = fields.FirstOrDefault(f => f.Key.Equals(key, System.StringComparison.OrdinalIgnoreCase));
            if (info == null) return false;

            // Save to the analysis field file
            var relFile = info.File;
            var abs = _fileLocator.GetPath(projectId, relFile);
            var dir = Path.GetDirectoryName(abs);
            if (dir != null)
                Directory.CreateDirectory(dir);

            // Determine how to save based on file extension and genericType
            string contentToSave = content ?? string.Empty;

            // If it's a .txt file, unwrap JSON if content was serialized
            if (relFile.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
            {
                // Try to parse as JSON and extract string value (for backwards compatibility)
                if (!string.IsNullOrWhiteSpace(contentToSave) && contentToSave.TrimStart().StartsWith("\""))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<JsonElement>(contentToSave);
                        if (parsed.ValueKind == JsonValueKind.String)
                        {
                            contentToSave = parsed.GetString() ?? contentToSave;
                        }
                    }
                    catch
                    {
                        // Not JSON or failed to parse - use as-is
                    }
                }
            }

            await File.WriteAllTextAsync(abs, contentToSave);

            // Persist to chat file if chatId is provided
            if (!string.IsNullOrWhiteSpace(chatId))
            {
                await PersistToChatFileAsync(projectId, chatId ?? string.Empty, userId ?? string.Empty, key, info.DisplayName ?? key, content ?? string.Empty, feedback ?? string.Empty);
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

                // Get the field configuration to determine the correct component name
                var fields = await GetFieldsAsync(projectId);
                var fieldInfo = fields.FirstOrDefault(f => f.Key.Equals(key, System.StringComparison.OrdinalIgnoreCase));
                var componentName = fieldInfo?.ChatComponentName ?? "AnalysisData";

                // Create analysis data payload
                var payload = new
                {
                    type = "analysis-data",
                    componentName = componentName, // Component name from config
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

