using Hazina.AI.CognitivePipeline.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 2: Organization.
/// Clusters facts by topic/category and structures relationships.
/// Sets SCPContext.OrganizedData with clustered, structured data.
/// </summary>
public class OrganizationStage : ISCPStage
{
    private readonly ILogger<OrganizationStage> _logger;

    public SCPStageType StageType => SCPStageType.Organization;
    public int Order => 2;
    public string Name => "Organization";

    public OrganizationStage(ILogger<OrganizationStage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };

        // Cluster facts by topic prefix (e.g., "artist-name" and "artist-birth" share "artist" cluster)
        var clusters = new Dictionary<string, List<KeyValuePair<string, object>>>();

        foreach (var entry in context.OrganizedData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cluster = ExtractCluster(entry.Key);
            if (!clusters.ContainsKey(cluster))
                clusters[cluster] = new List<KeyValuePair<string, object>>();

            clusters[cluster].Add(entry);
        }

        // Store clusters in metadata for downstream stages
        context.Metadata["OrganizedClusters"] = clusters;
        context.Metadata["ClusterCount"] = clusters.Count;

        // Build relationships: facts in the same cluster are related
        var relationships = new List<string>();
        foreach (var cluster in clusters.Where(c => c.Value.Count > 1))
        {
            var keys = cluster.Value.Select(kv => kv.Key).ToList();
            for (var i = 0; i < keys.Count - 1; i++)
            {
                relationships.Add($"{keys[i]} <-> {keys[i + 1]}");
            }
        }
        context.Metadata["Relationships"] = relationships;

        result.Confidence = clusters.Count > 0 ? 1.0 : 0.0;
        result.Findings.Add($"Organized {context.OrganizedData.Count} facts into {clusters.Count} clusters with {relationships.Count} relationships");
        result.Data["ClusterCount"] = clusters.Count;
        result.Data["RelationshipCount"] = relationships.Count;
        result.Success = true;

        _logger.LogDebug("Organization: {Count} clusters, {Relationships} relationships", clusters.Count, relationships.Count);

        return Task.FromResult(result);
    }

    private static string ExtractCluster(string key)
    {
        // Cluster by first segment of hyphenated key
        var dashIndex = key.IndexOf('-');
        return dashIndex > 0 ? key[..dashIndex] : key;
    }
}
