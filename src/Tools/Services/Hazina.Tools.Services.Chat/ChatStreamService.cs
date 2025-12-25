using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Store;
using Hazina.Tools.Services.Web;
using Hazina.Tools.Services.DataGathering.Abstractions;
using OpenAI.Chat;
using System.Reflection;
using System.Linq;
using HazinaStore;
using Mscc.GenerativeAI;
using System.IO;

namespace Hazina.Tools.Services.Chat
{
    public class ChatStreamService : ChatServiceBase, IChatStreamService
    {
        private readonly GeneratorAgentBase _agent;
        private readonly IntakeRepository _intake;
        private readonly IProjectChatNotifier _notifier;
        private readonly Func<IDocumentStore, string, string, string, IToolsContext> _toolsContextFactory;
        private readonly IDataGatheringService? _dataGatheringService;
        private ChatContextService? _contextService;

        public ChatStreamService(
            ProjectsRepository projects,
            ProjectFileLocator fileLocator,
            GeneratorAgentBase agent,
            IntakeRepository intake,
            IProjectChatNotifier notifier,
            Func<IDocumentStore, string, string, string, IToolsContext>? toolsContextFactory = null,
            IDataGatheringService? dataGatheringService = null)
            : base(projects, fileLocator)
        {
            _agent = agent;
            _intake = intake;
            _notifier = notifier;
            _toolsContextFactory = toolsContextFactory ?? DefaultToolsContextFactory;
            _dataGatheringService = dataGatheringService;
        }

        private IToolsContext DefaultToolsContextFactory(IDocumentStore store, string projectId, string chatId, string userId)
        {
            var apiKey = _agent.Config?.ApiSettings?.OpenApiKey ?? string.Empty;
            return new StoreToolsContext(new OpenAIConfig().Model, apiKey, store, _agent.Projects, _agent.Intake, projectId, chatId, _agent, userId);
        }

