using Google.Cloud.BigQuery.V2;
using OpenAI.Chat;
using System.Text.Json;
using Hazina.Tools.Data;
using Hazina.Tools.Models.BigQuery;
using Hazina.Tools.Models;

namespace Hazina.Tools.Services.BigQuery
{
    public class BigQueryContext : ToolsContextBase
    {
        public string ProjectId { get; set; }
        public string DatasetId { get; set; }
        public ProjectsRepository Projects { get; set; }
        public string ApiKey { get; set; }

        public List<ConnectedBigQueryAccount> Accounts { get; set; } = new List<ConnectedBigQueryAccount>();

        public BigQueryContext()
        {
            // TODO: Migrate to new DevGPT.Generator/AgentFactory tool infrastructure
            // DevGPT.LLMs namespace no longer exists in v1.0.10+
            // Original tool registration code commented out - requires migration
        }

        private static string BigQuery_ExtractAsString(BigQueryResults result)
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
                ? string.Join("\n", output)  // Limit output for GPT
                : "Query executed, but no results found.";
        }

        private async Task<string> PerformGetTables()
        {
            try
            {
                BigQueryClient client = BigQuery_GetClient();

                var result = await client.ExecuteQueryAsync(BigQueries.GetTablesQuery, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });
                // todo filter to only show the tables that are imported in the project?
                return BigQuery_ExtractAsString(result);
            }
            catch (Exception ex)
            {
                return $"BigQuery error: {ex.Message}";
            }
        }

        private async Task<string> PerformGetTableColumns(string table)
        {
            try
            {
                BigQueryClient client = BigQuery_GetClient();

                var result = await client.ExecuteQueryAsync(BigQueries.GetColumnsQuery.Replace("{table}", table), parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                return BigQuery_ExtractAsString(result);
            }
            catch (Exception ex)
            {
                return $"BigQuery error: {ex.Message}";
            }
        }

        private async Task<string> PerformCallAgent(string prompt, string projectid)
        {
            // TODO: Implement agent calling functionality
            // This requires AgentFactory, QuickAgentCreator, and StorePaths classes
            // which are not yet implemented in the codebase
            await Task.CompletedTask;
            return "Agent calling functionality not yet implemented.";
        }

        private async Task<string> PerformGetBigQueryResults(string select, string table, string where = "", string orderby = "", string groupby = "", string having = "", string limit = "")
        {
            //var subquery = $"SELECT * FROM {table} AS ";
            where = string.IsNullOrWhiteSpace(where) ? "" : $"WHERE {where}";
            orderby = string.IsNullOrWhiteSpace(orderby) ? "" : $"ORDER BY {orderby}";
            groupby = string.IsNullOrWhiteSpace(groupby) ? "" : $"GROUP BY {groupby}";
            having = string.IsNullOrWhiteSpace(having) ? "" : $"HAVING {having}";
            limit = string.IsNullOrWhiteSpace(limit) ? "" : $"LIMIT {limit}";

            var from = $"FROM (SELECT * FROM `{ProjectId}.{DatasetId}.{table}` WHERE account_id IN ({string.Join(",", Accounts.Select(s => $"'{s.Id}'"))})) AS `{table}`";

            var query = $"SELECT {select} {from} {where} {groupby} {having} {orderby} {limit}";

            try
            {
                BigQueryClient client = BigQuery_GetClient();

                var result = await client.ExecuteQueryAsync(query, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                return BigQuery_ExtractAsString(result);
            }
            catch (Exception ex)
            {
                return $"BigQuery error: {ex.Message}";
            }
        }

        private BigQueryClient BigQuery_GetClient()
        {
            var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "googleaccount.json");

            if (!File.Exists(credentialsPath))
                throw new FileNotFoundException($"Google credentials file not found: {credentialsPath}");

            var client = new BigQueryClientBuilder
            {
                ProjectId = ProjectId,
                JsonCredentials = File.ReadAllText(credentialsPath, System.Text.Encoding.UTF8)
            }.Build();
            return client;
        }
    }
}
