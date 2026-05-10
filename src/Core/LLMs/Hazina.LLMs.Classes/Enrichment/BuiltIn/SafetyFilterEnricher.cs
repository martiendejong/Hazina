using System.Text.RegularExpressions;

namespace Hazina.LLMs.Enrichment.BuiltIn;

/// <summary>
/// Basic content filtering for safety
/// </summary>
public class SafetyFilterEnricher : MessageEnricherBase
{
    public override string Name => "SafetyFilter";
    public override int Priority => 90; // Run late, just before sending

    private readonly SafetyFilterOptions _options;

    public SafetyFilterEnricher(SafetyFilterOptions? options = null)
    {
        _options = options ?? new SafetyFilterOptions();
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

            if (string.IsNullOrEmpty(message.Text))
                continue;

            // Filter blocked patterns
            foreach (var pattern in _options.BlockedPatterns)
            {
                if (Regex.IsMatch(message.Text, pattern, RegexOptions.IgnoreCase))
                {
                    message.Text = _options.BlockedContentReplacement;
                    message.FunctionName = "safety_filtered";
                    break;
                }
            }

            // Redact sensitive patterns (emails, phone numbers, etc.)
            if (_options.RedactSensitiveInfo)
            {
                message.Text = RedactSensitiveInfo(message.Text);
            }

            // Truncate long messages if enabled
            if (_options.MaxContentLength > 0 && message.Text.Length > _options.MaxContentLength)
            {
                message.Text = message.Text.Substring(0, _options.MaxContentLength) +
                    _options.TruncationSuffix;
                message.Response = "truncated";
            }
        }

        return Task.FromResult(enriched);
    }

    private string RedactSensitiveInfo(string content)
    {
        // Redact email addresses
        content = Regex.Replace(
            content,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "[EMAIL_REDACTED]",
            RegexOptions.IgnoreCase);

        // Redact phone numbers (basic pattern)
        content = Regex.Replace(
            content,
            @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b",
            "[PHONE_REDACTED]");

        // Redact credit card numbers (basic pattern)
        content = Regex.Replace(
            content,
            @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
            "[CARD_REDACTED]");

        return content;
    }
}

/// <summary>
/// Options for safety filter enricher
/// </summary>
public class SafetyFilterOptions
{
    public List<string> BlockedPatterns { get; set; } = new();
    public bool RedactSensitiveInfo { get; set; } = true;
    public int MaxContentLength { get; set; } = 0; // 0 = no limit
    public string TruncationSuffix { get; set; } = "... [truncated]";
    public string BlockedContentReplacement { get; set; } = "[Content blocked by safety filter]";
}
