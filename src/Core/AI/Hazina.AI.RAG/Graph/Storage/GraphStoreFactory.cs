namespace Hazina.AI.RAG.Graph.Storage;

using Hazina.AI.RAG.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating appropriate IGraphStore implementations based on configuration.
/// </summary>
public static class GraphStoreFactory
{
    /// <summary>
    /// Creates a graph store based on the provided configuration.
    /// </summary>
    /// <param name="config">GraphRAG configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Configured IGraphStore implementation</returns>
    public static IGraphStore Create(GraphRAGConfig config, ILogger logger)
    {
        return config.StorageType switch
        {
            GraphStorageType.SQLite => new SQLiteGraphStore(config.SQLitePath, (ILogger<SQLiteGraphStore>)logger),
            GraphStorageType.InMemory => new InMemoryGraphStore(),
            GraphStorageType.Neo4j => throw new NotImplementedException("Neo4j storage implementation coming in future release"),
            _ => throw new ArgumentException($"Unsupported storage type: {config.StorageType}")
        };
    }
}
