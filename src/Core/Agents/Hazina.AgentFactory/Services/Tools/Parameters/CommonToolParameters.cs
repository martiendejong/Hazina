using Hazina.LLMs;

namespace Hazina.AgentFactory.Services.Tools.Parameters;

/// <summary>
/// Common reusable tool parameters used across different tools.
/// Extracted from AgentFactory to centralize parameter definitions.
/// </summary>
public static class CommonToolParameters
{
    // File/Store parameters
    public static readonly ChatToolParameter Key = new()
    {
        Name = "key",
        Description = "The key/path of the file.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter Content = new()
    {
        Name = "content",
        Description = "The content of the file.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter Folder = new()
    {
        Name = "path",
        Description = "The partial path, the folder to query.",
        Type = "string",
        Required = false
    };

    public static readonly ChatToolParameter FilesRecursive = new()
    {
        Name = "recursive",
        Description = "Returns the files in subfolders as well.",
        Type = "boolean",
        Required = false
    };

    public static readonly ChatToolParameter Relevancy = new()
    {
        Name = "query",
        Description = "The relevancy search query.",
        Type = "string",
        Required = true
    };

    // Agent/Flow parameters
    public static readonly ChatToolParameter Instruction = new()
    {
        Name = "instruction",
        Description = "The instruction to send to the agent.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter SystemPrompt = new()
    {
        Name = "system_prompt",
        Description = "The system prompt for the agent.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter Store = new()
    {
        Name = "store",
        Description = "The store that the agent has access to.",
        Type = "string",
        Required = true
    };

    // Build/CLI parameters
    public static readonly ChatToolParameter Arguments = new()
    {
        Name = "arguments",
        Description = "The arguments to call git with.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter TimeoutSeconds = new()
    {
        Name = "timeout",
        Description = "The maximum number of seconds this process is allowed to run.",
        Type = "number",
        Required = true
    };

    // WordPress parameters
    public static readonly ChatToolParameter WpCommand = new()
    {
        Name = "command",
        Description = "The wp cli command to call.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter WpArguments = new()
    {
        Name = "arguments",
        Description = "The arguments to call the wordpress cli command with.",
        Type = "string",
        Required = false
    };

    // BigQuery parameters
    public static readonly ChatToolParameter BigQueryArguments = new()
    {
        Name = "arguments",
        Description = "The arguments to call Google BigQuery with.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter BigQueryDataSet = new()
    {
        Name = "datacollection",
        Description = "The dataset in Google BigQuery.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter BigQueryTableName = new()
    {
        Name = "tablename",
        Description = "The table name in Google BigQuery.",
        Type = "string",
        Required = true
    };

    // Email parameters
    public static readonly ChatToolParameter Recipient = new()
    {
        Name = "recipient",
        Description = "The email address that receives the email.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter Subject = new()
    {
        Name = "subject",
        Description = "The subject of the email.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter Body = new()
    {
        Name = "body",
        Description = "The body content of the email.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter EmailId = new()
    {
        Name = "emailId",
        Description = "The ID or index of the email.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter FolderName = new()
    {
        Name = "folder",
        Description = "The email folder.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter DestinationFolder = new()
    {
        Name = "destinationFolder",
        Description = "The destination folder for the email.",
        Type = "string",
        Required = true
    };

    public static readonly ChatToolParameter EmailAmount = new()
    {
        Name = "amount",
        Description = "The number of emails to read.",
        Type = "number",
        Required = true
    };

    public static readonly ChatToolParameter EmailOldestFirst = new()
    {
        Name = "oldestFirst",
        Description = "If we should return the oldest emails first.",
        Type = "boolean",
        Required = true
    };
}
