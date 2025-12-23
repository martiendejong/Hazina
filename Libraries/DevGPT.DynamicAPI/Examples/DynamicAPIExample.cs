using DevGPT.DynamicAPI.Core;
using DevGPT.DynamicAPI.Tools;
using DevGPT.DynamicAPI.Models;

namespace DevGPT.DynamicAPI.Examples;

/// <summary>
/// Example usage of the Dynamic API Integration system
/// </summary>
public class DynamicAPIExample
{
    public static async Task RunExamples()
    {
        // Initialize credential store
        var credStore = new CredentialStore("./credentials");

        // Example 1: Store some credentials
        await StoreExampleCredentials(credStore);

        // Example 2: Use DynamicAPIClient directly
        await DirectAPICallExample(credStore);

        // Example 3: Use with DevGPT agent tools
        await AgentToolExample(credStore);

        // Example 4: Web search for API documentation
        await WebSearchExample(credStore);

        // Example 5: Complete workflow - discover API and call it
        await CompleteWorkflowExample(credStore);
    }

    private static async Task StoreExampleCredentials(CredentialStore credStore)
    {
        Console.WriteLine("=== Example 1: Store Credentials ===\n");

        // Store Stripe API key
        await credStore.StoreCredential("stripe", "api_key", "sk_test_example123");
        Console.WriteLine("Stored Stripe credentials");

        // Store Bing Search API key (required for web search)
        await credStore.StoreCredential("bing", "api_key", "your_bing_api_key_here");
        Console.WriteLine("Stored Bing Search credentials");

        // List all services with stored credentials
        var services = credStore.ListServices();
        Console.WriteLine($"\nServices with credentials: {string.Join(", ", services)}");
        Console.WriteLine();
    }

    private static async Task DirectAPICallExample(CredentialStore credStore)
    {
        Console.WriteLine("=== Example 2: Direct API Call ===\n");

        var apiClient = new DynamicAPIClient(credStore);

        // Call a public API (no auth required)
        var publicResponse = await apiClient.Get("https://api.github.com/repos/microsoft/typescript");

        Console.WriteLine($"GitHub API Response (HTTP {publicResponse.StatusCode}):");
        if (publicResponse.IsSuccess && publicResponse.Body != null)
        {
            // Pretty print JSON (first 500 chars)
            var preview = publicResponse.Body.Length > 500
                ? publicResponse.Body.Substring(0, 500) + "..."
                : publicResponse.Body;
            Console.WriteLine(preview);
        }
        else
        {
            Console.WriteLine($"Error: {publicResponse.ErrorMessage}");
        }
        Console.WriteLine();
    }

    private static async Task AgentToolExample(CredentialStore credStore)
    {
        Console.WriteLine("=== Example 3: Agent Tool Usage ===\n");

        var apiClient = new DynamicAPIClient(credStore);
        var apiTool = new DynamicAPIDevGPTTool(apiClient);

        Console.WriteLine($"Tool Name: {apiTool.FunctionName}");
        Console.WriteLine($"Description: {apiTool.Description}");
        Console.WriteLine($"Parameters: {apiTool.Parameters.Count}");

        foreach (var param in apiTool.Parameters)
        {
            Console.WriteLine($"  - {param.Name} ({param.Type}): {param.Description}");
        }
        Console.WriteLine();
    }

    private static async Task WebSearchExample(CredentialStore credStore)
    {
        Console.WriteLine("=== Example 4: Web Search ===\n");

        try
        {
            var searchTool = new WebSearchTool(credStore);

            // Search for Stripe API documentation
            var searchResult = await searchTool.SearchApiDocumentation("Stripe", "create payment intent");

            Console.WriteLine($"Found {searchResult.Results.Count} results:");
            foreach (var result in searchResult.Results.Take(3))
            {
                Console.WriteLine($"\n  Title: {result.Title}");
                Console.WriteLine($"  URL: {result.Url}");
                Console.WriteLine($"  Snippet: {result.Snippet}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Web search failed (you may need to configure Bing API key): {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task CompleteWorkflowExample(CredentialStore credStore)
    {
        Console.WriteLine("=== Example 5: Complete Workflow ===\n");
        Console.WriteLine("This example shows how an agent would:");
        Console.WriteLine("1. Search for API documentation");
        Console.WriteLine("2. Learn about the API endpoints");
        Console.WriteLine("3. Make authenticated API calls");
        Console.WriteLine();

        // Step 1: Search for documentation
        Console.WriteLine("Step 1: Searching for API documentation...");
        try
        {
            var searchTool = new WebSearchTool(credStore);
            var docs = await searchTool.SearchApiDocumentation("JSONPlaceholder", "posts");

            if (docs.Results.Any())
            {
                Console.WriteLine($"Found documentation: {docs.Results.First().Url}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search skipped: {ex.Message}");
        }

        // Step 2: Make an API call based on discovered information
        Console.WriteLine("\nStep 2: Making API call to discovered endpoint...");
        var apiClient = new DynamicAPIClient(credStore);

        var response = await apiClient.Get("https://jsonplaceholder.typicode.com/posts/1");

        if (response.IsSuccess)
        {
            Console.WriteLine($"✓ Success! Retrieved data:");
            Console.WriteLine(response.Body);
        }
        else
        {
            Console.WriteLine($"✗ Failed: {response.ErrorMessage}");
        }

        Console.WriteLine("\n=== Workflow Complete ===");
    }
}

/// <summary>
/// Example of how to use Dynamic API tools with a DevGPT agent
/// </summary>
public class AgentIntegrationExample
{
    public static ToolsContext CreateToolsWithDynamicAPI(CredentialStore credStore)
    {
        var apiClient = new DynamicAPIClient(credStore);
        var searchTool = new WebSearchTool(credStore);

        var tools = new ToolsContext();

        // Add dynamic API tools
        tools.Add(new WebSearchDevGPTTool(searchTool));
        tools.Add(new FetchUrlDevGPTTool(searchTool));
        tools.Add(new DynamicAPIDevGPTTool(apiClient));

        return tools;
    }

    public static async Task RunAgentExample()
    {
        var credStore = new CredentialStore("./credentials");
        var tools = CreateToolsWithDynamicAPI(credStore);

        Console.WriteLine($"Created ToolsContext with {tools.Tools.Count} tools:");
        foreach (var tool in tools.Tools)
        {
            Console.WriteLine($"  - {tool.FunctionName}: {tool.Description}");
        }

        // Now you can use this ToolsContext with any DevGPTAgent
        // Example:
        // var agent = new DevGPTAgent("api_researcher", generator, tools);
        // var response = await agent.Generator.GetResponse("Find and call the Stripe API to list customers", cancel);
    }
}
