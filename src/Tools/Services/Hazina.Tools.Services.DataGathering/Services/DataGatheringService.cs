using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Services.DataGathering.Abstractions;
using DevGPT.GenerationTools.Services.DataGathering.Models;
using DevGPT.GenerationTools.Services.DataGathering.ToolsContexts;
using Microsoft.Extensions.Logging;
using DevGPT.GenerationTools.Data;
using System.IO;
using DevGPT.GenerationTools.Services.Store;
using OpenAI.Chat;

namespace DevGPT.GenerationTools.Services.DataGathering.Services;

/// <summary>
/// Implementation of <see cref="IDataGatheringService"/> that uses LLM calls to extract
/// structured data from chat conversations in the background.
/// </summary>
/// <remarks>
/// This service runs parallel to the main chat processing, analyzing messages for
/// information that should be stored as project data. It uses a specialized tools
/// context with data gathering tools only.
/// </remarks>
public sealed class DataGatheringService : IDataGatheringService
{
    private readonly IGatheredDataProvider _dataProvider;
    private readonly IGatheredDataNotifier _notifier;
    private readonly Func<ILLMClient> _clientFactory;
    private readonly ILogger<DataGatheringService>? _logger;
    private readonly string _systemPrompt;
    private readonly string _promptsRoot;
    private readonly IAnalysisFieldsProvider? _analysisProvider;
    private readonly ProjectFileLocator? _fileLocator;

    private const string DataGatheringSystemPromptFallback = @"You are a profile data extraction assistant. Only capture facts that describe the brand/project profile, such as:
- brand name or working title
- owner/founder name
- business idea or what they want to start/build/offer
- products/services
- target audience or market
- location/city/country/region
- goals, vision, mission, constraints, budget/timeline if mentioned
- key differentiators/positioning

Do NOT store raw user messages, timestamps, conversational meta, or filler text. Never mirror the entire user input.

Use concise, lowercase-hyphenated keys (e.g., 'brand-name', 'business-idea', 'owner-name', 'location', 'target-audience').
Only store NEW or changed profile facts; ignore repeats.";

    /// <summary>
    /// Initializes a new instance of the <see cref="DataGatheringService"/> class.
    /// </summary>
    /// <param name="dataProvider">Provider for storing gathered data.</param>
    /// <param name="notifier">Notifier for real-time updates.</param>
    /// <param name="clientFactory">Factory for creating LLM clients.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="promptsRoot">Optional root folder for prompt files; defaults to AppContext.BaseDirectory.</param>
    /// <param name="analysisProvider">Optional analysis provider to expose analysis tools to the LLM.</param>
    /// <param name="fileLocator">Optional file locator for persisting gathered data to chat files.</param>
    public DataGatheringService(
        IGatheredDataProvider dataProvider,
        IGatheredDataNotifier notifier,
        Func<ILLMClient> clientFactory,
        ILogger<DataGatheringService>? logger = null,
        string? promptsRoot = null,
        IAnalysisFieldsProvider? analysisProvider = null,
        ProjectFileLocator? fileLocator = null)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger;
        _promptsRoot = string.IsNullOrWhiteSpace(promptsRoot) ? AppContext.BaseDirectory : promptsRoot;
        _systemPrompt = LoadSystemPrompt(_promptsRoot);
        _analysisProvider = analysisProvider;
        _fileLocator = fileLocator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GatheredDataItem>> GatherDataFromMessageAsync(
        string projectId,
        string chatId,
        string userMessage,
        IEnumerable<DevGPTChatMessage> conversationHistory,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Array.Empty<GatheredDataItem>();
        }

