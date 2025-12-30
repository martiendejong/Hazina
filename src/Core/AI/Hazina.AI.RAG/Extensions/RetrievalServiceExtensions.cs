using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Interfaces;
using Hazina.AI.RAG.Retrieval;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.RAG.Extensions;

/// <summary>
/// DI extension methods for registering retrieval pipeline components
/// </summary>
public static class RetrievalServiceExtensions
{
    /// <summary>
    /// Register the default retrieval pipeline with vector store retriever and no-op reranker
    /// </summary>
    public static IServiceCollection AddRetrievalPipeline(
        this IServiceCollection services,
        Action<RetrievalPipelineOptions>? configure = null)
    {
        var options = new RetrievalPipelineOptions();
        configure?.Invoke(options);

        services.AddSingleton<IRetriever, VectorStoreRetriever>();

        switch (options.RerankingStrategy)
        {
            case RerankingStrategy.None:
                services.AddSingleton<IReranker, NoOpReranker>();
                break;

            case RerankingStrategy.LlmJudge:
                services.AddSingleton<IReranker>(sp =>
                {
                    var orchestrator = sp.GetRequiredService<IProviderOrchestrator>();
                    return new LlmJudgeReranker(orchestrator, options.LlmJudgeOptions);
                });
                break;

            default:
                services.AddSingleton<IReranker, NoOpReranker>();
                break;
        }

        services.AddSingleton<IRetrievalPipeline, RetrievalPipeline>();

        return services;
    }

    /// <summary>
    /// Register a custom retriever implementation
    /// </summary>
    public static IServiceCollection AddRetriever<T>(this IServiceCollection services)
        where T : class, IRetriever
    {
        services.AddSingleton<IRetriever, T>();
        return services;
    }

    /// <summary>
    /// Register a custom reranker implementation
    /// </summary>
    public static IServiceCollection AddReranker<T>(this IServiceCollection services)
        where T : class, IReranker
    {
        services.AddSingleton<IReranker, T>();
        return services;
    }

    /// <summary>
    /// Register a custom retrieval pipeline implementation
    /// </summary>
    public static IServiceCollection AddCustomRetrievalPipeline<T>(this IServiceCollection services)
        where T : class, IRetrievalPipeline
    {
        services.AddSingleton<IRetrievalPipeline, T>();
        return services;
    }
}

/// <summary>
/// Configuration options for retrieval pipeline
/// </summary>
public class RetrievalPipelineOptions
{
    /// <summary>
    /// Reranking strategy to use
    /// </summary>
    public RerankingStrategy RerankingStrategy { get; set; } = RerankingStrategy.None;

    /// <summary>
    /// Options for LLM judge reranker (if using LlmJudge strategy)
    /// </summary>
    public LlmJudgeRerankerOptions? LlmJudgeOptions { get; set; }
}

/// <summary>
/// Reranking strategy enumeration
/// </summary>
public enum RerankingStrategy
{
    None,
    LlmJudge
}
