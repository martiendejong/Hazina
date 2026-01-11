using Hazina.AI.Providers.Core;
using Hazina.LLMs;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;
using System.Diagnostics;

namespace Hazina.LongContext.Execution;

/// <summary>
/// Executes query nodes based on their type.
/// Supports Retrieval, Summarization, and Aggregation nodes.
/// </summary>
public class QueryNodeExecutor : IQueryNodeExecutor
{
    private readonly IContextShardProvider _shardProvider;
    private readonly IProviderOrchestrator _llmOrchestrator;

    public QueryNodeExecutor(
        IContextShardProvider shardProvider,
        IProviderOrchestrator llmOrchestrator)
    {
        _shardProvider = shardProvider ?? throw new ArgumentNullException(nameof(shardProvider));
        _llmOrchestrator = llmOrchestrator ?? throw new ArgumentNullException(nameof(llmOrchestrator));
    }

    public async Task<QueryNodeResult> ExecuteNodeAsync(
        QueryNode node,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        node.Status = QueryNodeStatus.InProgress;

        try
        {
            QueryNodeResult result = node.Type switch
            {
                QueryNodeType.Retrieval => await ExecuteRetrievalNodeAsync(node, ct),
                QueryNodeType.Summarization => await ExecuteSummarizationNodeAsync(node, ct),
                QueryNodeType.Aggregation => await ExecuteAggregationNodeAsync(node, ct),
                QueryNodeType.Root => await ExecuteRootNodeAsync(node, ct),
                _ => throw new NotSupportedException($"Node type {node.Type} not yet implemented")
            };

            node.Status = QueryNodeStatus.Completed;

            // Update execution time
            return new QueryNodeResult
            {
                NodeId = result.NodeId,
                Answer = result.Answer,
                UsedShards = result.UsedShards,
                TokensUsed = result.TokensUsed,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                ChildResults = result.ChildResults,
                ExecutionTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            node.Status = QueryNodeStatus.Failed;
            return new QueryNodeResult
            {
                NodeId = node.Id,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = sw.Elapsed
            };
        }
    }

    private async Task<QueryNodeResult> ExecuteRetrievalNodeAsync(QueryNode node, CancellationToken ct)
    {
        var maxShards = node.Metadata.TryGetValue("maxShards", out var max) && max is int m ? m : 20;

        // Retrieve shards
        var shards = await _shardProvider.FindRelevantShardsAsync(
            node.Prompt,
            maxShards: maxShards,
            ct: ct);

        // Build context from shards
        var context = string.Join("\n\n", shards.Select(s => $"[Score: {s.Relevance:F2}]\n{s.Content}"));

        // Generate answer using LLM
        var answer = await GenerateAnswerAsync(node.Prompt, context, ct);

        return new QueryNodeResult
        {
            NodeId = node.Id,
            Answer = answer,
            UsedShards = shards,
            TokensUsed = EstimateTokens(context + answer),
            Success = true
        };
    }

    private async Task<QueryNodeResult> ExecuteSummarizationNodeAsync(QueryNode node, CancellationToken ct)
    {
        // Execute children first
        var childResults = new List<QueryNodeResult>();
        foreach (var child in node.Children)
        {
            var result = await ExecuteNodeAsync(child, ct);
            childResults.Add(result);
        }

        // Combine child answers
        var combinedContext = string.Join("\n\n---\n\n", childResults.Select(r => r.Answer));

        // Summarize
        var summary = await SummarizeAsync(node.Prompt, combinedContext, ct);

        // Collect all shards from children
        var allShards = childResults.SelectMany(r => r.UsedShards).ToList();

        return new QueryNodeResult
        {
            NodeId = node.Id,
            Answer = summary,
            UsedShards = allShards,
            TokensUsed = childResults.Sum(r => r.TokensUsed) + EstimateTokens(summary),
            ChildResults = childResults,
            Success = true
        };
    }

    private async Task<QueryNodeResult> ExecuteAggregationNodeAsync(QueryNode node, CancellationToken ct)
    {
        // Similar to summarization but with different prompt
        var childResults = new List<QueryNodeResult>();
        foreach (var child in node.Children)
        {
            var result = await ExecuteNodeAsync(child, ct);
            childResults.Add(result);
        }

        var combinedAnswers = string.Join("\n\n", childResults.Select((r, i) => $"Sub-answer {i + 1}:\n{r.Answer}"));

        var aggregated = await AggregateAsync(node.Prompt, combinedAnswers, ct);

        var allShards = childResults.SelectMany(r => r.UsedShards).ToList();

        return new QueryNodeResult
        {
            NodeId = node.Id,
            Answer = aggregated,
            UsedShards = allShards,
            TokensUsed = childResults.Sum(r => r.TokensUsed) + EstimateTokens(aggregated),
            ChildResults = childResults,
            Success = true
        };
    }

    private async Task<QueryNodeResult> ExecuteRootNodeAsync(QueryNode node, CancellationToken ct)
    {
        // Root node delegates to its single child (retrieval node)
        if (node.Children.Count == 0)
        {
            throw new InvalidOperationException("Root node must have at least one child");
        }

        var childResult = await ExecuteNodeAsync(node.Children[0], ct);

        return new QueryNodeResult
        {
            NodeId = node.Id,
            Answer = childResult.Answer,
            UsedShards = childResult.UsedShards,
            TokensUsed = childResult.TokensUsed,
            ChildResults = new[] { childResult },
            Success = childResult.Success,
            ErrorMessage = childResult.ErrorMessage
        };
    }

    private async Task<string> GenerateAnswerAsync(string question, string context, CancellationToken ct)
    {
        var prompt = $"Context:\n{context}\n\nQuestion: {question}\n\nAnswer based on the context provided:";

        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant. Answer questions based on the provided context." },
            new() { Role = HazinaMessageRole.User, Text = prompt }
        };

        var response = await _llmOrchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, ct);
        return response.Result;
    }

    private async Task<string> SummarizeAsync(string prompt, string content, CancellationToken ct)
    {
        var summarizePrompt = $"Summarize the following content in relation to: {prompt}\n\nContent:\n{content}";

        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant. Provide concise summaries." },
            new() { Role = HazinaMessageRole.User, Text = summarizePrompt }
        };

        var response = await _llmOrchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, ct);
        return response.Result;
    }

    private async Task<string> AggregateAsync(string question, string combinedAnswers, CancellationToken ct)
    {
        var aggregatePrompt = $"Question: {question}\n\nSub-answers:\n{combinedAnswers}\n\nProvide a final comprehensive answer:";

        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant. Synthesize multiple answers into a coherent response." },
            new() { Role = HazinaMessageRole.User, Text = aggregatePrompt }
        };

        var response = await _llmOrchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, ct);
        return response.Result;
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: 1 token ≈ 4 characters
        return text.Length / 4;
    }
}
