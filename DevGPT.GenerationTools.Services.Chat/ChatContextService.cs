using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    /// <summary>
    /// Manages chat context using RAG (Retrieval Augmented Generation).
    /// Truncates long chat histories and retrieves relevant context from embeddings.
    /// </summary>
    public class ChatContextService
    {
        private readonly IDocumentStore _documentStore;
        private readonly ProjectsRepository _projects;
        private readonly ProjectFileLocator _fileLocator;
        private bool _isInitialized = false;

        // Configuration
        private const int RECENT_MESSAGES_COUNT = 10; // Keep last 10 messages in context
        private const int RELEVANT_MESSAGES_COUNT = 5; // Retrieve 5 relevant older messages
        private const int RELEVANT_PROJECT_DATA_COUNT = 3; // Retrieve 3 relevant project data items

        public ChatContextService(
            IDocumentStore documentStore,
            ProjectsRepository projects,
            ProjectFileLocator fileLocator)
        {
            _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        /// <summary>
        /// Safely creates a directory, handling the case where a file might exist with the same name.
        /// </summary>
        private void EnsureDirectoryExists(string path)
        {
            // If directory already exists, we're done
            if (System.IO.Directory.Exists(path))
                return;

            // If a FILE exists with this name, delete it first
            if (System.IO.File.Exists(path))
            {
                Console.WriteLine($"Warning: File exists at directory path '{path}', removing it");
                System.IO.File.Delete(path);
            }

            // Now create the directory
            System.IO.Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Ensures the document store directory structure exists.
        /// Creates all necessary directories for embeddings, chunks, and metadata.
        /// </summary>
        private void EnsureDocumentStoreInitialized(string projectId)
        {
            if (_isInitialized)
                return;

            try
            {
                // Get the project folder
                var projectFolder = _fileLocator.GetProjectFolder(projectId);

                // Ensure base project folder exists
                EnsureDirectoryExists(projectFolder);

                // Ensure parts folder exists (for chunks.json)
                var partsFolder = System.IO.Path.Combine(projectFolder, "parts");
                EnsureDirectoryExists(partsFolder);

                // Ensure documents folder exists (for text store)
                var documentsFolder = System.IO.Path.Combine(projectFolder, "documents");
                EnsureDirectoryExists(documentsFolder);

                // Ensure embeddings folder exists
                var embeddingsFolder = System.IO.Path.Combine(projectFolder, "embeddings");
                EnsureDirectoryExists(embeddingsFolder);

                // Ensure metadata folder exists
                var metadataFolder = System.IO.Path.Combine(projectFolder, "metadata");
                EnsureDirectoryExists(metadataFolder);

                _isInitialized = true;
                Console.WriteLine($"Document store initialized for project {projectId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize document store directories: {ex.Message}");
                // Don't throw - graceful degradation
            }
        }

        /// <summary>
        /// Builds context for the LLM with recent messages + relevant retrieved context.
        /// </summary>
        public async Task<List<ConversationMessage>> BuildContextAsync(
            string projectId,
            string chatId,
            List<ConversationMessage> allMessages,
            string currentQuery)
        {
            // 0. Ensure document store is initialized (creates necessary directories)
            EnsureDocumentStoreInitialized(projectId);

            // 1. Take last N messages as recent context
            var recentMessages = allMessages.Skip(Math.Max(0, allMessages.Count - RECENT_MESSAGES_COUNT)).ToList();

            // 2. Store ALL messages as embeddings (if not already stored)
            // Await so retrieval in this turn can use freshly added embeddings.
            try
            {
                await StoreMessagesAsEmbeddingsAsync(projectId, chatId, allMessages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to store messages as embeddings: {ex.Message}");
            }

            // 3. Retrieve relevant older messages (excluding recent ones)
            List<ConversationMessage> relevantOlderMessages = new List<ConversationMessage>();
            try
            {
                relevantOlderMessages = await RetrieveRelevantMessagesAsync(
                    projectId,
                    chatId,
                    currentQuery,
                    allMessages.Count - RECENT_MESSAGES_COUNT, // Only search in older messages
                    RELEVANT_MESSAGES_COUNT);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve relevant messages: {ex.Message}");
            }

            // 4. Retrieve relevant project data (analysis fields, gathered data, etc.)
            string relevantProjectContext = string.Empty;
            try
            {
                relevantProjectContext = await RetrieveRelevantProjectDataAsync(projectId, currentQuery);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve relevant project data: {ex.Message}");
            }

            // 5. Build final context: [relevant older messages] + [project data summary] + [recent messages]
            var context = new List<ConversationMessage>();

            // Add relevant older messages first
            if (relevantOlderMessages.Any())
            {
                context.AddRange(relevantOlderMessages);
            }

            // Add project data as a system message if any found
            if (!string.IsNullOrWhiteSpace(relevantProjectContext))
            {
                context.Add(new ConversationMessage
                {
                    Role = ChatMessageRole.System,
                    Text = $"**Relevant Project Information:**\n\n{relevantProjectContext}"
                });
            }

            // Add recent messages last (most important)
            context.AddRange(recentMessages);

            return context;
        }

        /// <summary>
        /// Stores all chat messages as embeddings in the document store.
        /// </summary>
        private async Task StoreMessagesAsEmbeddingsAsync(
            string projectId,
            string chatId,
            List<ConversationMessage> messages)
        {
            try
            {
                int startIndex = Math.Max(0, messages.Count - 10); for (int i = startIndex; i < messages.Count; i++)
                {
                    var message = messages[i];
                    var key = $"chat/{chatId}/message/{i}";

                    // Create searchable content with metadata
                    var content = $"[{message.Role}] {message.Text}";
                    var metadata = new Dictionary<string, string>
                    {
                        { "projectId", projectId },
                        { "chatId", chatId },
                        { "messageIndex", i.ToString() },
                        { "role", message.Role.ToString() },
                        { "timestamp", DateTime.UtcNow.ToString("O") }
                    };

                    try
                    {
                        // Store with split=false to keep each message as a single searchable unit
                        await _documentStore.Store(key, content, metadata, split: false);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - don't fail the entire chat if storage fails
                        Console.WriteLine($"Failed to store message {i} for chat {chatId}: {ex.Message}");
                    }
                }

                // Update embeddings for all newly added messages
                try
                {
                    await _documentStore.UpdateEmbeddings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to update embeddings: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Don't fail the chat if embedding storage fails
                Console.WriteLine($"Error in StoreMessagesAsEmbeddingsAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves relevant older messages using semantic search.
        /// </summary>
        private async Task<List<ConversationMessage>> RetrieveRelevantMessagesAsync(
            string projectId,
            string chatId,
            string query,
            int maxMessageIndex,
            int count)
        {
            if (maxMessageIndex <= 0 || string.IsNullOrWhiteSpace(query))
                return new List<ConversationMessage>();

            try
            {
                // Search for relevant messages in the chat history
                var searchQuery = $"chat/{chatId} {query}";
                var relevantItems = await _documentStore.Embeddings(searchQuery);

                var messages = new List<ConversationMessage>();
                foreach (var item in relevantItems.Take(count))
                {
                    var key = item.Document?.Key ?? "";

                    // Parse the message key to get the index
                    if (key.StartsWith($"chat/{chatId}/message/"))
                    {
                        var indexStr = key.Split('/').Last();
                        if (int.TryParse(indexStr, out int index) && index < maxMessageIndex)
                        {
                            // Get the text content using the GetText function
                            var content = item.GetText != null
                                ? await item.GetText(key)
                                : "";

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                // Extract role and text from content
                                var role = content.StartsWith("[User]") ? ChatMessageRole.User : ChatMessageRole.Assistant;
                                var text = content.Substring(content.IndexOf(']') + 1).Trim();

                                messages.Add(new ConversationMessage
                                {
                                    Role = role,
                                    Text = text
                                });
                            }
                        }
                    }
                }

                return messages;
            }
            catch
            {
                return new List<ConversationMessage>();
            }
        }

        /// <summary>
        /// Retrieves relevant project data (analysis fields, gathered data, documents).
        /// </summary>
        private async Task<string> RetrieveRelevantProjectDataAsync(string projectId, string query)
        {
            try
            {
                var searchQuery = $"project/{projectId} {query}";
                var relevantItems = await _documentStore.Embeddings(searchQuery);

                var contextParts = new List<string>();
                foreach (var item in relevantItems.Take(RELEVANT_PROJECT_DATA_COUNT))
                {
                    var key = item.Document?.Key ?? "";
                    var text = item.GetText != null
                        ? await item.GetText(key)
                        : "";

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Format based on item type
                        if (key.StartsWith("analysis/"))
                        {
                            var fieldKey = key.Substring(9);
                            contextParts.Add($"**{fieldKey}**: {text}");
                        }
                        else if (key.StartsWith("gathered/"))
                        {
                            var dataKey = key.Substring(9);
                            contextParts.Add($"**Gathered Data ({dataKey})**: {text}");
                        }
                        else
                        {
                            contextParts.Add(text);
                        }
                    }
                }

                return string.Join("\n\n", contextParts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Syncs all project data (analysis fields, gathered data, etc.) to the document store.
        /// Should be called periodically or when data is updated.
        /// </summary>
        public async Task SyncProjectDataToStoreAsync(string projectId)
        {
            try
            {
                // 0. Ensure document store is initialized
                EnsureDocumentStoreInitialized(projectId);

                // 1. Sync analysis fields
                await SyncAnalysisFieldsAsync(projectId);

                // 2. Sync gathered data
                await SyncGatheredDataAsync(projectId);

                // 3. Update all embeddings
                await _documentStore.UpdateEmbeddings();

                Console.WriteLine($"Project data synced to document store for project {projectId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing project data to store: {ex.Message}");
            }
        }

        private async Task SyncAnalysisFieldsAsync(string projectId)
        {
            try
            {
                var analysisFolder = _fileLocator.GetPath(projectId, "analysis");
                if (!System.IO.Directory.Exists(analysisFolder))
                    return;

                var files = System.IO.Directory.GetFiles(analysisFolder, "*.txt");
                foreach (var file in files)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    var content = await System.IO.File.ReadAllTextAsync(file);
                    var key = $"analysis/{fileName}";

                    var metadata = new Dictionary<string, string>
                    {
                        { "projectId", projectId },
                        { "type", "analysis" },
                        { "fieldKey", fileName }
                    };

                    await _documentStore.Store(key, content, metadata);
                }
            }
            catch { }
        }

        private async Task SyncGatheredDataAsync(string projectId)
        {
            try
            {
                var gatheredFile = _fileLocator.GetPath(projectId, "gathered-data.json");
                if (!System.IO.File.Exists(gatheredFile))
                    return;

                var json = await System.IO.File.ReadAllTextAsync(gatheredFile);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        var key = $"gathered/{kvp.Key}";
                        var content = kvp.Value?.ToString() ?? "";

                        var metadata = new Dictionary<string, string>
                        {
                            { "projectId", projectId },
                            { "type", "gathered" },
                            { "dataKey", kvp.Key }
                        };

                        await _documentStore.Store(key, content, metadata);
                    }
                }
            }
            catch { }
        }
    }
}
