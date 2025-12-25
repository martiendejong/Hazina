using Hazina.Tools.Models.BigQuery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Cloud.BigQuery.V2;
using Hazina.Tools.Data;
using Hazina.Tools.Services.Store;

namespace Hazina.Tools.Services.BigQuery
{
    /// <summary>
    /// BigQuery operations: list/import accounts and persist results under project folder.
    /// Refactored to remove hardcoded project IDs and LLM dependencies.
    /// </summary>
    public class BigQueryService
    {
        public string ProjectId { get; set; }
        public string DatasetId { get; set; }
        public ProjectsRepository Projects { get; set; }
        private ProjectFileLocator _fileLocator => Projects != null ? new ProjectFileLocator(Projects.ProjectsFolder) : null;

        public string _apiKey { get; set; }
        private string _credentialsPath;

        public BigQueryService(string apiKey = null, string datasetId = null, string credentialsPath = null)
        {
            _apiKey = apiKey;
            DatasetId = datasetId ?? Environment.GetEnvironmentVariable("BIGQUERY_DATASET") ?? "your-dataset";
            _credentialsPath = credentialsPath ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        }

        /// <summary>
        /// Performs BigQuery operations using AI to interpret and execute queries.
        /// </summary>
        /// <param name="prompt">The instruction for the BigQuery agent</param>
        /// <param name="projectId">The project ID to work with</param>
        /// <returns>The result of the BigQuery operation</returns>
        public async Task<string> PerformBigQueryResultsAsync(string prompt, string projectId)
        {
            // Get the bigquery accounts for this project
            var accounts = ReadImportedAccountsFile(projectId);
            var bigQueryContext = new BigQueryContext();
            bigQueryContext.Accounts = accounts;
            bigQueryContext.Projects = Projects;
            bigQueryContext.ApiKey = _apiKey;
            bigQueryContext.ProjectId = projectId;

            if (string.IsNullOrWhiteSpace(prompt))
                return "No instruction provided for big query.";

            var messages = new List<HazinaChatMessage>();
            messages.Add(new HazinaChatMessage(HazinaMessageRole.System, "Your role is to execute google big query queries until you have enough data to return the provided results. Run multiple queries, one for each relevant table. never put the account id in the where clause. this is done automatically. When retrieving posts, advertisements or other content that has textual information, always make sure that you include these texts in your response. so when you talk about a facebook post, you will always include the content of the post itself. when talking about advertisements show the content of the advertisement. when its a picture write that this post is a picture. when the user asks about posts or content, never assume on type of contain but look into all of the available data. when the user talks about a table you look into that table instead of showing the table name or the columns with their data types, that is your own private information. your response will always be a representation of the data that is in the tables. only use the actual information, never show example data or hallucinations."));
            messages.Add(new HazinaChatMessage(HazinaMessageRole.System, BigQueries.BigQueryPrompt));

            var folder = _fileLocator.GetProjectFolder(projectId);
            var bigQueryStoreSetup = StoreProvider.GetStoreSetup(folder, _apiKey);

            var g = new DocumentGenerator(bigQueryStoreSetup.Store, messages, bigQueryStoreSetup.LLMClient, []);

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            // TODO: BigQueryContext no longer implements IToolsContext - requires migration
            var response = await g.GetResponse(prompt, token, [], true, true, null);

            return response?.Result ?? string.Empty;
        }

        public async Task<List<ConnectedBigQueryAccount>> FetchAccounts()
        {
            if (string.IsNullOrWhiteSpace(ProjectId))
                throw new InvalidOperationException("ProjectId must be set before calling FetchAccounts().");

            var accounts = new List<ConnectedBigQueryAccount>();
            var client = GetClient();
            var results = await client.ExecuteQueryAsync($"SELECT * FROM `{ProjectId}.{DatasetId}.unique_accounts` where not(account_name is null)", Array.Empty<BigQueryParameter>());

            ConnectedBigQueryAccount account = null;
            foreach (var row in results)
            {
                if (account == null || account.Id != row["account_id"].ToString())
                {
                    account = new ConnectedBigQueryAccount()
                    {
                        Id = row["account_id"].ToString(),
                        Name = row["account_name"].ToString(),
                        Tables = new List<ConnectedBigQueryTable>()
                    };
                    accounts.Add(account);
                }
                var table = new ConnectedBigQueryTable()
                {
                    Name = row["tabel_naam"].ToString(),
                    NumRecords = int.TryParse(row["num_records"].ToString(), out var n) ? n : 0
                };
                account.Tables.Add(table);
            }
            return accounts;
        }

