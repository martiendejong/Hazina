using Hazina.AI.FluentAPI.Configuration;
using Hazina.LLMs;
using Hazina.Tools.Models;
using System.Text.Json;

namespace Hazina.Examples.CustomTools;

/// <summary>
/// Demonstrates creating and using custom tools (function calling) with Hazina.
///
/// This example shows:
/// - Defining custom tools that the AI can call
/// - Registering tools with the LLM
/// - Handling tool calls and returning results
/// - Multi-turn tool calling conversations
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - OpenAI API key (function calling requires GPT-3.5-turbo or GPT-4)
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Custom Tools Example ===\n");

        // 1. Setup: Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");
            return;
        }

        try
        {
            // 2. Create AI client
            var ai = QuickSetup.SetupOpenAI(apiKey);
            Console.WriteLine("✓ AI client initialized\n");

            // 3. Define custom tools
            var tools = new ToolsContext();

            // Tool 1: Get current weather
            tools.RegisterTool(new ToolDefinition
            {
                Name = "get_weather",
                Description = "Get the current weather for a location",
                Parameters = new ToolParameters
                {
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["location"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "The city name (e.g., 'London', 'New York')"
                        },
                        ["unit"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Temperature unit (celsius or fahrenheit)",
                            Enum = new[] { "celsius", "fahrenheit" }
                        }
                    },
                    Required = new[] { "location" }
                },
                Handler = async (args) =>
                {
                    var location = args["location"]?.ToString() ?? "Unknown";
                    var unit = args.ContainsKey("unit") ? args["unit"]?.ToString() : "celsius";

                    // Simulate API call (in real app, call actual weather API)
                    Console.WriteLine($"[TOOL CALLED] get_weather(location='{location}', unit='{unit}')");

                    var temperature = unit == "celsius" ? 18 : 64;
                    var weather = new
                    {
                        location,
                        temperature,
                        unit,
                        condition = "Partly cloudy",
                        humidity = 65,
                        wind_speed = 12
                    };

                    return JsonSerializer.Serialize(weather);
                }
            });

            // Tool 2: Calculate mortgage payment
            tools.RegisterTool(new ToolDefinition
            {
                Name = "calculate_mortgage",
                Description = "Calculate monthly mortgage payment",
                Parameters = new ToolParameters
                {
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["principal"] = new ToolProperty
                        {
                            Type = "number",
                            Description = "Loan amount in dollars"
                        },
                        ["interest_rate"] = new ToolProperty
                        {
                            Type = "number",
                            Description = "Annual interest rate (e.g., 3.5 for 3.5%)"
                        },
                        ["years"] = new ToolProperty
                        {
                            Type = "number",
                            Description = "Loan term in years"
                        }
                    },
                    Required = new[] { "principal", "interest_rate", "years" }
                },
                Handler = async (args) =>
                {
                    var principal = Convert.ToDouble(args["principal"]);
                    var annualRate = Convert.ToDouble(args["interest_rate"]);
                    var years = Convert.ToInt32(args["years"]);

                    Console.WriteLine($"[TOOL CALLED] calculate_mortgage(principal=${principal:N0}, rate={annualRate}%, years={years})");

                    // Calculate monthly payment
                    var monthlyRate = annualRate / 100 / 12;
                    var numPayments = years * 12;
                    var monthlyPayment = principal * (monthlyRate * Math.Pow(1 + monthlyRate, numPayments)) /
                                        (Math.Pow(1 + monthlyRate, numPayments) - 1);

                    var result = new
                    {
                        monthly_payment = Math.Round(monthlyPayment, 2),
                        total_paid = Math.Round(monthlyPayment * numPayments, 2),
                        total_interest = Math.Round((monthlyPayment * numPayments) - principal, 2)
                    };

                    return JsonSerializer.Serialize(result);
                }
            });

            // Tool 3: Search internal database (simulated)
            tools.RegisterTool(new ToolDefinition
            {
                Name = "search_products",
                Description = "Search product catalog by name or category",
                Parameters = new ToolParameters
                {
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["query"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Search query (product name or category)"
                        }
                    },
                    Required = new[] { "query" }
                },
                Handler = async (args) =>
                {
                    var query = args["query"]?.ToString()?.ToLower() ?? "";

                    Console.WriteLine($"[TOOL CALLED] search_products(query='{query}')");

                    // Simulated product database
                    var products = new[]
                    {
                        new { id = 1, name = "Laptop", category = "Electronics", price = 999 },
                        new { id = 2, name = "Mouse", category = "Electronics", price = 25 },
                        new { id = 3, name = "Desk Chair", category = "Furniture", price = 199 },
                        new { id = 4, name = "Standing Desk", category = "Furniture", price = 499 }
                    };

                    var results = products
                        .Where(p => p.name.ToLower().Contains(query) || p.category.ToLower().Contains(query))
                        .ToList();

                    return JsonSerializer.Serialize(new { count = results.Count, products = results });
                }
            });

            Console.WriteLine($"✓ Registered {tools.GetToolCount()} custom tools\n");

            // 4. Example 1: Weather query
            Console.WriteLine("--- Example 1: Weather Query ---\n");
            await RunConversation(ai, tools, "What's the weather like in Paris?");

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // 5. Example 2: Mortgage calculation
            Console.WriteLine("--- Example 2: Mortgage Calculation ---\n");
            await RunConversation(ai, tools, "Calculate monthly payment for a $300,000 loan at 4.5% interest for 30 years");

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // 6. Example 3: Product search
            Console.WriteLine("--- Example 3: Product Search ---\n");
            await RunConversation(ai, tools, "Show me electronics products");

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // 7. Example 4: Multi-tool query
            Console.WriteLine("--- Example 4: Multi-Tool Query ---\n");
            await RunConversation(ai, tools,
                "I'm looking for a desk chair and also curious about the weather in Seattle");

            Console.WriteLine("\n✓ Success! Your custom tools are working.");
            Console.WriteLine("\nKey takeaway:");
            Console.WriteLine("  AI automatically decides which tools to call based on user queries.");
            Console.WriteLine("  You define tools, AI figures out when and how to use them.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }

    static async Task RunConversation(ILLMClient ai, IToolsContext tools, string userMessage)
    {
        Console.WriteLine($"User: {userMessage}\n");

        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.System,
                Text = "You are a helpful assistant. Use the available tools to answer user questions accurately."
            },
            new()
            {
                Role = HazinaMessageRole.User,
                Text = userMessage
            }
        };

        // AI may need multiple turns to call tools and formulate response
        int maxTurns = 5;
        for (int turn = 0; turn < maxTurns; turn++)
        {
            var response = await ai.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                tools,
                null,
                CancellationToken.None
            );

            // Check if AI wants to call tools
            if (response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                // Add AI's tool call request to conversation
                messages.Add(new HazinaChatMessage
                {
                    Role = HazinaMessageRole.Assistant,
                    ToolCalls = response.ToolCalls
                });

                // Execute each tool call
                foreach (var toolCall in response.ToolCalls)
                {
                    var toolResult = await tools.ExecuteToolAsync(toolCall.Name, toolCall.Arguments);

                    // Add tool result to conversation
                    messages.Add(new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.Tool,
                        ToolCallId = toolCall.Id,
                        Text = toolResult
                    });
                }

                // Continue conversation with tool results
                continue;
            }

            // AI has final response (no more tool calls)
            Console.WriteLine($"Assistant: {response.Result}\n");

            if (response.TokenUsage != null)
            {
                Console.WriteLine($"Tokens used: {response.TokenUsage.TotalTokens}");
            }

            break;
        }
    }
}
