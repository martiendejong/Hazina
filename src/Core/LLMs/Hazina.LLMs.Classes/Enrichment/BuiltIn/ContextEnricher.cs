namespace Hazina.LLMs.Enrichment.BuiltIn;

/// <summary>
/// Adds conversation context to messages
/// </summary>
public class ContextEnricher : MessageEnricherBase
{
    public override string Name => "Context";
    public override int Priority => 20; // Run after metadata

    private readonly ContextEnricherOptions _options;

    public ContextEnricher(ContextEnricherOptions? options = null)
    {
        _options = options ?? new ContextEnricherOptions();
    }

    public override Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        var enriched = CloneMessages(messages);

        // Add system context message if enabled and not already present
        if (_options.AddSystemContext && !enriched.Any(m => m.Role.Role == HazinaMessageRole.System.Role))
        {
            var systemMessage = new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = _options.SystemContextTemplate
                    .Replace("{timestamp}", DateTimeOffset.UtcNow.ToString("O"))
                    .Replace("{conversation_id}", context.ConversationId ?? "unknown")
                    .Replace("{user_id}", context.UserId ?? "unknown")
            };

            enriched.Insert(0, systemMessage);
        }

        // Add conversation summary if available
        if (_options.AddConversationSummary && context.EnricherData.TryGetValue("conversation_summary", out var summary))
        {
            var summaryMessage = new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = $"Previous conversation summary: {summary}"
            };

            // Insert after system message
            var insertIndex = enriched.FindIndex(m => m.Role.Role == HazinaMessageRole.System.Role) + 1;
            if (insertIndex <= 0) insertIndex = 0;
            enriched.Insert(insertIndex, summaryMessage);
        }

        // Trim old messages if limit is set
        if (_options.MaxMessages > 0 && enriched.Count > _options.MaxMessages)
        {
            // Keep system messages and recent messages
            var systemMessages = enriched.Where(m => m.Role.Role == HazinaMessageRole.System.Role).ToList();
            var otherMessages = enriched.Where(m => m.Role.Role != HazinaMessageRole.System.Role).ToList();

            var keepCount = _options.MaxMessages - systemMessages.Count;
            var recentMessages = otherMessages.TakeLast(keepCount).ToList();

            enriched = systemMessages.Concat(recentMessages).ToList();
        }

        return Task.FromResult(enriched);
    }
}

/// <summary>
/// Options for context enricher
/// </summary>
public class ContextEnricherOptions
{
    public bool AddSystemContext { get; set; } = true;
    public bool AddConversationSummary { get; set; } = true;
    public int MaxMessages { get; set; } = 0; // 0 = no limit

    public string SystemContextTemplate { get; set; } =
        "Current timestamp: {timestamp}. Conversation ID: {conversation_id}.";
}