        try
        {
            var client = _clientFactory();
            var toolsContext = new DataGatheringToolsContext(_dataProvider, projectId, chatId, _analysisProvider);

            // Build messages for the extraction call
            var messages = BuildExtractionMessages(userMessage, conversationHistory);

            // Make the LLM call with tools
            var response = await client.GetResponse(
                messages,
                DevGPTChatResponseFormat.Text,
                toolsContext,
                images: null,
                cancellationToken);

            var gatheredItems = toolsContext.GatheredItems;

            // Notify clients about gathered data
            if (gatheredItems.Count > 0)
            {
                await NotifyGatheredDataAsync(projectId, chatId, gatheredItems, userId, cancellationToken);
            }

            _logger?.LogInformation(
                "Gathered {Count} data items from message in chat {ChatId}",
                gatheredItems.Count, chatId);

            return gatheredItems;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error gathering data from message in chat {ChatId}", chatId);
            return Array.Empty<GatheredDataItem>();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GatheredDataItem>> GetProjectDataAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        return _dataProvider.GetAllAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GatheredDataItem?> GetDataItemAsync(
        string projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        return _dataProvider.GetAsync(projectId, key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> StoreDataItemAsync(
        string projectId,
        string chatId,
        GatheredDataItem item,
        CancellationToken cancellationToken = default)
    {
        var success = await _dataProvider.SaveAsync(projectId, item, cancellationToken);

        if (success)
        {
            // Persist to chat file for page refresh persistence
            var payload = GatheredDataMessagePayload.FromSingleItem(item, $"Gathered: {item.Title}");
            await PersistToChatFileAsync(projectId, chatId, null, payload);

            // Send real-time notification via SignalR
            await _notifier.NotifyDataGatheredAsync(projectId, chatId, item, null, cancellationToken);
        }

        return success;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDataItemAsync(
        string projectId,
        string chatId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var success = await _dataProvider.DeleteAsync(projectId, key, cancellationToken);

        if (success)
        {
            await _notifier.NotifyDataDeletedAsync(projectId, chatId, key, null, cancellationToken);
        }

        return success;
    }

    private List<DevGPTChatMessage> BuildExtractionMessages(
        string userMessage,
        IEnumerable<DevGPTChatMessage> conversationHistory)
    {
        var messages = new List<DevGPTChatMessage>
        {
            new(DevGPTMessageRole.System, _systemPrompt)
        };

        // Include recent conversation context (limited to avoid token overflow)
        var recentHistory = conversationHistory
            .TakeLast(10)
            .ToList();

        if (recentHistory.Count > 0)
        {
            messages.Add(new DevGPTChatMessage(DevGPTMessageRole.System, "Here is the recent conversation context:"));
            messages.AddRange(recentHistory);
        }

        // Add the current message to analyze
        messages.Add(new DevGPTChatMessage(
            DevGPTMessageRole.User,
            $"Analyze this message for any information to extract and store:\n\n{userMessage}"));

        return messages;
    }

    private async Task NotifyGatheredDataAsync(
        string projectId,
        string chatId,
        IReadOnlyList<GatheredDataItem> items,
        string? userId,
        CancellationToken cancellationToken)
    {
        // First, persist to the chat file so data survives page refresh
        var summary = items.Count == 1
            ? $"Gathered: {items[0].Title}"
            : $"Gathered {items.Count} data items";
        var payload = GatheredDataMessagePayload.FromItems(items, summary);
        await PersistToChatFileAsync(projectId, chatId, userId, payload);

        // Then notify clients via SignalR for real-time updates
        if (items.Count == 1)
        {
            await _notifier.NotifyDataGatheredAsync(projectId, chatId, items[0], userId, cancellationToken);
        }
        else if (items.Count > 1)
        {
            await _notifier.NotifyDataGatheredBatchAsync(projectId, chatId, items, userId, cancellationToken);
        }
    }

    /// <summary>
    /// Persists gathered data as a chat message to the chat JSON file.
    /// </summary>
    private async Task PersistToChatFileAsync(
        string projectId,
        string chatId,
        string? userId,
        GatheredDataMessagePayload payload)
    {
        if (_fileLocator == null)
        {
            _logger?.LogDebug("FileLocator not configured, skipping chat file persistence");
            return;
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            _logger?.LogDebug("ChatId is empty, skipping chat file persistence");
            return;
        }

        try
        {
            // Determine the chat file path (user-specific or project-level)
            var chatFile = string.IsNullOrWhiteSpace(userId)
                ? _fileLocator.GetChatFile(projectId, chatId)
                : _fileLocator.GetChatFile(projectId, chatId, userId);

            _logger?.LogDebug("Persisting gathered data to chat file: {ChatFile}", chatFile);

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

            // Create and add the gathered data message
            var message = new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = payload.Summary ?? "Data gathered",
                Payload = payload
            };
                        // Prevent duplicate messages
            if (messages.Count > 0)
            {
                var lastMsg = messages.Last();
                if (lastMsg.Role == ChatMessageRole.Assistant && 
                    (lastMsg.Text == message.Text || (lastMsg.Payload != null && System.Text.Json.JsonSerializer.Serialize(lastMsg.Payload) == System.Text.Json.JsonSerializer.Serialize(message.Payload))))
                {
                    _logger?.LogDebug("Skipping duplicate gathered data message for chat {ChatId}", chatId);
                    return;
                }
            }

            messages.Add(message);

            // Save back to file
            await File.WriteAllTextAsync(chatFile, messages.Serialize());

            _logger?.LogInformation(
                "Persisted gathered data message to chat {ChatId} ({ItemCount} items)",
                chatId, payload.Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist gathered data to chat file for {ChatId}", chatId);
        }
    }

    private static string LoadSystemPrompt(string root)
    {
        try
        {
            var path = Path.Combine(root, ProjectFileLocator.DataGatheringPromptFile);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch { }

        return DataGatheringSystemPromptFallback;
    }
}
