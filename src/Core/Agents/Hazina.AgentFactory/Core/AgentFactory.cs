using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Google.Apis.Auth.OAuth2.Responses;
using Google.Cloud.BigQuery.V2;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;

using MathNet.Numerics.RootFinding;

using MimeKit;

using OpenAI.Images;

using UniqueId = MailKit.UniqueId;

public class AgentFactory {
    public AgentFactory(string openAIApiKey, string logFilePath, string googleProjectId = "")
    {
        OpenAiApiKey = openAIApiKey;
        GoogleProjectId = googleProjectId;
        LogFilePath = logFilePath;

        // Initialize config lists
        storesConfig = new List<StoreConfig>();
        agentsConfig = new List<AgentConfig>();
        flowsConfig = new List<FlowConfig>();

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var credentialsPath = Path.Combine(basePath, "googleaccount.json");

        if (!File.Exists(credentialsPath))
            return;

        try
        {
            googleAccountJson = File.ReadAllText(credentialsPath);
            using var doc = JsonDocument.Parse(googleAccountJson);
            googleAccountProjectId = doc.RootElement.GetProperty("project_id").GetString();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not load google account fle");
            Console.WriteLine(ex.Message);
        }
    }

    public List<StoreConfig> storesConfig;
    public List<AgentConfig> agentsConfig;
    public List<FlowConfig> flowsConfig;

    public bool WriteMode = false;
    public List<HazinaChatMessage> Messages; // Set on init by AgentManager.
    public string OpenAiApiKey;
    public string GoogleProjectId;
    public string LogFilePath;

    public ChatToolParameter keyParameter = new ChatToolParameter { Name = "key", Description = "The key/path of the file.", Type = "string", Required = true };
    public ChatToolParameter contentParameter = new ChatToolParameter { Name = "content", Description = "The content of the file.", Type = "string", Required = true };
    public ChatToolParameter folderParameter = new ChatToolParameter { Name = "path", Description = "The partial path, the folder to query.", Type = "string", Required = false };
    public ChatToolParameter filesRecursiveParameter = new ChatToolParameter { Name = "recursive", Description = "Returns the files in subfolders as well.", Type = "string", Required = false };
    public ChatToolParameter relevancyParameter = new ChatToolParameter { Name = "query", Description = "The relevancy search query.", Type = "string", Required = true };
    public ChatToolParameter instructionParameter = new ChatToolParameter { Name = "instruction", Description = "The instruction to send to the agent.", Type = "string", Required = true };
    public ChatToolParameter systemPromptParameter = new ChatToolParameter { Name = "system_prompt", Description = "The system prompt for the agent.", Type = "string", Required = true };
    public ChatToolParameter argumentsParameter = new ChatToolParameter { Name = "arguments", Description = "The arguments to call git with.", Type = "string", Required = true };
    public ChatToolParameter wpcommandParameter = new ChatToolParameter { Name = "command", Description = "The wp cli command to call.", Type = "string", Required = true };
    public ChatToolParameter wpargumentsParameter = new ChatToolParameter { Name = "arguments", Description = "The arguments to call the wordpress cli command with.", Type = "string", Required = false };
    public ChatToolParameter timeOutSecondsParameter = new ChatToolParameter { Name = "timeout", Description = "The maximum number of seconds this process is allowed to run.", Type = "number", Required = true };
    public ChatToolParameter bigQueryParameter = new ChatToolParameter { Name = "arguments", Description = "The arguments to call Google BigQuery with.", Type = "string", Required = true };
    public ChatToolParameter bigQueryDataSetParameter = new ChatToolParameter { Name = "datacollection", Description = "The dataset in Google BigQuery.", Type = "string", Required = true };
    public ChatToolParameter bigQueryTableNameParameter = new ChatToolParameter { Name = "tablename", Description = "The table name in Google BigQuery.", Type = "string", Required = true };
    public ChatToolParameter storeParameter = new ChatToolParameter { Name = "store", Description = "The store that the agent has access to.", Type = "string", Required = true };

