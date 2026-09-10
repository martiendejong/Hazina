namespace Hazina.AgenticOrchestration.Services.PersistentSession;

/// <summary>
/// Rolling context window manager - prevents context window overflow
/// Pattern: ChatGPT infinite conversation (summarization + truncation)
/// </summary>
public interface IRollingContextWindow
{
    /// <summary>Add message to context (auto-truncates if needed)</summary>
    Task AddMessageAsync(ContextMessage message);

    /// <summary>Get current context for LLM call</summary>
    List<ContextMessage> GetContext();

    /// <summary>Get context statistics</summary>
    ContextStatistics GetStatistics();

    /// <summary>Force truncation now</summary>
    Task TruncateAsync(int targetSize);
}

public class RollingContextWindow : IRollingContextWindow
{
    private readonly RollingContext _context;
    private readonly int _maxTokens;
    private readonly int _targetTokensAfterTruncation;

    public RollingContextWindow(
        RollingContext context,
        int maxTokens = 180000,           // ~90% of 200K limit
        int targetTokensAfterTruncation = 100000) // Leave room for response
    {
        _context = context;
        _maxTokens = maxTokens;
        _targetTokensAfterTruncation = targetTokensAfterTruncation;
    }

    public async Task AddMessageAsync(ContextMessage message)
    {
        // Estimate tokens (rough: 1 token ≈ 4 characters)
        message.EstimatedTokens = EstimateTokens(message.Content);

        _context.Messages.Add(message);

        // Check if we need to truncate
        var currentTokens = _context.Messages.Sum(m => m.EstimatedTokens);

        if (currentTokens > _maxTokens)
        {
            await TruncateAsync(_targetTokensAfterTruncation);
        }
    }

    public List<ContextMessage> GetContext()
    {
        return _context.Messages.ToList();
    }

    public ContextStatistics GetStatistics()
    {
        var totalTokens = _context.Messages.Sum(m => m.EstimatedTokens);
        var utilizationPercent = (double)totalTokens / _maxTokens * 100;

        return new ContextStatistics
        {
            TotalMessages = _context.Messages.Count,
            TotalTokens = totalTokens,
            MaxTokens = _maxTokens,
            UtilizationPercent = utilizationPercent,
            TruncatedCount = _context.TruncatedCount
        };
    }

    public async Task TruncateAsync(int targetTokens)
    {
        // Strategy: Keep first message (system prompt) + recent messages
        // Summarize/truncate middle messages

        if (_context.Messages.Count == 0) return;

        var systemMessage = _context.Messages.FirstOrDefault(m => m.Role == "system");
        var currentTokens = _context.Messages.Sum(m => m.EstimatedTokens);

        if (currentTokens <= targetTokens) return; // No truncation needed

        // Keep: system message + most recent N messages
        var messagesToKeep = new List<ContextMessage>();

        if (systemMessage != null)
        {
            messagesToKeep.Add(systemMessage);
        }

        // Add recent messages from end until we hit target
        var tokensUsed = systemMessage?.EstimatedTokens ?? 0;

        for (int i = _context.Messages.Count - 1; i >= 0; i--)
        {
            var msg = _context.Messages[i];

            if (msg == systemMessage) continue; // Already added

            if (tokensUsed + msg.EstimatedTokens <= targetTokens)
            {
                messagesToKeep.Insert(systemMessage != null ? 1 : 0, msg);
                tokensUsed += msg.EstimatedTokens;
            }
            else
            {
                break; // Can't fit more
            }
        }

        var truncatedCount = _context.Messages.Count - messagesToKeep.Count;

        _context.Messages.Clear();
        _context.Messages.AddRange(messagesToKeep);
        _context.TruncatedCount += truncatedCount;

        await Task.CompletedTask; // Placeholder for async summarization in future
    }

    private int EstimateTokens(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters
        // This is conservative (over-estimates)
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

public class ContextStatistics
{
    public int TotalMessages { get; set; }
    public int TotalTokens { get; set; }
    public int MaxTokens { get; set; }
    public double UtilizationPercent { get; set; }
    public int TruncatedCount { get; set; }
}