        private string LoadPromptOrDefault(string fallback)
        {
            try
            {
                var root = FileLocator?.ProjectsFolder ?? string.Empty;
                var filesToTry = new[]
                {
                    Path.Combine(root, ProjectFileLocator.ChatDefaultPromptFile),
                    Path.Combine(root, ProjectFileLocator.BasisPromptFile)
                };
                foreach (var path in filesToTry)
                {
                    if (File.Exists(path))
                    {
                        var text = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
            catch { }

            return fallback;
        }

        public async Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel)
        {
            // Build a lightweight, default chat prompt and use the agent pipeline
            var prompt = !string.IsNullOrWhiteSpace(project?.KlantSpecifiekePrompt)
                ? project.KlantSpecifiekePrompt
                : LoadPromptOrDefault("Je bent een behulpzame marketingassistent. Antwoord beknopt en duidelijk.");

            var generator = await _agent.GetGenerator(project, prompt);
            var context = await _agent.InitStore(project); // ensure store is ready for tools

            // Leverage the existing generation flow to get an assistant reply
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            // Use reflection to work around type constraint issue with NuGet package
            var getResponseMethods = generator.GetType().GetMethods().Where(m => m.Name == "GetResponse" && m.IsGenericMethod);
            var getResponseMethod = getResponseMethods.FirstOrDefault(m => m.GetParameters().Length == 7);
            var genericMethod = getResponseMethod?.MakeGenericMethod(typeof(GeneratedTextResponse));
            var responseTask = (Task)genericMethod.Invoke(generator, new object[] 
            { 
                chatMessage?.Message ?? string.Empty,
                tokenSource.Token,
                new List<HazinaChatMessage>(),
                true,
                true,
                _toolsContextFactory(context, projectId, chatId, string.Empty),
                null
            });
            await responseTask;
            var response = ((dynamic)responseTask).Result;

            var assistantText = response?.Result?.GeneratedText ?? "";
            var usage = response?.TokenUsage;
            if (usage != null)
            {
                // Track usage per project for charts
                TokenUsageTracker.Track(projectId, usage.InputTokens, usage.OutputTokens, usage.ModelName ?? "unknown");
            }

            return new ChatConversation
            {
                MetaData = new ChatMetadata { Id = chatId, Name = "Chat" },
                ChatMessages = new SerializableList<ConversationMessage>(new[]
                {
                    new ConversationMessage { Role = ChatMessageRole.User, Text = chatMessage?.Message },
                    new ConversationMessage { Role = ChatMessageRole.Assistant, Text = assistantText }
                }),
                TokenUsage = usage
            };
        }

        // Overload that accepts prior conversation history to include as base messages
        public async Task<ChatConversation> SendChatMessage(
            string projectId,
            string chatId,
            Project project,
            GeneratorMessage chatMessage,
            System.Collections.Generic.IEnumerable<ConversationMessage> history,
            CancellationToken cancel,
            string userId = "")
        {
            var prompt = !string.IsNullOrWhiteSpace(project?.KlantSpecifiekePrompt)
                ? project.KlantSpecifiekePrompt
                : LoadPromptOrDefault("You are a helpful marketing assistant. Answer professionally and clearly.");

            var generator = await _agent.GetGenerator(project, prompt);
            var context = await _agent.InitStore(project);

            // Initialize context service if not already done
            if (_contextService == null && context != null)
            {
                _contextService = new ChatContextService(context, _agent.Projects, FileLocator);
            }

            // Build optimized context with RAG (recent messages + relevant retrieved context)
            var contextMessages = history != null ? history.ToList() : new System.Collections.Generic.List<ConversationMessage>();
            if (_contextService != null && contextMessages.Any())
            {
                try
                {
                    var currentQuery = chatMessage?.Message ?? string.Empty;
                    contextMessages = await _contextService.BuildContextAsync(projectId, chatId, contextMessages, currentQuery);
                }
                catch (Exception ex)
                {
                    // Fallback to truncated history if RAG fails
                    Console.WriteLine($"RAG context building failed, using truncated history: {ex.Message}");
                    // Keep only last 10 messages as fallback
                    contextMessages = contextMessages.Skip(Math.Max(0, contextMessages.Count - 10)).ToList();
                }
            }

            var baseMessages = new System.Collections.Generic.List<HazinaChatMessage>();
            if (contextMessages != null)
            {
                foreach (var m in contextMessages)
                {
                    try { baseMessages.Add(m.ToChatMessage()); } catch { }
                }
            }

            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            // Stream chunks back to clients via notifier (SignalR)
            // Use reflection to work around type constraint issue with NuGet package
            Action<string> chunkCallback = (chunk) => {
                try
                {
                    // Use project name when available to match client-side filtering
                    var projectKey = project?.Name ?? projectId;
                    _notifier?.NotifyChunkReceived(projectKey, "chat", string.Empty, chunk);
                }
                catch { }
            };
            var streamResponseMethods = generator.GetType().GetMethods().Where(m => m.Name == "StreamResponse" && m.IsGenericMethod);
            var streamResponseMethod = streamResponseMethods.FirstOrDefault(m => m.GetParameters().Length == 8);
            var genericStreamMethod = streamResponseMethod?.MakeGenericMethod(typeof(GeneratedTextResponse));
            var responseTask = (Task)genericStreamMethod.Invoke(generator, new object[] 
            { 
                chatMessage?.Message ?? string.Empty,
                tokenSource.Token,
                chunkCallback,
                baseMessages,
                true,
                true,
                _toolsContextFactory(context, projectId, chatId, userId),
                null
            });
            await responseTask;
            var response = ((dynamic)responseTask).Result;

            var assistantText = response?.Result?.GeneratedText ?? "";
            var usage = response?.TokenUsage;
            if (usage != null)
            {
                TokenUsageTracker.Track(projectId, usage.InputTokens, usage.OutputTokens, usage.ModelName ?? "unknown");
            }

            // Note: Data gathering is handled in ChatService AFTER chat is saved
            // to prevent race conditions where gathered data gets overwritten

            return new ChatConversation
            {
                MetaData = new ChatMetadata { Id = chatId, Name = "Chat" },
                ChatMessages = new SerializableList<ConversationMessage>(new[]
                {
                    new ConversationMessage { Role = ChatMessageRole.User, Text = chatMessage?.Message },
                    new ConversationMessage { Role = ChatMessageRole.Assistant, Text = assistantText }
                }),
                TokenUsage = usage
            };
        }

        public Task<ChatConversation> SendChatMessage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, System.Collections.Generic.IEnumerable<ConversationMessage> history, CancellationToken cancel)
            => SendChatMessage(projectId, chatId, project, chatMessage, history, cancel, userId);
    }
}
