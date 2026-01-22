using Google.Cloud.BigQuery.V2;

namespace Hazina.AgentFactory.Services.BigQuery;

/// <summary>
/// Service for BigQuery operations.
/// Extracted from AgentFactory to follow Single Responsibility Principle.
/// </summary>
public class BigQueryService : IBigQueryService
{
    private readonly string _projectId;
    private readonly string _jsonCredentials;

    public BigQueryService(string projectId, string jsonCredentials)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _jsonCredentials = jsonCredentials ?? throw new ArgumentNullException(nameof(jsonCredentials));
    }

    public BigQueryClient GetClient()
    {
        return new BigQueryClientBuilder
        {
            ProjectId = _projectId,
            JsonCredentials = _jsonCredentials
        }.Build();
    }

    public async Task<string> GetDatasetsAsync(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        var client = GetClient();

        var sqlEu = "SELECT schema_name FROM `region-eu`.INFORMATION_SCHEMA.SCHEMATA;";
        var resultEu = await client.ExecuteQueryAsync(sqlEu, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

        var sqlUs = "SELECT schema_name FROM `region-us`.INFORMATION_SCHEMA.SCHEMATA;";
        var resultUs = await client.ExecuteQueryAsync(sqlUs, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

        var combined = resultEu.Union(resultUs).ToList();
        return ExtractAsString(combined);
    }

    public async Task<string> GetTablesAsync(string dataset, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(dataset))
            return "No dataset provided.";

        var client = GetClient();
        var sql = $"SELECT table_name FROM `{_projectId}.{dataset}.INFORMATION_SCHEMA.TABLES`;";
        var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

        return ExtractAsString(result);
    }

    public async Task<string> GetTableFieldsAsync(string dataset, string tableName, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(dataset))
            return "No dataset provided.";
        if (string.IsNullOrWhiteSpace(tableName))
            return "No table provided.";

        var client = GetClient();
        var sql = $"SELECT table_name, STRING_AGG(CONCAT('- ', column_name, ' (', data_type, ')'), '\\n') AS fields_list FROM `{_projectId}.{dataset}.INFORMATION_SCHEMA.COLUMNS` WHERE table_name='{tableName}' GROUP BY table_name;";
        var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

        return ExtractAsString(result);
    }

    public async Task<string> ExecuteQueryAsync(string sql, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sql))
            return "No query provided.";

        try
        {
            var client = GetClient();
            var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });
            return ExtractAsString(result);
        }
        catch (Exception ex)
        {
            return $"BigQuery error: {ex.Message}";
        }
    }

    public static string ExtractAsString(IEnumerable<BigQueryRow> result)
    {
        var output = new List<string>();
        foreach (var row in result)
        {
            var rowValues = new List<string>();

            foreach (var field in row.Schema.Fields)
            {
                var value = row[field.Name];
                rowValues.Add($"{field.Name}: {value}");
            }

            output.Add(string.Join(", ", rowValues));
        }

        return output.Count > 0
            ? string.Join("\n", output)
            : "Query executed, but no results found.";
    }
}
