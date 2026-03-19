using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.AgentFactory.Configuration.Schema;

/// <summary>
/// Generates JSON schemas for Hazina configuration objects.
/// Enables IDE autocomplete and validation when authoring configs.
/// </summary>
public static class ConfigSchemaGenerator
{
    /// <summary>
    /// Generates a JSON schema for StoreConfig.
    /// </summary>
    public static string GenerateStoreConfigSchema()
    {
        var schema = new
        {
            schema = "https://json-schema.org/draft/2020-12/schema",
            id = "https://hazina.dev/schemas/store-config.json",
            title = "Hazina Store Configuration",
            description = "Configuration schema for Hazina knowledge stores",
            type = "object",
            properties = new
            {
                Name = new
                {
                    type = "string",
                    description = "Unique name of the store",
                    minLength = 1
                },
                Description = new
                {
                    type = "string",
                    description = "Human-readable description of the store's purpose"
                },
                Path = new
                {
                    type = "string",
                    description = "File system path to the store location (absolute or relative)",
                    minLength = 1
                },
                FileFilters = new
                {
                    type = "array",
                    description = "File filter patterns (e.g., '*.cs', '*.txt')",
                    items = new { type = "string" },
                    examples = new[] { new[] { "*.cs", "*.ts" }, new[] { "*.md", "*.txt" } }
                },
                SubDirectory = new
                {
                    type = "string",
                    description = "Optional subdirectory within the path to restrict access",
                    defaultValue = ""
                },
                ExcludePattern = new
                {
                    type = "array",
                    description = "Patterns to exclude from the store",
                    items = new { type = "string" },
                    examples = new[] { new[] { "node_modules", ".git", "bin", "obj" } }
                },
                IsReadOnly = new
                {
                    type = "boolean",
                    description = "Whether the store is read-only (safe default)",
                    defaultValue = true
                },
                ExplicitWriteEnabled = new
                {
                    type = "boolean",
                    description = "Explicit confirmation that write access is intended",
                    defaultValue = false
                },
                MaxFileSizeBytes = new
                {
                    type = "integer",
                    description = "Maximum file size in bytes (prevents memory exhaustion)",
                    minimum = 0,
                    defaultValue = 10485760 // 10MB
                },
                AllowedExtensions = new
                {
                    type = "array",
                    description = "Allowed file extensions for safety (e.g., ['.txt', '.md'])",
                    items = new { type = "string", pattern = "^\\." },
                    examples = new[] { new[] { ".txt", ".md", ".json" } }
                },
                FollowSymlinks = new
                {
                    type = "boolean",
                    description = "Whether to follow symbolic links (security consideration)",
                    defaultValue = false
                },
                MaxDirectoryDepth = new
                {
                    type = "integer",
                    description = "Maximum depth for directory traversal (prevents infinite loops)",
                    minimum = 1,
                    maximum = 100,
                    defaultValue = 10
                }
            },
            required = new[] { "Name", "Path" },
            additionalProperties = false
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Generates a JSON schema for AgentConfig.
    /// </summary>
    public static string GenerateAgentConfigSchema()
    {
        var schema = new
        {
            schema = "https://json-schema.org/draft/2020-12/schema",
            id = "https://hazina.dev/schemas/agent-config.json",
            title = "Hazina Agent Configuration",
            description = "Configuration schema for Hazina AI agents",
            type = "object",
            properties = new
            {
                Name = new
                {
                    type = "string",
                    description = "Unique name of the agent",
                    minLength = 1
                },
                Description = new
                {
                    type = "string",
                    description = "Human-readable description of the agent's purpose"
                },
                Prompt = new
                {
                    type = "string",
                    description = "System prompt that defines the agent's behavior and capabilities",
                    minLength = 1
                },
                Stores = new
                {
                    type = "array",
                    description = "Knowledge stores accessible by this agent",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            Name = new { type = "string", description = "Name of the store" },
                            Write = new { type = "boolean", description = "Whether write access is granted", defaultValue = false }
                        },
                        required = new[] { "Name" }
                    }
                },
                Functions = new
                {
                    type = "array",
                    description = "Function/tool names available to the agent",
                    items = new { type = "string" }
                },
                CallsAgents = new
                {
                    type = "array",
                    description = "Other agents this agent can delegate to",
                    items = new { type = "string" }
                },
                CallsFlows = new
                {
                    type = "array",
                    description = "Flows this agent can trigger",
                    items = new { type = "string" }
                },
                ExplicitModify = new
                {
                    type = "boolean",
                    description = "Whether the agent requires explicit permission for modifications",
                    defaultValue = false
                }
            },
            required = new[] { "Name", "Prompt" },
            additionalProperties = false
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Generates a JSON schema for FlowConfig.
    /// </summary>
    public static string GenerateFlowConfigSchema()
    {
        var schema = new
        {
            schema = "https://json-schema.org/draft/2020-12/schema",
            id = "https://hazina.dev/schemas/flow-config.json",
            title = "Hazina Flow Configuration",
            description = "Configuration schema for Hazina agent flows (multi-agent orchestration)",
            type = "object",
            properties = new
            {
                Name = new
                {
                    type = "string",
                    description = "Unique name of the flow",
                    minLength = 1
                },
                Description = new
                {
                    type = "string",
                    description = "Human-readable description of the flow's purpose"
                },
                CallsAgents = new
                {
                    type = "array",
                    description = "Agents that participate in this flow (in execution order)",
                    items = new { type = "string" },
                    minItems = 1
                }
            },
            required = new[] { "Name", "CallsAgents" },
            additionalProperties = false
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Saves all schemas to the specified directory.
    /// </summary>
    public static void SaveAllSchemas(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        File.WriteAllText(
            Path.Combine(outputDirectory, "store-config.schema.json"),
            GenerateStoreConfigSchema());

        File.WriteAllText(
            Path.Combine(outputDirectory, "agent-config.schema.json"),
            GenerateAgentConfigSchema());

        File.WriteAllText(
            Path.Combine(outputDirectory, "flow-config.schema.json"),
            GenerateFlowConfigSchema());
    }
}
