using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Abstractions;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using Hazina.Tools.Services.Web;
using System;
using System.IO;
using System.Threading.Tasks;
using Hazina.Tools.Services.Chat;

namespace Hazina.Tools.Services.Chat
{
    public partial class ChatService : ChatServiceBase, IChatService
    {
        private readonly IProjectChatNotifier _notifier;
        private readonly IDataGatheringService? _dataGatheringService;
        private readonly IAnalysisFieldService? _analysisFieldService;
        public GeneratorAgentBase Agent { get; set; }

        public ProjectsRepository Projects { get; set; }
        public IntakeRepository Intake { get; set; }

        private readonly IChatMetadataService _metadataService;
        private readonly IChatMessageService _messageService;
        private readonly IConversationStarterService _starterService;
        private readonly IChatCanvasService _canvasService;
        private readonly IGeneratedImageRepository _generatedImageRepository;
        private readonly IChatImageService _imageService;
        private readonly IChatStreamService _streamService;

        public ChatService(
            ProjectsRepository projects,
            ProjectFileLocator fileLocator,
            IntakeRepository intake,
            GeneratorAgentBase agent,
            IProjectChatNotifier notifier,
            IChatMetadataService metadataService,
            IChatMessageService messageService,
            IConversationStarterService starterService,
            IChatCanvasService canvasService,
            IGeneratedImageRepository generatedImageRepository,
            IChatImageService imageService,
            IChatStreamService streamService,
            IDataGatheringService? dataGatheringService = null,
            IAnalysisFieldService? analysisFieldService = null)
            : base(projects, fileLocator)
        {
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _dataGatheringService = dataGatheringService;
            _analysisFieldService = analysisFieldService;
            Projects = projects;
            Intake = intake;
            Agent = agent;

            // Injected dependencies (DI-managed)
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _starterService = starterService ?? throw new ArgumentNullException(nameof(starterService));
            _canvasService = canvasService ?? throw new ArgumentNullException(nameof(canvasService));
            _generatedImageRepository = generatedImageRepository ?? throw new ArgumentNullException(nameof(generatedImageRepository));
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        }

        protected async Task onChatCreated(string project, string chatId)
        {
            await _notifier.NotifyChatCreated(project, chatId);
        }

        protected async Task onChunkReceived(string project, string subMethod, string contentHook, string chunk)
        {
            _ = _notifier.NotifyChunkReceived(project, subMethod, contentHook, chunk);
        }

        protected async Task onCanvasReceived(string project, string subMethod, string contentHook, string chunk)
        {
            await _notifier.NotifyCanvasReceived(project, subMethod, contentHook, chunk);
        }

        public SerializableList<ChatConversation> GetChats(string projectId)
            => _metadataService.GetChats(projectId);

        public SerializableList<ChatConversation> GetChats(string projectId, string userId)
            => _metadataService.GetChats(projectId, userId);

        public SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId)
            => _messageService.GetChatMessages(projectId, chatId);

