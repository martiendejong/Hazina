using System.Text;

namespace Hazina.App.HazinaCoder.Core.Caching;

/// <summary>
/// Smart context compression for token optimization
/// Iteration 36: Context optimization
/// </summary>
public class ContextCompressor
{
    public string CompressContext(string context, int targetTokens)
    {
        var lines = context.Split('\n');

        // Priority-based compression
        var important = new List<string>();
        var normal = new List<string>();
        var verbose = new List<string>();

        foreach (var line in lines)
        {
            if (line.Contains("ERROR") || line.Contains("TODO") || line.Contains("FIXME"))
                important.Add(line);
            else if (line.Trim().StartsWith("//") || line.Trim().StartsWith("#"))
                verbose.Add(line);
            else
                normal.Add(line);
        }

        // Build compressed context
        var sb = new StringBuilder();

        // Always include important lines
        foreach (var line in important)
            sb.AppendLine(line);

        // Add normal lines until token limit
        var estimatedTokens = important.Count * 5; // Rough estimate
        foreach (var line in normal)
        {
            if (estimatedTokens + (line.Length / 4) > targetTokens)
                break;

            sb.AppendLine(line);
            estimatedTokens += line.Length / 4;
        }

        return sb.ToString();
    }

    public string SummarizeOldMessages(List<string> messages)
    {
        if (messages.Count < 5)
            return string.Join("\n", messages);

        // Summarize middle messages, keep first and last
        var sb = new StringBuilder();

        sb.AppendLine(messages[0]);
        sb.AppendLine($"[{messages.Count - 2} messages summarized]");
        sb.AppendLine(messages[^1]);

        return sb.ToString();
    }
}

/// <summary>
/// Predictive cache warmer
/// Iteration 37: Predictive caching
/// </summary>
public class PredictiveCacheWarmer
{
    private readonly Dictionary<string, List<string>> _accessPatterns = new();

    public void RecordAccess(string key, string nextKey)
    {
        if (!_accessPatterns.ContainsKey(key))
            _accessPatterns[key] = new();

        _accessPatterns[key].Add(nextKey);
    }

    public List<string> PredictNextAccess(string currentKey)
    {
        if (!_accessPatterns.ContainsKey(currentKey))
            return new();

        return _accessPatterns[currentKey]
            .GroupBy(k => k)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();
    }

    public async Task WarmCacheAsync(string currentKey, Func<string, Task> loadFunc)
    {
        var predictions = PredictNextAccess(currentKey);

        var tasks = predictions.Select(key => Task.Run(async () =>
        {
            try
            {
                await loadFunc(key);
            }
            catch
            {
                // Ignore warming failures
            }
        }));

        await Task.WhenAll(tasks);
    }
}