    // email
    public ChatToolParameter recipientParameter = new ChatToolParameter { Name = "recipient", Description = "The email address that receives the email.", Type = "string", Required = true };
    public ChatToolParameter subjectParameter = new ChatToolParameter { Name = "subject", Description = "The subject of the email.", Type = "string", Required = true };
    public ChatToolParameter bodyParameter = new ChatToolParameter { Name = "body", Description = "The body content of the email.", Type = "string", Required = true };
    public ChatToolParameter emailIdParameter = new ChatToolParameter { Name = "emailId", Description = "The ID or index of the email.", Type = "string", Required = true };
    public ChatToolParameter folderNameParameter = new ChatToolParameter { Name = "folder", Description = "The email folder.", Type = "string", Required = true };
    public ChatToolParameter destinationFolderParameter = new ChatToolParameter { Name = "destinationFolder", Description = "The destination folder for the email.", Type = "string", Required = true };
    public ChatToolParameter emailAmountParameter = new ChatToolParameter { Name = "amount", Description = "The number of emails to read.", Type = "number", Required = true };
    public ChatToolParameter emailOldestFirstParameter = new ChatToolParameter { Name = "oldestFirst", Description = "If we should return the oldest emails first.", Type = "boolean", Required = true };

    public Dictionary<string, HazinaAgent> Agents = new Dictionary<string, HazinaAgent>();
    public Dictionary<string, HazinaFlow> Flows = new Dictionary<string, HazinaFlow>();

    public string googleAccountJson { get; set; }
    public string googleAccountProjectId { get; set; }

