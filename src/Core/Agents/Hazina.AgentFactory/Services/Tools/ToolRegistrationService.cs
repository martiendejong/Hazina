using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hazina.AgentFactory.Services.BigQuery;
using Hazina.AgentFactory.Services.Email;
using Hazina.AgentFactory.Services.Tools.Parameters;
using Hazina.LLMs;

namespace Hazina.AgentFactory.Services.Tools;

/// <summary>
/// Service responsible for registering tools with agent contexts.
/// Extracted from AgentFactory to follow Single Responsibility Principle.
/// </summary>
public class ToolRegistrationService : IToolRegistrationService
{
    private readonly List<StoreConfig> _storesConfig;
    private readonly List<AgentConfig> _agentsConfig;
    private readonly List<FlowConfig> _flowsConfig;
    private readonly IEmailService _emailService;
    private readonly IBigQueryService? _bigQueryService;
    private readonly Func<string, string, string, CancellationToken, Task<string>> _callAgentFunc;
    private readonly Func<string, string, string, CancellationToken, Task<string>> _callFlowFunc;
    private readonly Func<string, string, string, CancellationToken, Task<string>> _callCoderAgentFunc;
    private readonly Func<string, string, string, bool, CancellationToken, Task<string>> _callCustomAgentFunc;
    private readonly Func<bool> _getWriteMode;

    public ToolRegistrationService(
        List<StoreConfig> storesConfig,
        List<AgentConfig> agentsConfig,
        List<FlowConfig> flowsConfig,
        IEmailService emailService,
        IBigQueryService? bigQueryService,
        Func<string, string, string, CancellationToken, Task<string>> callAgentFunc,
        Func<string, string, string, CancellationToken, Task<string>> callFlowFunc,
        Func<string, string, string, CancellationToken, Task<string>> callCoderAgentFunc,
        Func<string, string, string, bool, CancellationToken, Task<string>> callCustomAgentFunc,
        Func<bool> getWriteMode)
    {
        _storesConfig = storesConfig;
        _agentsConfig = agentsConfig;
        _flowsConfig = flowsConfig;
        _emailService = emailService;
        _bigQueryService = bigQueryService;
        _callAgentFunc = callAgentFunc;
        _callFlowFunc = callFlowFunc;
        _callCoderAgentFunc = callCoderAgentFunc;
        _callCustomAgentFunc = callCustomAgentFunc;
        _getWriteMode = getWriteMode;
    }

    public void AddStoreTools(
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IToolsContext tools,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        string caller)
    {
        AddAgentTools(tools, agents, caller);
        AddFlowTools(tools, flows, caller);

        var i = 0;
        foreach (var storeItem in stores)
        {
            var store = storeItem.Store;
            AddReadTools(tools, store);
            if (storeItem.Write)
            {
                AddWriteTools(tools, store);
            }
            if (i == 0)
            {
                AddBuildTools(tools, functions, store);
            }
            ++i;
        }

        if (functions.Contains("custom"))
        {
            AddAdvancedAgentTools(tools, agents, caller);
        }
        if (functions.Contains("wordpress"))
        {
            AddWordpressTools(tools);
        }
        if (functions.Contains("email"))
        {
            AddEmailTools(tools);
        }
    }

    private void AddWriteTools(IToolsContext tools, IDocumentStore store)
    {
        var config = _storesConfig.FirstOrDefault(x => x.Name == store.Name) ?? new StoreConfig { Name = store.Name, Description = "" };

        var writeFile = new HazinaChatTool(
            $"{store.Name}_write",
            $"Store a file in store {store.Name}. {config.Description}",
            [CommonToolParameters.Key, CommonToolParameters.Content],
            async (messages, toolCall, cancel) =>
            {
                if (_getWriteMode()) return "Cannot give write instructions when in write mode";
                if (CommonToolParameters.Key.TryGetValue(toolCall, out string key))
                    if (CommonToolParameters.Content.TryGetValue(toolCall, out string content))
                        return await store.Store(key, content, null, false) ? "success" : "content provided was the same as the file";
                    else
                        return "No content given";
                return "No key given";
            });
        tools.Add(writeFile);

        var deleteFile = new HazinaChatTool(
            $"{store.Name}_delete",
            $"Removes a file from store {store.Name}. {config.Description}",
            [CommonToolParameters.Key],
            async (messages, toolCall, cancel) =>
            {
                if (CommonToolParameters.Key.TryGetValue(toolCall, out string key))
                    return await store.Remove(key) ? "success" : "the file was already deleted";
                return "No key given";
            });
        tools.Add(deleteFile);
    }

