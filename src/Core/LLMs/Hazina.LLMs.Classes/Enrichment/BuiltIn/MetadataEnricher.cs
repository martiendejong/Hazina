namespace Hazina.LLMs.Enrichment.BuiltIn;

/// <summary>
/// Adds metadata to messages (AgentName, FlowName fields)
/// </summary>
public class MetadataEnricher : MessageEnricherBase
{
    public override string Name => "Metadata";
    public override int Priority => 10; // Run early

    private readonly MetadataEnricherOptions _options;

    public MetadataEnricher(MetadataEnricherOptions? options = null)
    {
        _options = options ?? new MetadataEnricherOptions();
    }

    public override Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        var enriched = CloneMessages(messages);

        for (int i = 0; i < enriched.Count; i++)
        {
            var message = enriched[i];

            // Add conversation ID to AgentName field if enabled and empty
            if (_options.AddConversationId &&
                string.IsNullOrEmpty(message.AgentName) &&
                context.ConversationId != null)
            {
                message.AgentName = context.ConversationId;
            }

            // Add flow name (could be session ID)
            if (_options.AddFlowName &&
                string.IsNullOrEmpty(message.FlowName))
            {
                message.FlowName = context.Metadata.TryGetValue("flow_name", out var flowName)
                    ? flowName.ToString() ?? ""
                    : "";
            }

            // Store metadata in context for later access
            if (_options.StoreMetadataInContext)
            {
                context.EnricherData[$"message_{i}_id"] = message.MessageId;
                context.EnricherData[$"message_{i}_role"] = message.Role.Role;
            }
        }

        return Task.FromResult(enriched);
    }
}

/// <summary>
/// Options for metadata enricher
/// </summary>
public class MetadataEnricherOptions
{
    public bool AddConversationId { get; set; } = true;
    public bool AddFlowName { get; set; } = true;
    public bool StoreMetadataInContext { get; set; } = false;
}
