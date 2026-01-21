using Google.Cloud.BigQuery.V2;

namespace Hazina.AgentFactory.Services.BigQuery;

/// <summary>
/// Interface for BigQuery operations.
/// </summary>
public interface IBigQueryService
{
    BigQueryClient GetClient();
    Task<string> GetDatasetsAsync(CancellationToken cancel);
    Task<string> GetTablesAsync(string dataset, CancellationToken cancel);
    Task<string> GetTableFieldsAsync(string dataset, string tableName, CancellationToken cancel);
    Task<string> ExecuteQueryAsync(string sql, CancellationToken cancel);
}