    private void AddReadTools(IToolsContext tools, IDocumentStore store)
    {
        var config = _storesConfig.FirstOrDefault(x => x.Name == store.Name) ?? new StoreConfig { Name = store.Name, Description = "" };

        var getFiles = new HazinaChatTool(
            $"{store.Name}_list",
            $"Retrieve a list of the files in store {store.Name}. {config.Description}",
            [CommonToolParameters.Folder],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                CommonToolParameters.FilesRecursive.TryGetValue(toolCall, out bool recursive);
                try
                {
                    if (CommonToolParameters.Folder.TryGetValue(toolCall, out string folder))
                        return string.Join("\n", await store.List(folder, recursive));
                    return string.Join("\n", await store.List("", recursive));
                }
                catch (Exception e)
                {
                    return "Error: " + e.Message;
                }
            });
        tools.Add(getFiles);

        var getRelevancy = new HazinaChatTool(
            $"{store.Name}_relevancy",
            $"Retrieve a list of relevant files in store {store.Name}. {config.Description}",
            [CommonToolParameters.Relevancy],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (CommonToolParameters.Relevancy.TryGetValue(toolCall, out string key))
                    return string.Join("\n", await store.RelevantItems(key));
                return "No key given";
            });
        tools.Add(getRelevancy);

        var getFile = new HazinaChatTool(
            $"{store.Name}_read",
            $"Retrieve a file from store {store.Name}. {config.Description}",
            [CommonToolParameters.Key],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (CommonToolParameters.Key.TryGetValue(toolCall, out string key))
                    return await store.Get(key) ?? "File not found";
                return "No key given";
            });
        tools.Add(getFile);
    }

    private void AddBuildTools(IToolsContext tools, IEnumerable<string> functions, IDocumentStore store)
    {
        if (functions.Contains("git"))
        {
            var git = new HazinaChatTool(
                "git",
                "Calls git and returns the output.",
                [CommonToolParameters.Arguments, CommonToolParameters.TimeoutSeconds],
                async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (CommonToolParameters.Arguments.TryGetValue(toolCall, out string args) &&
                        CommonToolParameters.TimeoutSeconds.TryGetValue(toolCall, out string timeout))
                    {
                        var output = await GitOutput.GetGitOutput(store.TextStore.RootFolder, args, TimeSpan.FromSeconds(int.Parse(timeout)));
                        return output.Item1 + "\n" + output.Item2;
                    }
                    return "arguments not provided";
                });
            tools.Add(git);
        }

        if (functions.Contains("build"))
        {
            var build = new HazinaChatTool(
                "build",
                "Builds the solution and returns the output.",
                [],
                async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build.bat", "build_errors.log"));
            tools.Add(build);
        }

        if (functions.Contains("build_dotnet"))
        {
            var build = new HazinaChatTool(
                "build_dotnet",
                "Builds the .NET backend solution and returns the output.",
                [],
                async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build_dotnet.bat", "build_errors.log"));
            tools.Add(build);
        }

        if (functions.Contains("build_quasar"))
        {
            var build = new HazinaChatTool(
                "build_quasar",
                "Builds the Quasar frontend project and returns the output.",
                [],
                async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build_quasar.bat", "build_errors.log"));
            tools.Add(build);
        }

        if (functions.Contains("test_quasar"))
        {
            var build = new HazinaChatTool(
                "test_quasar",
                "Tests the Quasar frontend project and returns the output.",
                [],
                async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "test_quasar.bat", "build_errors.log"));
            tools.Add(build);
        }

        if (functions.Contains("npm"))
        {
            var npm = new HazinaChatTool(
                "npm",
                "Runs the npm command.",
                [CommonToolParameters.Arguments, CommonToolParameters.TimeoutSeconds],
                async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (CommonToolParameters.Arguments.TryGetValue(toolCall, out string args) &&
                        CommonToolParameters.TimeoutSeconds.TryGetValue(toolCall, out string timeout))
                    {
                        var output = await NpmOutput.GetNpmOutputAsync(store.TextStore.RootFolder + "\\frontend", args, TimeSpan.FromSeconds(int.Parse(timeout)));
                        return output.Item1 + "\n" + output.Item2;
                    }
                    return "arguments not provided";
                });
            tools.Add(npm);
        }

        if (functions.Contains("dotnet"))
        {
            var dotnet = new HazinaChatTool(
                "dotnet",
                "Runs the dotnet command.",
                [CommonToolParameters.Arguments, CommonToolParameters.TimeoutSeconds],
                async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (CommonToolParameters.Arguments.TryGetValue(toolCall, out string args) &&
                        CommonToolParameters.TimeoutSeconds.TryGetValue(toolCall, out string timeout))
                    {
                        var output = await DotNetOutput.GetDotNetOutput(store.TextStore.RootFolder, args, TimeSpan.FromSeconds(int.Parse(timeout)));
                        return output.Item1 + "\n" + output.Item2;
                    }
                    return "arguments not provided";
                });
            tools.Add(dotnet);
        }

        if (functions.Contains("bigquery") && _bigQueryService != null)
        {
            AddBigQueryTools(tools);
        }
    }

    private void AddBigQueryTools(IToolsContext tools)
    {
        if (_bigQueryService == null) return;

        var bigQueryDatasetsTool = new HazinaChatTool(
            "bigquery_datasets",
            "Retrieves the available datasets in Google BigQuery.",
            [],
            async (messages, toolCall, cancel) =>
                await HazinaChatTool.CallTool(async () => await _bigQueryService.GetDatasetsAsync(cancel), cancel));
        tools.Add(bigQueryDatasetsTool);

        var bigQueryTablesTool = new HazinaChatTool(
            "query_tables",
            "Returns the tables in a Google BigQuery dataset.",
            [CommonToolParameters.BigQueryDataSet],
            async (messages, toolCall, cancel) =>
                await HazinaChatTool.CallTool(async () =>
                {
                    if (!CommonToolParameters.BigQueryDataSet.TryGetValue(toolCall, out string collection))
                        return "No dataset provided.";
                    return await _bigQueryService.GetTablesAsync(collection, cancel);
                }, cancel));
        tools.Add(bigQueryTablesTool);

        var bigQueryTableFieldsTool = new HazinaChatTool(
            "query_tablefields",
            "Returns the fields of the tables in a Google BigQuery dataset.",
            [CommonToolParameters.BigQueryDataSet, CommonToolParameters.BigQueryTableName],
            async (messages, toolCall, cancel) =>
                await HazinaChatTool.CallTool(async () =>
                {
                    if (!CommonToolParameters.BigQueryDataSet.TryGetValue(toolCall, out string collection))
                        return "No dataset provided.";
                    if (!CommonToolParameters.BigQueryTableName.TryGetValue(toolCall, out string table))
                        return "No table provided.";
                    return await _bigQueryService.GetTableFieldsAsync(collection, table, cancel);
                }, cancel));
        tools.Add(bigQueryTableFieldsTool);

        var bigQueryTool = new HazinaChatTool(
            "query_bigquery",
            "Runs a read-only SQL query on Google BigQuery and returns the results as a list of rows.",
            [CommonToolParameters.BigQueryArguments],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.BigQueryArguments.TryGetValue(toolCall, out string sql))
                    return "No query provided.";
                return await _bigQueryService.ExecuteQueryAsync(sql, cancel);
            });
        tools.Add(bigQueryTool);
    }

    private void AddWordpressTools(IToolsContext tools)
    {
        var wordpressAgent = new HazinaChatTool(
            "wordpress_cli",
            "Call the wordpress cli",
            [CommonToolParameters.WpCommand, CommonToolParameters.WpArguments],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.WpCommand.TryGetValue(toolCall, out string command))
                    return "No arguments given";
                CommonToolParameters.WpArguments.TryGetValue(toolCall, out string wparguments);

                // TODO: Make these configurable
                var username = "martiendejong2008@gmail.com";
                var password = "qEWK IwaU JzyL ufe9 Ecro tkKB";
                var siteurl = "http://localhost";

#pragma warning disable MA0039 // Do not write your own certificate validation method
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
#pragma warning restore MA0039
                var httpClient = new HttpClient(handler);

                try
                {
                    var url = $"{siteurl}/wp-json/wp-cli-api-bridge/v1/command";
                    var request = new HttpRequestMessage(HttpMethod.Post, url);

                    var jsonContent = JsonSerializer.Serialize(wparguments);
                    using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    request.Content = content;

                    string basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

                    using var response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });
        tools.Add(wordpressAgent);
    }

    private void AddEmailTools(IToolsContext tools)
    {
        var sendEmailTool = new HazinaChatTool(
            "email_send",
            "Sends an email",
            [CommonToolParameters.Recipient, CommonToolParameters.Subject, CommonToolParameters.Body],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.Recipient.TryGetValue(toolCall, out string to))
                    return "Recipient not specified";
                if (!CommonToolParameters.Subject.TryGetValue(toolCall, out string subject))
                    return "Subject not specified";
                if (!CommonToolParameters.Body.TryGetValue(toolCall, out string body))
                    return "Body not specified";

                try
                {
                    var result = await _emailService.SendEmailAsync(to, subject, body, cancel);
                    return result ? "Email sent successfully." : "Failed to send email.";
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(sendEmailTool);

        var listInboxTool = new HazinaChatTool(
            "email_list_inbox",
            "Lists latest emails in the inbox",
            [CommonToolParameters.EmailAmount, CommonToolParameters.EmailOldestFirst],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    if (!CommonToolParameters.EmailAmount.TryGetValue(toolCall, out string amount))
                        return "Amount not specified";
                    if (!CommonToolParameters.EmailOldestFirst.TryGetValue(toolCall, out string oldestFirst))
                        return "OldestFirst not specified";

                    var emails = await _emailService.ListInboxEmailsAsync(int.Parse(amount), bool.Parse(oldestFirst), cancel);
                    return string.Join("\n\n", emails.Select(e => $"ID:{e.Id} {e.Sender} - {e.Subject} - {e.Date}"));
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(listInboxTool);

        var readEmailTool = new HazinaChatTool(
            "email_read",
            "Reads an email by ID or index",
            [CommonToolParameters.EmailId],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.EmailId.TryGetValue(toolCall, out string id))
                    return "Email ID not specified";

                try
                {
                    var email = await _emailService.ReadEmailAsync(id, cancel);
                    return email != null ? $"From: {email.Sender}\nSubject: {email.Subject}\n\n{email.Body}" : "Email not found.";
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(readEmailTool);

        var createFolderTool = new HazinaChatTool(
            "email_create_folder",
            "Creates a folder in the mailbox",
            [CommonToolParameters.FolderName],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.FolderName.TryGetValue(toolCall, out string folderName))
                    return "Folder name not specified";

                try
                {
                    return await _emailService.CreateMailboxFolderAsync(folderName, cancel);
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(createFolderTool);

        var moveEmailTool = new HazinaChatTool(
            "email_move",
            "Moves an email to a folder",
            [CommonToolParameters.EmailId, CommonToolParameters.DestinationFolder],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.EmailId.TryGetValue(toolCall, out string emailId))
                    return "Email ID not specified";
                if (!CommonToolParameters.DestinationFolder.TryGetValue(toolCall, out string folderName))
                    return "Destination folder not specified";

                try
                {
                    return await _emailService.MoveEmailToFolderAsync(emailId, folderName, cancel);
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(moveEmailTool);

        var listFoldersTool = new HazinaChatTool(
            "email_list_folders",
            "Lists all folders in the mailbox",
            [],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    var folders = await _emailService.ListMailboxFoldersAsync(cancel);
                    return string.Join("\n", folders);
                }
                catch (Exception ex) { return ex.Message; }
            });
        tools.Add(listFoldersTool);
    }

    private void AddAdvancedAgentTools(IToolsContext tools, IEnumerable<string> agents, string caller)
    {
        var getStores = new HazinaChatTool(
            "custom_agent_getstores",
            "Gets a list of stores that a custom agent can use",
            [],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                return string.Join(",", _storesConfig.Select(s => s.Name).ToList());
            });
        tools.Add(getStores);

        var runCustomAgent = new HazinaChatTool(
            "custom_agent_run",
            "Runs a custom agent",
            [CommonToolParameters.Instruction, CommonToolParameters.SystemPrompt, CommonToolParameters.Store],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.Instruction.TryGetValue(toolCall, out string instruction))
                    return "No instruction given";
                if (!CommonToolParameters.SystemPrompt.TryGetValue(toolCall, out string systemPrompt))
                    return "No system prompt given";
                if (!CommonToolParameters.Store.TryGetValue(toolCall, out string store))
                    return "No store given";
                return await _callCustomAgentFunc(systemPrompt, store, instruction, false, cancel);
            });
        tools.Add(runCustomAgent);

        var writeCustomAgent = new HazinaChatTool(
            "custom_agent_write",
            "Runs a custom agent that writes documents",
            [CommonToolParameters.Instruction, CommonToolParameters.SystemPrompt, CommonToolParameters.Store],
            async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (!CommonToolParameters.Instruction.TryGetValue(toolCall, out string instruction))
                    return "No instruction given";
                if (!CommonToolParameters.SystemPrompt.TryGetValue(toolCall, out string systemPrompt))
                    return "No system prompt given";
                if (!CommonToolParameters.Store.TryGetValue(toolCall, out string store))
                    return "No store given";
                if (_getWriteMode())
                    return "Already in writemode";
                return await _callCustomAgentFunc(systemPrompt, store, instruction, true, cancel);
            });
        tools.Add(writeCustomAgent);
    }

    private void AddAgentTools(IToolsContext tools, IEnumerable<string> agents, string caller)
    {
        foreach (var agent in agents)
        {
            var config = _agentsConfig.FirstOrDefault(x => x.Name == agent);
            if (config == null)
                throw new Exception($"Agent {agent} is not defined. Request by {caller}.");

            if (config.ExplicitModify)
            {
                var callCoderAgent = new HazinaChatTool(
                    agent,
                    $"Calls {agent} to modify the codebase. {config.Description} Be aware of the token limit of 8000 so only let the agents make small modifications at a time.",
                    [CommonToolParameters.Instruction],
                    async (messages, toolCall, cancel) =>
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (_getWriteMode()) return "Cannot give write instructions when in write mode";
                        if (CommonToolParameters.Instruction.TryGetValue(toolCall, out string key))
                        {
                            return await _callCoderAgentFunc(agent, key, caller, cancel);
                        }
                        return "No key given";
                    });
                tools.Add(callCoderAgent);
            }
            else
            {
                var callAgent = new HazinaChatTool(
                    agent,
                    $"Calls {agent} to execute a tasks and return a message. {config.Description}",
                    [CommonToolParameters.Instruction],
                    async (messages, toolCall, cancel) =>
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (CommonToolParameters.Instruction.TryGetValue(toolCall, out string key))
                            return await _callAgentFunc(agent, key, caller, cancel);
                        return "No key given";
                    });
                tools.Add(callAgent);
            }
        }
    }

    private void AddFlowTools(IToolsContext tools, IEnumerable<string> flows, string caller)
    {
        foreach (var flow in flows)
        {
            var config = _flowsConfig.FirstOrDefault(x => x.Name == flow);
            if (config == null)
                throw new Exception($"Flow {flow} not found");

            var callFlow = new HazinaChatTool(
                flow,
                $"Calls {flow} agent workflow to execute a tasks and return a message. {config.Description}",
                [CommonToolParameters.Instruction],
                async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (CommonToolParameters.Instruction.TryGetValue(toolCall, out string key))
                    {
                        var id = Guid.NewGuid().ToString();
                        tools.SendMessage(id, flow, key);
                        var response = await _callFlowFunc(flow, key, caller, cancel);
                        tools.SendMessage(id, flow, response);
                        return response;
                    }
                    return "No key given";
                });
            tools.Add(callFlow);
        }
    }
}