    // Updated: messages are stored with full meta-info and updated in-place with Response upon agent reply
    public async Task<string> CallAgent(string name, string query, string caller, CancellationToken cancel)
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.GetResponse(query + (WriteMode ? writeModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        // Find the message by MessageId and update Response
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
        if(storedMsg != null) storedMsg.Response = response;
        // Log reply as new message entry for history
        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = response
        };
        Messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);
        return response;
    }

    // CallFlow updated to store/track agent/flow meta-data in chat history per message
    public async Task<string> CallFlow(string name, string query, string caller, CancellationToken cancel)
    {
        var flow = Flows[name];
        foreach (var agent in flow.CallsAgents)
        {
            if (Agents[agent].IsCoder && !WriteMode)
            {
                WriteMode = true;
                query = await CallCoderAgent(agent, query, caller, cancel, flow.Name);
                WriteMode = false;
            }
            else
            {
                query = await CallAgentWithMeta(agent, query, caller, string.Empty, flow.Name, cancel);
            }
        }
        return query;
    }

    const string writeModeText = "\nYou are now in write mode. You cannot call any other {agent}_write tools or write file tools in this mode. The file modifications need to be included in your response.";

    // Extended CallCoderAgent to accept flowName and store correct message meta-info
    public async Task<string> CallCoderAgent(string name, string query, string caller, CancellationToken cancel, string flowName = "")
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = "CodeModify",
            FlowName = flowName,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.UpdateStore(query + writeModeText + "\nALL YOUR MODIFICATIONS MUST ALWAYS SUPPLY THE WHOLE FILE. NEVER leave antyhing out and NEVER replace it with something like /* the rest of the code goes here */ or /* the rest of the code stays the same */", cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
        if(storedMsg != null) storedMsg.Response = response;
        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = "CodeModify",
            FlowName = flowName,
            Response = response
        };
        Messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);
        return response;
    }

    private async Task<string> CallCustomAgent(string systemPrompt, string store, string instruction, string caller, bool isCoder, CancellationToken cancel)
    {
        var storeConfig = storesConfig.FirstOrDefault(s => s.Name.ToLower() == store.ToLower());
        if (storeConfig == null) return "Store not found";

        var openAIConfig = new OpenAIConfig(OpenAiApiKey);
        var llmClient = new OpenAIClientWrapper(openAIConfig);
        var creator = new QuickAgentCreator(this, llmClient);
        var loader = new StoresAndAgentsAndFlowLoader(creator);

        var dstore = creator.CreateStore(new StorePaths(storeConfig.Path), storeConfig.Name) as IDocumentStore;
        await loader.AddFiles(dstore, storeConfig.Path, storeConfig.FileFilters, storeConfig.SubDirectory, storeConfig.ExcludePattern);
        
        var agent = await CreateUnregisteredAgent("unregistered", systemPrompt, [( dstore, true )], ["custom"], [], [], isCoder);

        instruction = "de functies custom_agent_getstores, custom_agent_run en custom_agent_write kunnen gebruikt worden om de stores in te zien, custom agents aan te roepen voor een response, of om wijzigingen te maken. " + instruction;

        string response;
        if (isCoder)
        {
            WriteMode = true;
            try
            {
                var responseObj = await agent.Generator.UpdateStore(instruction + writeModeText + "\nALL YOUR MODIFICATIONS MUST ALWAYS SUPPLY THE WHOLE FILE. NEVER leave antyhing out and NEVER replace it with something like /* the rest of the code goes here */ or /* the rest of the code stays the same */", cancel, null, true, true, agent.Tools, null);
                response = responseObj.Result;
            }
            finally {
                WriteMode = false;
            }
        }
        else
        {
            var responseObj = await agent.Generator.GetResponse(instruction, cancel, Messages);
            response = responseObj.Result;
        }
        return response;
    }

    // Helper for agent call with custom meta-info (used in flows)
    public async Task<string> CallAgentWithMeta(string name, string query, string caller, string functionName, string flowName, CancellationToken cancel)
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = functionName ?? string.Empty,
            FlowName = flowName ?? string.Empty,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.GetResponse(query + (WriteMode ? writeModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
        if(storedMsg != null) storedMsg.Response = response;
        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = functionName ?? string.Empty,
            FlowName = flowName ?? string.Empty,
            Response = response
        };
        Messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);
        return response;
    }

    public async Task<HazinaAgent> CreateUnregisteredAgent(string name, string systemPrompt, IEnumerable<(IDocumentStore Store, bool Write)> stores, IEnumerable<string> function, IEnumerable<string> agents, IEnumerable<string> flows, bool isCoder = false, string model = "")
    {
        var config = new OpenAIConfig(OpenAiApiKey);
        if(!string.IsNullOrWhiteSpace(model))
            config.Model = model;
        var llmClient = new OpenAIClientWrapper(config);
        var tools = new ToolsContext();
        tools.SendMessage = (string id, string agent, string output) =>
        {
            // Intentionally left as no-op; UI may override.
        };
        AddStoreTools(stores, tools, function, agents, flows, name);
        var tempStores = stores.Skip(1).Select(s => s.Store as IDocumentStore).ToList();
        var generator = new DocumentGenerator(stores.First().Store, new List<HazinaChatMessage>() { new HazinaChatMessage { Role = HazinaMessageRole.System, Text = systemPrompt } }, llmClient, OpenAiApiKey, LogFilePath, tempStores);
        var agent = new HazinaAgent(name, generator, tools, isCoder);
        return agent;
    }

    public async Task<HazinaAgent> CreateAgent(string name, string systemPrompt, IEnumerable<(IDocumentStore Store, bool Write)> stores, IEnumerable<string> function, IEnumerable<string> agents, IEnumerable<string> flows, bool isCoder = false)
    {       
        var agent = await CreateUnregisteredAgent(name, systemPrompt, stores, function, agents, flows, isCoder);
        Agents[name] = agent;
        return agent;
    }

    public HazinaFlow CreateFlow(string name, List<string> callsAgents)
    {
        var flow = new HazinaFlow(name, callsAgents);
        Flows[name] = flow;
        return flow;
    }

    private void AddStoreTools(IEnumerable<(IDocumentStore Store, bool Write)> stores, IToolsContext tools, IEnumerable<string> functions, IEnumerable<string> agents, IEnumerable<string> flows, string caller)
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
        if(functions.Contains("custom"))
        {
            AddAdvancedAgentTools(tools, agents, caller);
        }
        if (functions.Contains("wordpress"))
        {
            AddWordpressTools(tools, agents, caller);
        }
        if (functions.Contains("email"))
        {
            AddEmailFunctions(tools);
        }
    }

    private void AddWriteTools(IToolsContext tools, IDocumentStore store)
    {
        var config = storesConfig.FirstOrDefault(x => x.Name == store.Name) ?? new StoreConfig { Name = store.Name, Description = "" };
        var writeFile = new HazinaChatTool($"{store.Name}_write", $"Store a file in store {store.Name}. {config.Description}", [keyParameter, contentParameter], async (messages, toolCall, cancel) =>
        {
            if (WriteMode) return "Cannot give write instructions when in write mode";
            if (keyParameter.TryGetValue(toolCall, out string key))
                if (contentParameter.TryGetValue(toolCall, out string content))
                    return await store.Store(key, content, null, false) ? "success" : "content provided was the same as the file";
                else
                    return "No content given";
            return "No key given";
        });
        tools.Add(writeFile);
        var deleteFile = new HazinaChatTool($"{store.Name}_delete", $"Removes a file from store {store.Name}. {config.Description}", [keyParameter], async (messages, toolCall, cancel) =>
        {
            if (keyParameter.TryGetValue(toolCall, out string key))
                return await store.Remove(key) ? "success" : "the file was already deleted"; ;
            return "No key given";
        });
        tools.Add(deleteFile);
    }

    private void AddBuildTools(IToolsContext tools, IEnumerable<string> functions, IDocumentStore store)
    {
        if (functions.Contains("git"))
        {
            var git = new HazinaChatTool($"git", $"Calls git and returns the output.", [argumentsParameter, timeOutSecondsParameter], async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();

                if (argumentsParameter.TryGetValue(toolCall, out string args) && timeOutSecondsParameter.TryGetValue(toolCall, out string timeout))
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
            var build = new HazinaChatTool($"build", $"Builds the solution and returns the output.", [], async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build.bat", "build_errors.log"));
            tools.Add(build);
        }
        if (functions.Contains("build_dotnet"))
        {
            var build = new HazinaChatTool($"build_dotnet", $"Builds the .NET backend solution and returns the output.", [], async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build_dotnet.bat", "build_errors.log"));
            tools.Add(build);
        }
        if (functions.Contains("build_quasar"))
        {
            var build = new HazinaChatTool($"build_quasar", $"Builds the Quasar frontend project and returns the output.", [], async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "build_quasar.bat", "build_errors.log"));
            tools.Add(build);
        }
        if (functions.Contains("test_quasar"))
        {
            var build = new HazinaChatTool($"test_quasar", $"Tests the Quasar frontend project and returns the output.", [], async (messages, toolCall, cancel) => BuildOutput.GetBuildOutput(store.TextStore.RootFolder, "test_quasar.bat", "build_errors.log"));
            tools.Add(build);
        }
        if (functions.Contains("npm"))
        {
            var npm = new HazinaChatTool($"npm", $"Runs the npm command.", [argumentsParameter, timeOutSecondsParameter], async (messages, toolCall, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (argumentsParameter.TryGetValue(toolCall, out string args) && timeOutSecondsParameter.TryGetValue(toolCall, out string timeout))
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
            var git = new HazinaChatTool($"dotnet", $"Runs the dotnet command.", [argumentsParameter, timeOutSecondsParameter], async (messages, toolCall, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (argumentsParameter.TryGetValue(toolCall, out string args) && timeOutSecondsParameter.TryGetValue(toolCall, out string timeout))
                {
                    var output = await DotNetOutput.GetDotNetOutput(store.TextStore.RootFolder, args, TimeSpan.FromSeconds(int.Parse(timeout)));
                    return output.Item1 + "\n" + output.Item2;
                }
                return "arguments not provided";
            });
            tools.Add(git);
        }
        // TODO: Re-implement ClaudeCliTool if needed
        // if (functions.Contains("claude_cli") || functions.Contains("llm_claude_cli"))
        // {
        //     // Adds a tool that calls local Anthropic Claude CLI
        //     tools.Add(ClaudeCliTool.Create());
        // }
        if (functions.Contains("bigquery"))
        {
            //var bigQueryProject = "social-media-hulp";
            var bigQueryProject = googleAccountProjectId;// "wide-lattice-389014";

            //string schema = "wide-lattice-389014.marketing_data";
            //string newSchema = "social-media-hulp.{collection}";


            var bigQueryCollectionsTool = new HazinaChatTool(
                "bigquery_datasets",
                "Retrieves the available datasets in Google BigQuery.",
                [],
                async (messages, toolCall, cancel) => await HazinaChatTool.CallTool(async () =>
                {
                    BigQueryClient client = BigQuery_GetClient();

                    var sql = "SELECT schema_name FROM `region-eu`.INFORMATION_SCHEMA.SCHEMATA;";
                    var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                    var sql2 = "SELECT schema_name FROM `region-us`.INFORMATION_SCHEMA.SCHEMATA;";
                    var result2 = await client.ExecuteQueryAsync(sql2, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                    var r = result.Union(result2).ToList();

                    return BigQuery_ExtractAsString(r);
                }, cancel)                
            );            
            tools.Add(bigQueryCollectionsTool);
            var bigQueryTablesTool = new HazinaChatTool(
                "query_tables",
                "Returns the tables in a Google BigQuery dataset.",
                [bigQueryDataSetParameter],
                async (messages, toolCall, cancel) => await HazinaChatTool.CallTool(async () =>
                {
                    if (!bigQueryDataSetParameter.TryGetValue(toolCall, out string collection))
                        return "No dataset provided.";

                    BigQueryClient client = BigQuery_GetClient();
                    var sql = $"SELECT table_name FROM `{bigQueryProject}.{collection}.INFORMATION_SCHEMA.TABLES`;";

                    var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                    return BigQuery_ExtractAsString(result);
                }, cancel)                
            );
            tools.Add(bigQueryTablesTool);
            var bigQueryTableFieldsTool = new HazinaChatTool(
                "query_tablefields",
                "Returns the fields of the tables in a Google BigQuery dataset.",
                [bigQueryDataSetParameter, bigQueryTableNameParameter],
                async (messages, toolCall, cancel) => await HazinaChatTool.CallTool(async () =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (!bigQueryDataSetParameter.TryGetValue(toolCall, out string collection))
                        return "No dataset provided.";
                    if (!bigQueryTableNameParameter.TryGetValue(toolCall, out string table))
                        return "No table provided.";

                    BigQueryClient client = BigQuery_GetClient();
                    var sql = $"SELECT table_name, STRING_AGG(CONCAT('- ', column_name, ' (', data_type, ')'), '\\n') AS fields_list FROM `{bigQueryProject}.{collection}.INFORMATION_SCHEMA.COLUMNS` WHERE table_name='{table}' GROUP BY table_name;";

                    var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                    return BigQuery_ExtractAsString(result);
                }, cancel)
            );
            tools.Add(bigQueryTableFieldsTool);


            //        public string GetTablesQuery = @"
            //SELECT 
            //  table_name,
            //  STRING_AGG(CONCAT('- ', column_name, ' (', data_type, ')'), '\n') AS fields_list
            //FROM 
            //  `wide-lattice-389014.marketing_data.INFORMATION_SCHEMA.COLUMNS`
            //GROUP BY 
            //  table_name
            //ORDER BY 
            //  table_name;";

            //    public string GetColumnsQuery = @"
            //SELECT DISTINCT table_name
            //FROM `wide-lattice-389014.marketing_data.INFORMATION_SCHEMA.COLUMNS`
            //WHERE table_name='{table}'
            //ORDER BY table_name;";


            var bigQueryTool = new HazinaChatTool(
                "query_bigquery",
                "Runs a read-only SQL query on Google BigQuery and returns the results as a list of rows.",
                [bigQueryParameter],
                async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (!bigQueryParameter.TryGetValue(toolCall, out string sql))
                        return "No query provided.";

                    try
                    {
                        BigQueryClient client = BigQuery_GetClient();

                        var result = await client.ExecuteQueryAsync(sql, parameters: null, null, new GetQueryResultsOptions { PageSize = 10000 });

                        return BigQuery_ExtractAsString(result);
                    }
                    catch (Exception ex)
                    {
                        return $"BigQuery error: {ex.Message}";
                    }
                }
            );
            tools.Add(bigQueryTool);
        }
    }

    private static string BigQuery_ExtractAsString(IEnumerable<BigQueryRow> result)
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

    private BigQueryClient BigQuery_GetClient()
    {
        var client = new BigQueryClientBuilder
        {
            ProjectId = googleAccountProjectId,
            JsonCredentials = googleAccountJson
        }.Build();

        return client;
    }

    private void AddReadTools(IToolsContext tools, IDocumentStore store)
    {
        var config = storesConfig.FirstOrDefault(x => x.Name == store.Name) ?? new StoreConfig { Name = store.Name, Description = "" };
        var getFiles = new HazinaChatTool($"{store.Name}_list", $"Retrieve a list of the files in store {store.Name}. {config.Description}", [folderParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            filesRecursiveParameter.TryGetValue(toolCall, out bool recursive);
            try
            {
                if (folderParameter.TryGetValue(toolCall, out string folder))
                    return string.Join("\n", await store.List(folder, recursive));
                return string.Join("\n", await store.List("", recursive));
            }
            catch(Exception e)
            {
                return "Error: " + e.Message;
            }
        });
        tools.Add(getFiles);
        var getRelevancy = new HazinaChatTool($"{store.Name}_relevancy", $"Retrieve a list of relevant files in store {store.Name}. {config.Description}", [relevancyParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            if (relevancyParameter.TryGetValue(toolCall, out string key))
                return string.Join("\n", await store.RelevantItems(key));
            return "No key given";
        });
        tools.Add(getRelevancy);
        HazinaChatTool getFile = new HazinaChatTool($"{store.Name}_read", $"Retrieve a file from store {store.Name}. {config.Description}", [keyParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            if (keyParameter.TryGetValue(toolCall, out string key))
                return await store.Get(key) ?? "File not found";
            return "No key given";
        });
        tools.Add(getFile);
    }

    private void AddWordpressTools(IToolsContext tools, IEnumerable<string> agents, string caller)
    {
        var wordpressAgent = new HazinaChatTool($"wordpress_cli", $"Call the wordpress cli", [wpcommandParameter, wpargumentsParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            if (!wpcommandParameter.TryGetValue(toolCall, out string command))
                return "No arguments given";
            wpargumentsParameter.TryGetValue(toolCall, out string wparguments);

            // todo call wordpress cli
            var username = "martiendejong2008@gmail.com";
            var password = "qEWK IwaU JzyL ufe9 Ecro tkKB";
            var siteurl = "http://localhost";
            var method = HttpMethod.Post;

            //var httpClient = new HttpClient();
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var httpClient = new HttpClient(handler);


            try
            {
                var url = $"{siteurl}/wp-json/wp-cli-api-bridge/v1/command";

                var request = new HttpRequestMessage(method, url);

                var jsonContent = JsonSerializer.Serialize(wparguments);
                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                if (content != null)
                    request.Content = content;

                string basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);


                using var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();
                return data;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        });
        tools.Add(wordpressAgent);
    }

    private void AddEmailFunctions(IToolsContext tools)
    {
        var sendEmailTool = new HazinaChatTool("email_send", "Sends an email", [recipientParameter, subjectParameter, bodyParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            if (!recipientParameter.TryGetValue(toolCall, out string to))
                return "Recipient not specified";
            if (!subjectParameter.TryGetValue(toolCall, out string subject))
                return "Subject not specified";
            if (!bodyParameter.TryGetValue(toolCall, out string body))
                return "Body not specified";

            try {
                var result = await SendEmailAsync(to, subject, body, cancel);
                return result ? "Email sent successfully." : "Failed to send email.";
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(sendEmailTool);

        var listInboxTool = new HazinaChatTool("email_list_inbox", "Lists latest emails in the inbox", [emailAmountParameter, emailOldestFirstParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                if (!emailAmountParameter.TryGetValue(toolCall, out string amount))
                    return "Recipient not specified";
                if (!emailOldestFirstParameter.TryGetValue(toolCall, out string oldestFirst))
                    return "Subject not specified";

                var emails = await ListInboxEmailsAsync(int.Parse(amount), bool.Parse(oldestFirst), cancel);
                return string.Join("\n\n", emails.Select(e => $"ID:{e.Id} {e.Sender} - {e.Subject} - {e.Date}"));
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(listInboxTool);

        var readEmailTool = new HazinaChatTool("email_read", "Reads an email by ID or index", [emailIdParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            if (!emailIdParameter.TryGetValue(toolCall, out string id))
                return "Email ID not specified";

            try
            { 
                var email = await ReadEmailAsync(id, cancel);
                return email != null ? $"From: {email.Sender}\nSubject: {email.Subject}\n\n{email.Body}" : "Email not found.";
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(readEmailTool);

        var createFolderTool = new HazinaChatTool("email_create_folder", "Creates a folder in the mailbox", [folderNameParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            if (!folderNameParameter.TryGetValue(toolCall, out string folderName))
                return "Folder name not specified";

            try
            { 
                var result = await CreateMailboxFolderAsync(folderName, cancel);
                return result;
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(createFolderTool);

        var moveEmailTool = new HazinaChatTool("email_move", "Moves an email to a folder", [emailIdParameter, destinationFolderParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            if (!emailIdParameter.TryGetValue(toolCall, out string emailId))
                return "Email ID not specified";
            if (!destinationFolderParameter.TryGetValue(toolCall, out string folderName))
                return "Destination folder not specified";

            try
            {
                var result = await MoveEmailToFolderAsync(emailId, folderName, cancel);
                return result;
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(moveEmailTool);

        var listFoldersTool = new HazinaChatTool("email_list_folders", "Lists all folders in the mailbox", [], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                var folders = await ListMailboxFoldersAsync(cancel);
                return string.Join("\n", folders);
            }
            catch (Exception ex) { return ex.Message; }
        });
        tools.Add(listFoldersTool);
    }


    private void AddAdvancedAgentTools(IToolsContext tools, IEnumerable<string> agents, string caller)
    {
        var callAgent = new HazinaChatTool($"custom_agent_getstores", $"Gets a list of stores that a custom agent can use", [], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            return string.Join(",", storesConfig.Select(s => s.Name).ToList());
        });
        tools.Add(callAgent);

        var callAgent2 = new HazinaChatTool($"custom_agent_run", $"Runs a custom agent", [instructionParameter, systemPromptParameter, storeParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            if (!instructionParameter.TryGetValue(toolCall, out string instruction))
                return "No instruction given";
            if (!systemPromptParameter.TryGetValue(toolCall, out string systemPrompt))
                return "No system prompt given";
            if (!storeParameter.TryGetValue(toolCall, out string store))
                return "No store given";
            return await CallCustomAgent(systemPrompt, store, instruction, caller, false, cancel);
        });
        tools.Add(callAgent2);

        var callAgent3 = new HazinaChatTool($"custom_agent_write", $"Runs a custom agent that writes documents", [instructionParameter, systemPromptParameter, storeParameter], async (messages, toolCall, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            if (!instructionParameter.TryGetValue(toolCall, out string instruction))
                return "No instruction given";
            if (!systemPromptParameter.TryGetValue(toolCall, out string systemPrompt))
                return "No system prompt given";
            if (!storeParameter.TryGetValue(toolCall, out string store))
                return "No store given";
            if (WriteMode)
                return "Already in writemode";
            return await CallCustomAgent(systemPrompt, store, instruction, caller, true, cancel);
        });
        tools.Add(callAgent3);
    }

    private void AddAgentTools(IToolsContext tools, IEnumerable<string> agents, string caller)
    {
        foreach (var agent in agents)
        {
            var config = agentsConfig.FirstOrDefault(x => x.Name == agent);
            if (config == null)
                throw new Exception($"Agent {agent} is not defined. Request by {caller}.");
            if (config.ExplicitModify)
            {
                var callCoderAgent = new HazinaChatTool($"{agent}", $"Calls {agent} to modify the codebase. {config.Description} Be aware of the token limit of 8000 so only let the agents make small modifications at a time.", [instructionParameter], async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (WriteMode) return "Cannot give write instructions when in write mode";
                    if (instructionParameter.TryGetValue(toolCall, out string key))
                    {
                        WriteMode = true;
                        var result = await CallCoderAgent(agent, key, caller, cancel);
                        WriteMode = false;
                        return result;
                    }
                    return "No key given";
                });
                tools.Add(callCoderAgent);
            }
            else
            {
                var callAgent = new HazinaChatTool($"{agent}", $"Calls {agent} to execute a tasks and return a message. {config.Description}", [instructionParameter], async (messages, toolCall, cancel) =>
                {
                    cancel.ThrowIfCancellationRequested();
                    if (instructionParameter.TryGetValue(toolCall, out string key))
                        return await CallAgent(agent, key, caller, cancel);
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
            var config = flowsConfig.FirstOrDefault(x => x.Name == flow);
            if (config == null)
                throw new Exception($"Flow {flow} not found");
            var callFlow = new HazinaChatTool($"{flow}", $"Calls {flow} agent workflow to execute a tasks and return a message. {config.Description}", [instructionParameter], async (messages, toolCall, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                if (instructionParameter.TryGetValue(toolCall, out string key))
                {
                    var id = Guid.NewGuid().ToString();
                    tools.SendMessage(id, flow, key);
                    var response = await CallFlow(flow, key, caller, cancel);
                    tools.SendMessage(id, flow, response);
                    return response;
                }
                return "No key given";
            });
            tools.Add(callFlow);
        }
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancel)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Agent", EmailSettings.SmtpUser));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(EmailSettings.SmtpHost, EmailSettings.SmtpPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.SmtpUser, EmailSettings.SmtpPassword, cancel);
        await client.SendAsync(message, cancel);
        await client.DisconnectAsync(true, cancel);

        // also add to sent folder
        using var imap = new ImapClient();
        await imap.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await imap.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var sent = imap.GetFolder(SpecialFolder.Sent) ?? imap.GetFolder("Sent");
        await sent.OpenAsync(FolderAccess.ReadWrite);
        await sent.AppendAsync(message);
        await imap.DisconnectAsync(true, cancel);
        
        return true;
    }
    public async Task<List<EmailSummary>> ListInboxEmailsAsync(int amount, bool oldestFirst, CancellationToken cancel)
    {
        using var client = new MailKit.Net.Imap.ImapClient();
        await client.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancel);

        // Haal de laatste 10 UniqueIds op
        var uids = await inbox.SearchAsync(MailKit.Search.SearchQuery.All, cancel);
        uids = oldestFirst ? uids.Reverse().ToList() : uids;
        var lastUids = uids.Reverse().Take(amount).ToList();

        // Haal Envelope-gegevens (header info)
        var summaries = await inbox.FetchAsync(lastUids, MailKit.MessageSummaryItems.Envelope, cancel);

        var result = summaries
            .Select(summary => new EmailSummary
            {
                Id = summary.UniqueId.ToString(),
                Sender = summary.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address ?? "Unknown",
                Subject = summary.Envelope?.Subject ?? "(no subject)",
                Date = summary.Envelope?.Date?.DateTime ?? DateTime.MinValue
            })
            .OrderByDescending(e => e.Date)
            .ToList();

        await client.DisconnectAsync(true, cancel);
        return result;
    }
    public async Task<EmailDetail> ReadEmailAsync(string id, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancel);

        if (!UniqueId.TryParse(id, out var uid))
            return null;

        var message = await inbox.GetMessageAsync(uid, cancel);
        var body = message.TextBody ?? message.HtmlBody ?? "<no content>";

        await client.DisconnectAsync(true, cancel);

        return new EmailDetail
        {
            Sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown",
            Subject = message.Subject,
            Body = body
        };
    }

    public async Task<string> CreateMailboxFolderAsync(string folderName, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var personal = client.GetFolder(client.PersonalNamespaces[0]);

        try
        {
            if (await personal.GetSubfolderAsync(folderName, cancel) != null)
                return $"Folder '{folderName}' already exists.";
        }
        catch (MailKit.FolderNotFoundException)
        {
            // Expected: folder does not exist, so we can create it.
        }

        await personal.CreateAsync(folderName, true, cancel);

        await client.DisconnectAsync(true, cancel);
        return $"Folder '{folderName}' created successfully.";
    }

    public async Task<string> MoveEmailToFolderAsync(string emailUid, string targetFolderName, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancel);

        if (!UniqueId.TryParse(emailUid, out var uid))
            return "Invalid email UID.";

        var root = client.GetFolder(client.PersonalNamespaces[0]);
        var folders = await root.GetSubfoldersAsync(false, cancel);

        var dest = folders.FirstOrDefault(f => string.Equals(f.Name, targetFolderName, StringComparison.OrdinalIgnoreCase));
        if (dest == null)
            return $"Target folder '{targetFolderName}' does not exist.";

        //await dest.OpenAsync(FolderAccess.ReadWrite, cancel);
        await inbox.MoveToAsync(uid, dest, cancel);

        await client.DisconnectAsync(true, cancel);
        return $"Email moved to '{targetFolderName}'.";
    }

    public async Task<List<string>> ListMailboxFoldersAsync(CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(EmailSettings.ImapHost, EmailSettings.ImapPort, EmailSettings.UseSsl, cancel);
        await client.AuthenticateAsync(EmailSettings.ImapUser, EmailSettings.ImapPassword, cancel);

        var root = await client.GetFolderAsync(client.PersonalNamespaces[0].Path, cancel);

        var all = new List<IMailFolder>();
        await EnumerateFoldersAsync(root, all, cancel, isRoot: true);

        await client.DisconnectAsync(true, cancel);

        return all.Select(f => f.FullName).ToList();
    }

    private async Task EnumerateFoldersAsync(IMailFolder folder, List<IMailFolder> list, CancellationToken cancel, bool isRoot = false)
    {
        // Voeg de folder toe aan de lijst, ook als je 'm niet opent
        list.Add(folder);

        // Alleen openen als de folder daadwerkelijk berichten kan bevatten
        if (!folder.Attributes.HasFlag(FolderAttributes.NoSelect))
        {
            try
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, cancel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kon folder '{folder.FullName}' niet openen: {ex.Message}");
            }
        }

        // Recursief alle subfolders ophalen
        foreach (var sub in await folder.GetSubfoldersAsync(false, cancel))
        {
            await EnumerateFoldersAsync(sub, list, cancel, false);
        }
    }

    public EmailSettings EmailSettings = new EmailSettings();
}
//public class EmailSettings
//{
//    public string SmtpHost = "mail.zxcs.nl";
//    public int SmtpPort = 587;
//    public string SmtpUser = "formsubmissions@martiendejong.nl";
//    public string SmtpPassword = "ysZU9TarE5qNZRvEBntY";
//    public string ImapHost = "mail.zxcs.nl";
//    public int ImapPort = 993;
//    public string ImapUser = "formsubmissions@martiendejong.nl";
//    public string ImapPassword = "ysZU9TarE5qNZRvEBntY";
//    public bool UseSsl = true;
//}
public class EmailSettings
{
    public string SmtpHost = "mail.zxcs.nl";
    public int SmtpPort = 587;
    public string SmtpUser = "info@martiendejong.nl";
    public string SmtpPassword = "hLPFy6MdUnfEDbYTwXps";
    public string ImapHost = "mail.zxcs.nl";
    public int ImapPort = 993;
    public string ImapUser = "info@martiendejong.nl";
    public string ImapPassword = "hLPFy6MdUnfEDbYTwXps";
    public bool UseSsl = true;
}
public class EmailSummary
{
    public string Id { get; set; }
    public string Sender { get; set; }
    public string Subject { get; set; }
    public DateTime Date { get; set; }
}

public class EmailDetail
{
    public string Sender { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

