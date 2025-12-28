using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Memory;
using Hazina.LLMs.GoogleADK.Memory.Models;
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// Agent with both session management and memory bank capabilities
/// </summary>
public class MemoryEnabledAgent : SessionEnabledAgent
{
    private readonly MemoryBank _memoryBank;
    private readonly MemoryConfiguration _memoryConfig;

    public MemoryEnabledAgent(
        string name,
        ILLMClient llmClient,
        SessionManager sessionManager,
        MemoryBank memoryBank,
        MemoryConfiguration? memoryConfig = null,
        AgentContext? context = null,
        int maxHistorySize = 50) : base(name, llmClient, sessionManager, context, maxHistorySize)
    {
        _memoryBank = memoryBank ?? throw new ArgumentNullException(nameof(memoryBank));
        _memoryConfig = memoryConfig ?? new MemoryConfiguration();
    }

    /// <summary>
    /// Execute with session and memory tracking
    /// </summary>
    public async Task<AgentResult> ExecuteWithMemoryAsync(
        string input,
        bool autoCreateSession = true,
        bool storeInMemory = true,
        CancellationToken cancellationToken = default)
    {
        // Retrieve relevant memories before execution
        var relevantMemories = await RetrieveRelevantMemoriesAsync(input, cancellationToken);

        // Build context from memories
        var memoryContext = BuildMemoryContext(relevantMemories);

        // Augment system instructions with memory context
        var originalInstructions = SystemInstructions;
        if (!string.IsNullOrEmpty(memoryContext))
        {
            SystemInstructions = $"{originalInstructions}\n\nRelevant memories:\n{memoryContext}";
        }

        // Execute with session
        var result = await ExecuteWithSessionAsync(input, autoCreateSession, cancellationToken);

        // Restore original instructions
        SystemInstructions = originalInstructions;

        // Store interaction in memory if configured
        if (storeInMemory && CurrentSession != null)
        {
            await StoreInteractionAsync(input, result.Output, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Retrieve relevant memories for the current input
    /// </summary>
    private async Task<List<MemorySearchResult>> RetrieveRelevantMemoriesAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var query = new MemoryQuery
        {
            QueryText = input,
            AgentName = Name,
            UserId = CurrentSession?.UserId,
            Limit = _memoryConfig.MaxRetrievedMemories,
            MinImportance = _memoryConfig.MinImportanceThreshold
        };

        // Add type filters if configured
        if (_memoryConfig.RetrieveSemanticMemories && _memoryConfig.RetrieveEpisodicMemories)
        {
            // Retrieve both types
            return await _memoryBank.SearchAsync(query, cancellationToken);
        }
        else if (_memoryConfig.RetrieveSemanticMemories)
        {
            query.Type = MemoryType.Semantic;
            return await _memoryBank.SearchAsync(query, cancellationToken);
        }
        else if (_memoryConfig.RetrieveEpisodicMemories)
        {
            query.Type = MemoryType.Episodic;
            return await _memoryBank.SearchAsync(query, cancellationToken);
        }

        return new List<MemorySearchResult>();
    }

    /// <summary>
    /// Build context string from retrieved memories
    /// </summary>
    private string BuildMemoryContext(List<MemorySearchResult> memories)
    {
        if (!memories.Any()) return string.Empty;

        var contextBuilder = new System.Text.StringBuilder();

        foreach (var memory in memories.Take(_memoryConfig.MaxRetrievedMemories))
        {
            contextBuilder.AppendLine($"- [{memory.Memory.Type}] {memory.Memory.Content}");
        }

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Store interaction in memory bank
    /// </summary>
    private async Task StoreInteractionAsync(
        string userInput,
        string agentResponse,
        CancellationToken cancellationToken)
    {
        // Store user input as episodic memory
        if (_memoryConfig.StoreUserInputs)
        {
            await _memoryBank.StoreMemoryAsync(
                content: $"User said: {userInput}",
                type: MemoryType.Episodic,
                agentName: Name,
                userId: CurrentSession?.UserId,
                sessionId: CurrentSession?.SessionId,
                importance: CalculateImportance(userInput),
                tags: new List<string> { "user-input", "conversation" },
                cancellationToken: cancellationToken
            );
        }

        // Store agent response as episodic memory
        if (_memoryConfig.StoreAgentResponses)
        {
            await _memoryBank.StoreMemoryAsync(
                content: $"Agent responded: {agentResponse}",
                type: MemoryType.Episodic,
                agentName: Name,
                userId: CurrentSession?.UserId,
                sessionId: CurrentSession?.SessionId,
                importance: CalculateImportance(agentResponse),
                tags: new List<string> { "agent-response", "conversation" },
                cancellationToken: cancellationToken
            );
        }

        Context.Log(LogLevel.Debug, "Stored interaction in memory bank");
    }

    /// <summary>
    /// Store a semantic memory (fact/knowledge)
    /// </summary>
    public async Task<MemoryItem> StoreSemanticMemoryAsync(
        string content,
        double importance = 0.7,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var memory = await _memoryBank.StoreMemoryAsync(
            content: content,
            type: MemoryType.Semantic,
            agentName: Name,
            userId: CurrentSession?.UserId,
            sessionId: CurrentSession?.SessionId,
            importance: importance,
            tags: tags ?? new List<string> { "knowledge", "fact" },
            cancellationToken: cancellationToken
        );

        Context.Log(LogLevel.Information, "Stored semantic memory: {MemoryId}", memory.MemoryId);
        return memory;
    }

    /// <summary>
    /// Store a procedural memory (how-to)
    /// </summary>
    public async Task<MemoryItem> StoreProceduralMemoryAsync(
        string content,
        double importance = 0.6,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var memory = await _memoryBank.StoreMemoryAsync(
            content: content,
            type: MemoryType.Procedural,
            agentName: Name,
            userId: CurrentSession?.UserId,
            sessionId: CurrentSession?.SessionId,
            importance: importance,
            tags: tags ?? new List<string> { "procedure", "how-to" },
            cancellationToken: cancellationToken
        );

        Context.Log(LogLevel.Information, "Stored procedural memory: {MemoryId}", memory.MemoryId);
        return memory;
    }

    /// <summary>
    /// Search agent's memories
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchMemoriesAsync(
        string queryText,
        MemoryType? type = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await _memoryBank.SearchByTextAsync(
            queryText: queryText,
            type: type,
            agentName: Name,
            userId: CurrentSession?.UserId,
            limit: limit,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get recent memories for this agent
    /// </summary>
    public async Task<List<MemoryItem>> GetRecentMemoriesAsync(
        int count = 10,
        MemoryType? type = null,
        CancellationToken cancellationToken = default)
    {
        return await _memoryBank.GetRecentMemoriesAsync(
            agentName: Name,
            userId: CurrentSession?.UserId,
            type: type,
            limit: count,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Calculate importance score for text
    /// </summary>
    private double CalculateImportance(string text)
    {
        // Simple heuristic based on length and keywords
        var baseImportance = 0.5;

        // Longer texts might be more important
        if (text.Length > 200)
        {
            baseImportance += 0.1;
        }

        // Questions might be more important
        if (text.Contains("?"))
        {
            baseImportance += 0.1;
        }

        // Keywords that suggest importance
        var importantKeywords = new[] { "important", "remember", "always", "never", "must", "critical" };
        if (importantKeywords.Any(k => text.ToLowerInvariant().Contains(k)))
        {
            baseImportance += 0.2;
        }

        return Math.Clamp(baseImportance, 0.0, 1.0);
    }
}

/// <summary>
/// Configuration for memory-enabled agents
/// </summary>
public class MemoryConfiguration
{
    /// <summary>
    /// Maximum number of memories to retrieve per query
    /// </summary>
    public int MaxRetrievedMemories { get; set; } = 5;

    /// <summary>
    /// Minimum importance threshold for retrieval
    /// </summary>
    public double MinImportanceThreshold { get; set; } = 0.3;

    /// <summary>
    /// Whether to retrieve semantic memories
    /// </summary>
    public bool RetrieveSemanticMemories { get; set; } = true;

    /// <summary>
    /// Whether to retrieve episodic memories
    /// </summary>
    public bool RetrieveEpisodicMemories { get; set; } = true;

    /// <summary>
    /// Whether to store user inputs as memories
    /// </summary>
    public bool StoreUserInputs { get; set; } = true;

    /// <summary>
    /// Whether to store agent responses as memories
    /// </summary>
    public bool StoreAgentResponses { get; set; } = false;

    /// <summary>
    /// Auto-consolidate weak memories
    /// </summary>
    public bool AutoConsolidate { get; set; } = true;
}
