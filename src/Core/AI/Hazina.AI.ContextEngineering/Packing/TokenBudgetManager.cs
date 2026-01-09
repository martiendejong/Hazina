using Hazina.AI.ContextEngineering.Configuration;
using System.Text;

namespace Hazina.AI.ContextEngineering.Packing;

/// <summary>
/// Manages token budgets and trimming for context packing
/// </summary>
public class TokenBudgetManager
{
    /// <summary>
    /// Trim context to fit within MaxTokens budget
    /// </summary>
    /// <param name="fullContext">Full assembled context</param>
    /// <param name="sections">Individual sections</param>
    /// <param name="policy">Packing policy</param>
    /// <param name="tokenEstimator">Function to estimate tokens</param>
    /// <returns>Trimmed context</returns>
    public string TrimToFit(
        string fullContext,
        Dictionary<string, string> sections,
        PackingPolicy policy,
        Func<string, int> tokenEstimator)
    {
        var currentTokens = tokenEstimator(fullContext);
        if (currentTokens <= policy.MaxTokens)
            return fullContext;

        // Build priority map (lower index = higher priority, trim last)
        var priorityMap = policy.TrimPriority
            .Select((name, index) => new { name, priority = index })
            .ToDictionary(x => x.name, x => x.priority);

        // Sort sections by priority (highest priority first = lowest index)
        var sortedSections = sections
            .OrderBy(kvp => priorityMap.ContainsKey(kvp.Key) ? priorityMap[kvp.Key] : int.MaxValue)
            .ToList();

        // Keep trimming sections from lowest priority until we fit
        var trimmedSections = new Dictionary<string, string>(sections);
        var tokensToRemove = currentTokens - policy.MaxTokens;

        // Start from lowest priority (end of list)
        for (int i = sortedSections.Count - 1; i >= 0 && tokensToRemove > 0; i--)
        {
            var sectionName = sortedSections[i].Key;
            var sectionContent = sortedSections[i].Value;
            var sectionTokens = tokenEstimator(sectionContent);

            if (sectionTokens > tokensToRemove)
            {
                // Trim this section partially
                var targetTokens = sectionTokens - tokensToRemove;
                var targetChars = (int)(targetTokens * 4); // Reverse of estimation
                var trimmedContent = TruncateToLength(sectionContent, targetChars);
                trimmedSections[sectionName] = trimmedContent;
                tokensToRemove = 0;
            }
            else
            {
                // Remove entire section
                trimmedSections.Remove(sectionName);
                tokensToRemove -= sectionTokens;
            }
        }

        // Reassemble trimmed context
        return AssembleSections(trimmedSections, policy);
    }

    private string AssembleSections(Dictionary<string, string> sections, PackingPolicy policy)
    {
        var sb = new StringBuilder();
        bool first = true;

        foreach (var sectionName in policy.Sections)
        {
            if (!sections.ContainsKey(sectionName))
                continue;

            if (!first)
            {
                sb.Append(policy.SectionSeparator);
            }

            var header = string.Format(policy.SectionHeaderFormat, sectionName.ToUpperInvariant());
            sb.AppendLine(header);
            sb.Append(sections[sectionName]);

            first = false;
        }

        return sb.ToString();
    }

    private string TruncateToLength(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        // Try to truncate at a line break
        var truncated = text.Substring(0, maxLength);
        var lastNewline = truncated.LastIndexOf('\n');

        if (lastNewline > maxLength * 0.8) // If we're close to target, use the newline
        {
            return truncated.Substring(0, lastNewline) + "\n... (trimmed)";
        }

        return truncated + "... (trimmed)";
    }
}
