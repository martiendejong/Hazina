using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;

namespace Hazina.LongContext.Planning;

/// <summary>
/// Simple planner that creates a single-node retrieval plan (no recursion).
/// Used for backwards compatibility when recursion is disabled.
/// </summary>
public class SimpleQueryPlanner : IQueryPlanner
{
    public Task<QueryNode> PlanAsync(
        LongContextRequest request,
        CancellationToken ct = default)
    {
        // Create a single root node for retrieval
        var root = new QueryNode
        {
            Type = QueryNodeType.Root,
            Prompt = request.Query,
            Depth = 0,
            Children = new[]
            {
                new QueryNode
                {
                    Type = QueryNodeType.Retrieval,
                    Prompt = request.Query,
                    Depth = 1,
                    ParentId = Guid.Empty, // Will be set to root's ID
                    Metadata = new Dictionary<string, object>
                    {
                        ["maxShards"] = 20,
                        ["tags"] = request.Tags.ToList(),
                        ["metadataFilters"] = request.MetadataFilters
                    }
                }
            }
        };

        return Task.FromResult(root);
    }
}