        public SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId, string userId)
            => _messageService.GetChatMessages(projectId, chatId, userId);

        public async Task Delete(string projectId, string chatId)
        {
            var chats = GetChats(projectId);
            var chat = chats.Single(c => c.MetaData.Id == chatId);
            chats.Remove(chat);
            var chatFile = base.GetChatFile(projectId, chatId);
            File.Delete(chatFile);
            _metadataService.SaveChatMetaData(projectId, new SerializableList<ChatMetadata>(chats.Select(c => c.MetaData)));
        }

        public async Task Delete(string projectId, string chatId, string userId)
        {
            var chats = GetChats(projectId, userId);
            var chat = chats.Single(c => c.MetaData.Id == chatId);
            chats.Remove(chat);
            var chatFile = base.GetChatFile(projectId, chatId, userId);
            File.Delete(chatFile);
            _metadataService.SaveChatMetaData(projectId, new SerializableList<ChatMetadata>(chats.Select(c => c.MetaData)), userId);
        }

        public void Remove(string projectId, string chatId, int index)
        {
            var messages = GetChatMessages(projectId, chatId);
            var m = messages[index];
            messages.Remove(m);
            _messageService.StoreChatMessages(projectId, chatId, messages);
        }

        public void Remove(string projectId, string chatId, string userId, int index)
        {
            var messages = GetChatMessages(projectId, chatId, userId);
            var m = messages[index];
            messages.Remove(m);
            _messageService.StoreChatMessages(projectId, chatId, messages, userId);
        }

        public void Update(string projectId, string chatId, string userId, int index, string message)
        {
            var messages = GetChatMessages(projectId, chatId, userId);
            messages[index].Text = message;
            _messageService.StoreChatMessages(projectId, chatId, messages, userId);
        }

        public void Update(string projectId, string chatId, int index, string message)
        {
            var messages = GetChatMessages(projectId, chatId);
            messages[index].Text = message;
            _messageService.StoreChatMessages(projectId, chatId, messages);
        }

        public ConversationMessage AddFileMessage(string projectId, string chatId, string filePath, bool includeInProject)
        {
            var fileMessage = new HazinaStoreChatFile { File = filePath, IncludeInProject = includeInProject };
            var chatItem = new ConversationMessage { Role = ChatMessageRole.Assistant, Text = JsonSerializer.Serialize(fileMessage) };
            var messages = GetChatMessages(projectId, chatId);
            // Prevent duplicate file messages
            if (messages.Count > 0)
            {
                var lastMsg = messages.Last();
                if (lastMsg.Role == ChatMessageRole.Assistant && lastMsg.Text == chatItem.Text)
                {
                    return lastMsg;
                }
            }
            messages.Add(chatItem);
            _messageService.StoreChatMessages(projectId, chatId, messages);
            return chatItem;
        }

        public ConversationMessage AddFileMessage(string projectId, string chatId, string userId, string filePath, bool includeInProject)
        {
            var fileMessage = new HazinaStoreChatFile { File = filePath, IncludeInProject = includeInProject };
            var chatItem = new ConversationMessage { Role = ChatMessageRole.Assistant, Text = JsonSerializer.Serialize(fileMessage) };
            var messages = GetChatMessages(projectId, chatId, userId);
            // Prevent duplicate file messages
            if (messages.Count > 0)
            {
                var lastMsg = messages.Last();
                if (lastMsg.Role == ChatMessageRole.Assistant && lastMsg.Text == chatItem.Text)
                {
                    return lastMsg;
                }
            }
            messages.Add(chatItem);
            _messageService.StoreChatMessages(projectId, chatId, messages, userId);
            return chatItem;
        }

        public ChatMetadata UpdateChatName(string projectId, string chatId, string userId, string name)
        {
            var metas = _metadataService.GetChatMetaDataUser(projectId, userId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Name = name;
            }
            _metadataService.SaveChatMetaData(projectId, metas, userId);
            return meta;
        }

        public ChatMetadata UpdateChatName(string projectId, string chatId, string name)
        {
            var metas = _metadataService.GetChatMetaData(projectId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.Name = name;
            }
            _metadataService.SaveChatMetaData(projectId, metas);
            return meta;
        }

        public ChatMetadata UpdateChatPinState(string projectId, string chatId, string userId, bool isPinned)
        {
            var metas = _metadataService.GetChatMetaDataUser(projectId, userId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.IsPinned = isPinned;
            }
            _metadataService.SaveChatMetaData(projectId, metas, userId);
            return meta;
        }

        public ChatMetadata UpdateChatPinState(string projectId, string chatId, bool isPinned)
        {
            var metas = _metadataService.GetChatMetaData(projectId);
            var meta = metas.SingleOrDefault(c => c.Id == chatId);
            if (meta != null)
            {
                meta.IsPinned = isPinned;
            }
            _metadataService.SaveChatMetaData(projectId, metas);
            return meta;
        }

        public async Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, string userId, Project project, CanvasMessage message, CancellationToken cancel)
            => await _canvasService.EditCanvasMessage(projectId, chatId, userId, project, message, cancel);

        public async Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, Project project, CanvasMessage message, CancellationToken cancel)
            => await _canvasService.EditCanvasMessage(projectId, chatId, project, message, cancel);

        public void CreateChat(string projectId, string chatId)
            => _messageService.CreateChat(projectId, chatId);

        public string GetChatUploadsFolder(string projectId, string chatId)
            => _canvasService.GetChatUploadsFolder(projectId, chatId);

        public string GetChatUploadsFolder(string projectId, string chatId, string userId)
            => _canvasService.GetChatUploadsFolder(projectId, chatId, userId);

        public SerializableList<ChatConversation> GetAllChats()
            => _metadataService.GetAllChats();

        public async Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet)
            => await _imageService.GenerateImage(projectId, chatId, userId, project, chatMessage, cancel, isImageSet);

        public async Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet)
            => await _imageService.GenerateImage(projectId, chatId, project, chatMessage, cancel, isImageSet);

        // Overloads accepting raw string messages
        public Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, string chatMessage, CancellationToken cancel, bool isImageSet)
            => GenerateImage(projectId, chatId, userId, project, new GeneratorMessage { Message = chatMessage }, cancel, isImageSet);

        public Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, string chatMessage, CancellationToken cancel, bool isImageSet)
            => GenerateImage(projectId, chatId, project, new GeneratorMessage { Message = chatMessage }, cancel, isImageSet);

        public async Task<ChatConversation> SendChatMessage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel)
        {
            // Ensure chat exists and metadata is initialized
            if (string.IsNullOrWhiteSpace(chatId))
            {
                chatId = Guid.NewGuid().ToString();
                _messageService.CreateChat(projectId, chatId);

                var metas = _metadataService.GetChatMetaDataUser(projectId, userId);
                metas.Add(new ChatMetadata
                {
                    Id = chatId,
                    Name = "Chat",
                    Modified = DateTime.UtcNow,
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    IsPinned = false,
                    ProjectId = projectId,
                    LastMessagePreview = chatMessage?.Message ?? string.Empty
                });
                _metadataService.SaveChatMetaData(projectId, metas, userId);

                // Notify UI that a chat was created so it can navigate
                await _notifier.NotifyChatCreated(project?.Name ?? projectId, chatId);
            }

            // Append the user message immediately so UI can reflect it on refresh
            var messages = _messageService.GetChatMessages(projectId, chatId, userId);

            // If chat is brand new and has an opening question in metadata, add it first
            if (messages.Count == 0 && chatMessage?.Metadata != null && chatMessage.Metadata.ContainsKey("openingQuestion"))
            {
                var openingQuestion = chatMessage.Metadata["openingQuestion"]?.ToString();
                if (!string.IsNullOrWhiteSpace(openingQuestion))
                {
                    messages.Add(new ConversationMessage { Role = ChatMessageRole.Assistant, Text = openingQuestion });
                }
            }

            var history = messages.ToList(); // exclude the new user message from base history sent to the model
            // Use OriginalMessage for display if available, otherwise use Message
            var displayText = chatMessage?.OriginalMessage ?? chatMessage?.Message;
            messages.Add(new ConversationMessage { Role = ChatMessageRole.User, Text = displayText, Attachments = chatMessage?.Attachments });
            _messageService.StoreChatMessages(projectId, chatId, messages, userId);

            // Send a small immediate chunk to show activity while streaming starts
            _ = _notifier.NotifyChunkReceived(project?.Name ?? projectId, "chat", string.Empty, "...", chatId);

            // Pass prior history separately so the latest user message isn't duplicated
            var convo = await _streamService.SendChatMessage(projectId, chatId, userId, project, chatMessage, history, cancel);

            // Try to stream the final assistant message and persist it
            var reply = convo?.ChatMessages?.LastOrDefault()?.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(reply))
            {
                // Persist assistant reply
                var updated = _messageService.GetChatMessages(projectId, chatId, userId);
                updated.Add(new ConversationMessage { Role = ChatMessageRole.Assistant, Text = reply });
                _messageService.StoreChatMessages(projectId, chatId, updated, userId);

                // Update metadata preview/modified
                _metadataService.UpdateChatMetadataModified(projectId, chatId, null, userId);

                // Stream final chunk
                _ = _notifier.NotifyChunkReceived(project?.Name ?? projectId, "chat", string.Empty, reply, chatId);
            }

            // Fire off background services AFTER chat is saved
            // This ensures gathered data won't be overwritten by the chat save
            if (!string.IsNullOrWhiteSpace(chatMessage?.Message))
            {
                var historyMessages = history.Select(m => m.ToChatMessage()).ToList();

                // Run data gathering in background
                if (_dataGatheringService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var succeeded = false;
                        await _notifier.NotifyOperationStatus(projectId, chatId, "gathering-data", "started");
                        try
                        {
                            await _dataGatheringService.GatherDataFromMessageAsync(
                                projectId,
                                chatId,
                                chatMessage.Message,
                                historyMessages,
                                userId,
                                CancellationToken.None);
                            succeeded = true;
                        }
                        catch { /* Best effort */ }
                        finally
                        {
                            await _notifier.NotifyOperationStatus(
                                projectId,
                                chatId,
                                "gathering-data",
                                succeeded ? "completed" : "error");
                        }
                    });
                }

                // Run analysis field generation in parallel
                if (_analysisFieldService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var succeeded = false;
                        await _notifier.NotifyOperationStatus(projectId, chatId, "generating-analysis", "started");
                        try
                        {
                            var generatedFields = await _analysisFieldService.GenerateFromConversationAsync(
                                projectId,
                                chatId,
                                chatMessage.Message,
                                historyMessages,
                                userId,
                                CancellationToken.None);

                            // Check if any ImageSet fields need generation
                            var imageSetFieldsToGenerate = generatedFields
                                .Where(f => f.Content == "__IMAGE_SET_GENERATION_REQUIRED__")
                                .ToList();

                            if (imageSetFieldsToGenerate.Any())
                            {
                                // Trigger image generation for each ImageSet field
                                foreach (var imageSetField in imageSetFieldsToGenerate)
                                {
                                    try
                                    {
                                        await GenerateImageSetForChatAsync(
                                            projectId,
                                            chatId,
                                            imageSetField.Key,
                                            userId,
                                            CancellationToken.None);
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log but don't fail the whole operation
                                        Console.WriteLine($"Error generating ImageSet for field {imageSetField.Key}: {ex.Message}");
                                    }
                                }
                            }
                            succeeded = true;
                        }
                        catch { /* Best effort */ }
                        finally
                        {
                            await _notifier.NotifyOperationStatus(
                                projectId,
                                chatId,
                                "generating-analysis",
                                succeeded ? "completed" : "error");
                        }
                    });
                }

                // Sync project data to document store for RAG (analysis fields, gathered data, etc.)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Small delay to let data gathering and analysis finish
                        await Task.Delay(2000);
                        await SyncProjectDataToDocumentStoreAsync(projectId);
                    }
                    catch { /* Best effort */ }
                });
            }

            return convo;
        }

        public async Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel)
        {
            // Ensure chat exists and metadata is initialized
            if (string.IsNullOrWhiteSpace(chatId))
            {
                chatId = Guid.NewGuid().ToString();
                _messageService.CreateChat(projectId, chatId);

                var metas = _metadataService.GetChatMetaData(projectId);
                metas.Add(new ChatMetadata
                {
                    Id = chatId,
                    Name = "Chat",
                    Modified = DateTime.UtcNow,
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    IsPinned = false,
                    ProjectId = projectId,
                    LastMessagePreview = chatMessage?.Message ?? string.Empty
                });
                _metadataService.SaveChatMetaData(projectId, metas);

                // Notify UI that a chat was created so it can navigate
                await _notifier.NotifyChatCreated(project?.Name ?? projectId, chatId);
            }


            // Append the user message immediately so UI can reflect it on refresh
            var messages = _messageService.GetChatMessages(projectId, chatId);

            // If chat is brand new and has an opening question in metadata, add it first
            if (messages.Count == 0 && chatMessage?.Metadata != null && chatMessage.Metadata.ContainsKey("openingQuestion"))
            {
                var openingQuestion = chatMessage.Metadata["openingQuestion"]?.ToString();
                if (!string.IsNullOrWhiteSpace(openingQuestion))
                {
                    messages.Add(new ConversationMessage { Role = ChatMessageRole.Assistant, Text = openingQuestion });
                }
            }

            var history = messages.ToList();
            // Use OriginalMessage for display if available, otherwise use Message
            var displayText = chatMessage?.OriginalMessage ?? chatMessage?.Message;
            messages.Add(new ConversationMessage { Role = ChatMessageRole.User, Text = displayText, Attachments = chatMessage?.Attachments });
            _messageService.StoreChatMessages(projectId, chatId, messages);

            // Send a small immediate chunk to show activity while streaming starts
            _ = _notifier.NotifyChunkReceived(project?.Name ?? projectId, "chat", string.Empty, "...", chatId);

            // Pass prior history (without the newly appended user message)
            var convo = await _streamService.SendChatMessage(projectId, chatId, project, chatMessage, history, cancel);

            // Try to stream the final assistant message and persist it
            var reply = convo?.ChatMessages?.LastOrDefault()?.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(reply))
            {
                // Persist assistant reply
                var updated = _messageService.GetChatMessages(projectId, chatId);
                updated.Add(new ConversationMessage { Role = ChatMessageRole.Assistant, Text = reply });
                _messageService.StoreChatMessages(projectId, chatId, updated);

                // Update metadata preview/modified
                _metadataService.UpdateChatMetadataModified(projectId, chatId, null);

                // Stream final chunk
                _ = _notifier.NotifyChunkReceived(project?.Name ?? projectId, "chat", string.Empty, reply, chatId);
            }

            // Fire off background services AFTER chat is saved
            // This ensures gathered data won't be overwritten by the chat save
            if (!string.IsNullOrWhiteSpace(chatMessage?.Message))
            {
                var historyMessages = history.Select(m => m.ToChatMessage()).ToList();

                // Run data gathering in background
                if (_dataGatheringService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var succeeded = false;
                        await _notifier.NotifyOperationStatus(projectId, chatId, "gathering-data", "started");
                        try
                        {
                            await _dataGatheringService.GatherDataFromMessageAsync(
                                projectId,
                                chatId,
                                chatMessage.Message,
                                historyMessages,
                                null, // no userId for project-specific chats
                                CancellationToken.None);
                            succeeded = true;
                        }
                        catch { /* Best effort */ }
                        finally
                        {
                            await _notifier.NotifyOperationStatus(
                                projectId,
                                chatId,
                                "gathering-data",
                                succeeded ? "completed" : "error");
                        }
                    });
                }

                // Run analysis field generation in parallel
                if (_analysisFieldService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var succeeded = false;
                        await _notifier.NotifyOperationStatus(projectId, chatId, "generating-analysis", "started");
                        try
                        {
                            await _analysisFieldService.GenerateFromConversationAsync(
                                projectId,
                                chatId,
                                chatMessage.Message,
                                historyMessages,
                                null, // no userId for project-specific chats
                                CancellationToken.None);
                            succeeded = true;
                        }
                        catch { /* Best effort */ }
                        finally
                        {
                            await _notifier.NotifyOperationStatus(
                                projectId,
                                chatId,
                                "generating-analysis",
                                succeeded ? "completed" : "error");
                        }
                    });
                }
            }

            return convo;
        }

        // Overloads accepting raw string messages
        public Task<ChatConversation> SendChatMessage(string projectId, string chatId, string userId, Project project, string chatMessage, CancellationToken cancel)
            => SendChatMessage(projectId, chatId, userId, project, new GeneratorMessage { Message = chatMessage }, cancel);

        public Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, string chatMessage, CancellationToken cancel)
            => SendChatMessage(projectId, chatId, project, new GeneratorMessage { Message = chatMessage }, cancel);

        public async Task<ConversationStarter> GetConversationStarter(string projectId, string chatId, string userId)
            => await _starterService.GetConversationStarter(projectId, chatId, userId);

        public async Task<ConversationStarter> GetConversationStarter(string projectId, string chatId)
            => await _starterService.GetConversationStarter(projectId, chatId);

        public async Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string userId, string addTochatMessage, CancellationToken cancel)
            => await _starterService.OpenConversationStarter(projectId, starter, chatId, userId, addTochatMessage, cancel);

        public async Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string addTochatMessage, CancellationToken cancel)
            => await _starterService.OpenConversationStarter(projectId, starter, chatId, addTochatMessage, cancel);

        // Use base helpers for file path operations; reflection is no longer needed.

        // Expose meta-data helpers used by ChatController
        public SerializableList<ChatMetadata> GetChatMetaData(string projectId)
            => _metadataService.GetChatMetaData(projectId);

        public SerializableList<ChatMetadata> GetChatMetaDataUser(string projectId, string userId)
            => _metadataService.GetChatMetaDataUser(projectId, userId);

        public SerializableList<GeneratedImageInfo> GetGeneratedImages(string projectId)
            => _generatedImageRepository.GetAll(projectId, null);

        public SerializableList<GeneratedImageInfo> GetGeneratedImages(string projectId, string userId)
            => _generatedImageRepository.GetAll(projectId, string.IsNullOrWhiteSpace(userId) ? null : userId);

        // Overloads to fetch single chat metadata
        public ChatMetadata GetChatMetaData(string projectId, string chatId)
            => _metadataService.GetChatMetaData(projectId, chatId);

        public ChatMetadata GetChatMetaDataUser(string projectId, string chatId, string userId)
            => _metadataService.GetChatMetaDataUser(projectId, chatId, userId);

        /// <summary>
        /// Generates an ImageSet (4 logo images) for a chat-initiated analysis field generation.
        /// This is called when the LLM determines a logo should be generated during conversation.
        /// </summary>
        private async Task GenerateImageSetForChatAsync(
            string projectId,
            string chatId,
            string fieldKey,
            string? userId,
            CancellationToken cancellationToken)
        {
            var project = Projects.Load(projectId);
            var promptBase = "A brand logo for the business discussed in this conversation. Clean, memorable, professional.";

            var images = new List<ImageVariant>();

            // Generate 4 logo variants
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    var variantPrompt = $"{promptBase} (variant {i + 1})";
                    var message = new GeneratorMessage { Message = variantPrompt };

                    // Generate single image
                    var convo = await GenerateImage(projectId, chatId, project, message, cancellationToken, true);
                    var lastAssistant = convo.ChatMessages.LastOrDefault(m => m.Role == ChatMessageRole.Assistant);

                    // Extract URL from message
                    var url = ExtractImageUrlFromMessage(lastAssistant);

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        images.Add(new ImageVariant { Url = url, Prompt = variantPrompt, Id = $"img-{i}" });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating image variant {i}: {ex.Message}");
                }
            }

            if (images.Count > 0)
            {
                // Create ImageSet
                var imageSet = new ImageSet
                {
                    Images = images,
                    SelectedIndex = 0,
                    Title = promptBase,
                    Key = fieldKey
                };

                var contentJson = System.Text.Json.JsonSerializer.Serialize(imageSet, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                // Save to analysis field
                if (_analysisFieldService != null)
                {
                    await _analysisFieldService.SaveFieldAsync(
                        projectId,
                        chatId,
                        fieldKey,
                        contentJson,
                        "Generated from chat conversation",
                        userId,
                        cancellationToken);
                }
            }
        }

        private static string ExtractImageUrlFromMessage(ConversationMessage? assistant)
        {
            if (assistant == null) return string.Empty;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(assistant.Payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("url", out System.Text.Json.JsonElement urlProp))
                {
                    var u = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(u)) return u;
                }
            }
            catch { }

            var text = assistant?.Text ?? string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"!\[Generated Image\]\((.*?)\)");
            if (match.Success) return match.Groups[1].Value;
            return string.Empty;
        }

        /// <summary>
        /// Syncs project data (analysis fields, gathered data, etc.) to the document store for RAG.
        /// This enables semantic search over all project information.
        /// </summary>
        public async Task SyncProjectDataToDocumentStoreAsync(string projectId)
        {
            try
            {
                // Get the document store from the agent
                var project = Projects.Load(projectId);
                var context = await Agent.InitStore(project);

                if (context != null)
                {
                    var contextService = new ChatContextService(context, Projects, FileLocator);
                    await contextService.SyncProjectDataToStoreAsync(projectId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing project data to document store: {ex.Message}");
            }
        }
    }
}