        public string GetBigQueryFolder(string projectId)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectId);
            var socialMediaFolder = Path.Combine(projectFolder, "socialmedia");
            var bigQueryFolder = Path.Combine(socialMediaFolder, "bigquery");
            Directory.CreateDirectory(bigQueryFolder);
            return bigQueryFolder;
        }

        public List<ConnectedBigQueryAccount> ReadImportedAccountsFile(string projectid)
        {
            var bigQueryFolder = GetBigQueryFolder(projectid);
            var accountsFile = Path.Combine(bigQueryFolder, "accounts");
            if (!File.Exists(accountsFile)) return new List<ConnectedBigQueryAccount>();
            var json = File.ReadAllText(accountsFile);
            return JsonSerializer.Deserialize<List<ConnectedBigQueryAccount>>(json) ?? new List<ConnectedBigQueryAccount>();
        }

        public void WriteImportedAccountsFile(string projectid, List<ConnectedBigQueryAccount> accounts)
        {
            var bigQueryFolder = GetBigQueryFolder(projectid);
            var accountsFile = Path.Combine(bigQueryFolder, "accounts");
            var json = JsonSerializer.Serialize(accounts);
            File.WriteAllText(accountsFile, json);
        }

        public void RemoveAccount(string projectid, string accountid)
        {
            var bigQueryFolder = GetBigQueryFolder(projectid);
            var accountFolder = Path.Combine(bigQueryFolder, accountid);
            if (Directory.Exists(accountFolder))
                Directory.Delete(accountFolder, true);

            var accounts = ReadImportedAccountsFile(projectid);
            var acc = accounts.FirstOrDefault(a => a.Id == accountid);
            if (acc != null)
            {
                accounts.Remove(acc);
                WriteImportedAccountsFile(projectid, accounts);
            }
        }

        public async Task ImportAccount(string projectid, string accountid)
        {
            var bigQueryAccounts = await FetchAccounts();
            var acc = bigQueryAccounts.Find(a => a.Id == accountid);
            if (acc == null) return;

            var bigQueryFolder = GetBigQueryFolder(projectid);
            var accountFolder = Path.Combine(bigQueryFolder, acc.Id);
            Directory.CreateDirectory(accountFolder);

            var accounts = ReadImportedAccountsFile(projectid);
            var existingAcc = accounts.FirstOrDefault(a => a.Id == acc.Id);
            if (existingAcc != null) accounts.Remove(existingAcc);
            accounts.Add(acc);
            WriteImportedAccountsFile(projectid, accounts);

            foreach (var table in acc.Tables)
            {
                try
                {
                    var client = GetClient();
                    var query = $"SELECT * FROM `{ProjectId}.{DatasetId}.{table.Name}` WHERE account_id = @account_id";
                    var parameters = new[] { new BigQueryParameter("account_id", BigQueryDbType.String, acc.Id) };
                    var results = await client.ExecuteQueryAsync(query, parameters);

                    table.ColumnNames = results.Schema.Fields.Select(f => f.Name).ToList();
                    table.RecordsAsCsv = new List<string>();
                    foreach (var row in results)
                    {
                        var rowData = results.Schema.Fields.Select(f => row[f.Name]?.ToString() ?? string.Empty);
                        var rowCsv = string.Join(",", rowData);
                        table.RecordsAsCsv.Add(rowCsv);
                    }

                    var itemsPerStep = 25;
                    var step = 1;
                    for (var i = 0; i < table.RecordsAsCsv.Count; i += itemsPerStep)
                    {
                        var fileContent = string.Join(",", table.ColumnNames) + "\n" + string.Join("\n", table.RecordsAsCsv.Skip(i).Take(itemsPerStep).ToList());
                        var rowsFile = Path.Combine(accountFolder, table.Name + ".rows." + step);
                        File.WriteAllText(rowsFile, fileContent);
                        ++step;
                    }
                }
                catch
                {
                    // keep going on table errors
                }
            }
        }

        private BigQueryClient GetClient()
        {
            if (string.IsNullOrWhiteSpace(ProjectId))
                throw new InvalidOperationException("ProjectId must be set before creating the BigQuery client.");

            var credentialsPath = _credentialsPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "googleaccount.json");

            if (!File.Exists(credentialsPath))
                throw new FileNotFoundException($"Google credentials file not found: {credentialsPath}. Set GOOGLE_APPLICATION_CREDENTIALS environment variable or provide path in constructor.");

            var client = new BigQueryClientBuilder
            {
                ProjectId = ProjectId,
                JsonCredentials = File.ReadAllText(credentialsPath, System.Text.Encoding.UTF8)
            }.Build();
            return client;
        }
    }
}
