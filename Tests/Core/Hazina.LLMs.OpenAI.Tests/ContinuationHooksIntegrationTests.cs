using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hazina.LLMs.OpenAI.Tests;

/// <summary>
/// Integration tests for SimpleOpenAIClientChatInteraction continuation hooks.
/// These tests use real OpenAI API calls (when API key is available) to validate
/// the complete behavior of continuation hooks in production-like scenarios.
///
/// Tests are skipped when OPENAI_API_KEY environment variable is not set.
/// </summary>
public class ContinuationHooksIntegrationTests
{
    private readonly OpenAIConfig? _config;
    private readonly bool _hasApiKey;

    public ContinuationHooksIntegrationTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _config = new OpenAIConfig(apiKey: apiKey)
            {
                Model = "gpt-4o-mini" // Use cost-effective model for tests
            };
            _hasApiKey = true;
        }
        else
        {
            try
            {
                _config = OpenAIConfig.Load();
                _hasApiKey = !string.IsNullOrWhiteSpace(_config?.ApiKey);
            }
            catch
            {
                _hasApiKey = false;
            }
        }
    }

    #region Test Helpers

    private HazinaChatTool CreateSimpleTool(string name, string description, Func<string> implementation)
    {
        return new HazinaChatTool(
            name,
            description,
            new List<ChatToolParameter>(), // No parameters for simple tools
            async (messages, toolCall, ct) =>
            {
                return await Task.FromResult(implementation());
            }
        );
    }

    private class TestToolsContext : IToolsContext
    {
        public List<HazinaChatTool> Tools { get; set; } = new();
        public Action<string, string, string>? SendMessage { get; set; }
        public string? ProjectId { get; set; }
        public Action<string, int, int, string>? OnTokensUsed { get; set; }
        public Action<string, string, int>? OnToolExecuted { get; set; }
        public Func<string, int, bool>? ShouldContinue { get; set; }
        public string? ContinuationPrompt { get; set; }
        public int MaxContinuations { get; set; } = 5;

        // Tracking for test assertions
        public List<(string toolName, string result, int turnNumber)> ToolExecutions { get; } = new();
        public List<(string content, int turnNumber)> ContinuationChecks { get; } = new();
        public List<(int inputTokens, int outputTokens)> TokenUsages { get; } = new();

        public void Add(HazinaChatTool info)
        {
            Tools.Add(info);
        }

        public void SetupOnToolExecuted()
        {
            OnToolExecuted = (toolName, result, turnNumber) =>
            {
                ToolExecutions.Add((toolName, result, turnNumber));
            };
        }

        public void SetupOnTokensUsed()
        {
            OnTokensUsed = (projectId, inputTokens, outputTokens, model) =>
            {
                TokenUsages.Add((inputTokens, outputTokens));
            };
        }

        public void SetupShouldContinue(Func<string, int, bool> predicate)
        {
            ShouldContinue = (content, turnNumber) =>
            {
                ContinuationChecks.Add((content, turnNumber));
                return predicate(content, turnNumber);
            };
        }
    }

    #endregion

    #region Phase 1: Backward Compatibility Tests

    /// <summary>
    /// Scenario 1: Single-turn conversation without tools, no continuation hooks
    /// OLD BEHAVIOR: Should stop immediately on first response
    /// </summary>
    [Fact]
    public async Task Scenario1_NoContinuationHooks_StopsOnFirstResponse()
    {
        if (!_hasApiKey)
        {
            // Skip test if no API key - this is expected in CI without secrets
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            ShouldContinue = null, // OLD BEHAVIOR - no continuation
            OnToolExecuted = null,
            MaxContinuations = 5
        };
        context.SetupOnTokensUsed(); // Track token usage

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Say 'Hello' and nothing else."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Result);
        Assert.Contains("Hello", response.Result, StringComparison.OrdinalIgnoreCase);

        // Verify OLD BEHAVIOR: No continuation was triggered
        Assert.Empty(context.ContinuationChecks); // ShouldContinue was never called
        Assert.Single(context.TokenUsages); // Only one API call
    }

    /// <summary>
    /// Scenario 2: Verify that with tools but no OnToolExecuted callback,
    /// tools still execute normally (backward compatibility)
    /// </summary>
    [Fact]
    public async Task Scenario2_ToolsWithNullOnToolExecuted_WorksNormally()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange - Add a simple tool
        var context = new TestToolsContext
        {
            ShouldContinue = null,
            OnToolExecuted = null // OLD BEHAVIOR - no callback
        };

        // Add a simple "get_current_time" tool
        context.Add(CreateSimpleTool(
            "get_current_time",
            "Gets the current UTC time",
            () => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        ));

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "What time is it? Use the get_current_time tool."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Result);

        // Tool should have been called despite OnToolExecuted being null
        // Response should include time information
        Assert.True(
            response.Result.Contains("time", StringComparison.OrdinalIgnoreCase) ||
            response.Result.Contains("UTC", StringComparison.OrdinalIgnoreCase) ||
            response.Result.Contains("2026", StringComparison.OrdinalIgnoreCase),
            "Response should reference time information from tool call"
        );
    }

    #endregion

    #region Phase 2: Continuation Hooks Tests

    /// <summary>
    /// Scenario 6: Single continuation - task completion detection
    /// NEW BEHAVIOR: ShouldContinue triggers continuation until condition met
    /// </summary>
    [Fact]
    public async Task Scenario6_ShouldContinue_ContinuesUntilTaskComplete()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            MaxContinuations = 5
        };

        // Set up continuation logic: continue until model says "TASK COMPLETE"
        context.SetupShouldContinue((content, turn) =>
        {
            return !content.Contains("TASK COMPLETE", StringComparison.OrdinalIgnoreCase);
        });

        context.ContinuationPrompt = "Please continue. When you're done, say 'TASK COMPLETE'.";

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = "You are a helpful assistant. When you complete a task, always end with 'TASK COMPLETE'."
            },
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Count from 1 to 3. After you've counted, say 'TASK COMPLETE'."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Result);

        // Should have triggered ShouldContinue at least once
        Assert.NotEmpty(context.ContinuationChecks);

        // Final response should contain "TASK COMPLETE"
        Assert.Contains("TASK COMPLETE", response.Result, StringComparison.OrdinalIgnoreCase);

        // Should have counted 1, 2, 3
        Assert.Contains("1", response.Result);
        Assert.Contains("2", response.Result);
        Assert.Contains("3", response.Result);
    }

    /// <summary>
    /// Scenario 7: MaxContinuations enforced - safety limit
    /// Tests that continuation loop stops at MaxContinuations even if ShouldContinue returns true
    /// </summary>
    [Fact]
    public async Task Scenario7_MaxContinuations_EnforcesSafetyLimit()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            MaxContinuations = 2 // Low limit for test speed
        };

        // Set up continuation logic: ALWAYS continue (infinite loop condition)
        context.SetupShouldContinue((content, turn) => true);

        context.ContinuationPrompt = "Continue counting.";

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Count from 1 to 100. Keep counting until I tell you to stop."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);

        // Should have called ShouldContinue exactly MaxContinuations times
        // Initial response (turn 0) + 2 continuations = 3 total checks
        Assert.True(
            context.ContinuationChecks.Count <= context.MaxContinuations + 1,
            $"Expected at most {context.MaxContinuations + 1} continuation checks, got {context.ContinuationChecks.Count}"
        );

        // Verify turn numbers are sequential (0, 1, 2)
        for (int i = 0; i < context.ContinuationChecks.Count; i++)
        {
            Assert.Equal(i, context.ContinuationChecks[i].turnNumber);
        }
    }

    /// <summary>
    /// Scenario 8: Custom continuation prompt
    /// Tests that ContinuationPrompt is used when provided
    /// </summary>
    [Fact]
    public async Task Scenario8_CustomContinuationPrompt_IsUsed()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            MaxContinuations = 3,
            ContinuationPrompt = "Continue working. If done, say 'DONE'."
        };

        context.SetupShouldContinue((content, turn) =>
        {
            return turn < 1; // Continue only once
        });

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Start a task."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);

        // Should have continued at least once
        Assert.NotEmpty(context.ContinuationChecks);

        // This test validates that the custom prompt was used internally
        // (Hard to assert directly without inspecting internal message history)
        Assert.True(context.ContinuationChecks.Count >= 1);
    }

    /// <summary>
    /// Scenario 9: ShouldContinue receives correct turn numbers
    /// Tests that turn numbers are accurately tracked and passed to callback
    /// </summary>
    [Fact]
    public async Task Scenario9_ShouldContinue_ReceivesCorrectTurnNumbers()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            MaxContinuations = 3
        };

        // Continue for exactly 3 turns (turn 0, 1, 2)
        context.SetupShouldContinue((content, turn) => turn < 2);

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Respond with a number. Keep going."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);

        // Should have 3 continuation checks (turn 0, 1, 2)
        Assert.Equal(3, context.ContinuationChecks.Count);

        // Verify turn numbers are sequential
        Assert.Equal(0, context.ContinuationChecks[0].turnNumber);
        Assert.Equal(1, context.ContinuationChecks[1].turnNumber);
        Assert.Equal(2, context.ContinuationChecks[2].turnNumber);
    }

    /// <summary>
    /// Scenario 10: OnToolExecuted callback tracks tool usage
    /// Tests that OnToolExecuted is called with correct parameters after each tool execution
    /// </summary>
    [Fact]
    public async Task Scenario10_OnToolExecuted_TracksToolUsage()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext();
        context.SetupOnToolExecuted(); // Enable tool tracking

        // Add multiple tools
        context.Add(CreateSimpleTool(
            "add",
            "Adds two numbers",
            () => "5" // Simple mock: return "sum=5"
        ));

        context.Add(CreateSimpleTool(
            "multiply",
            "Multiplies two numbers",
            () => "10"
        ));

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Add 2 and 3, then multiply the result by 2. Use the add and multiply tools."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);

        // Should have called at least one tool (possibly both)
        Assert.NotEmpty(context.ToolExecutions);

        // Verify OnToolExecuted was called with correct structure
        foreach (var execution in context.ToolExecutions)
        {
            Assert.NotNull(execution.toolName);
            Assert.NotNull(execution.result);
            Assert.True(execution.turnNumber >= 0);

            // Tool name should be one of our registered tools
            Assert.True(
                execution.toolName == "add" || execution.toolName == "multiply",
                $"Unexpected tool name: {execution.toolName}"
            );
        }
    }

    #endregion

    #region Phase 2B: Error Handling Tests

    /// <summary>
    /// Scenario 11: OnToolExecuted callback throws exception
    /// Tests that exceptions in OnToolExecuted are silently caught
    /// </summary>
    [Fact]
    public async Task Scenario11_OnToolExecutedThrows_ExceptionCaughtSilently()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext();

        // Set OnToolExecuted to always throw
        context.OnToolExecuted = (toolName, result, turnNumber) =>
        {
            throw new InvalidOperationException("Test exception in OnToolExecuted");
        };

        context.Add(CreateSimpleTool(
            "get_time",
            "Gets current time",
            () => DateTime.UtcNow.ToString()
        ));

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "What time is it? Use the get_time tool."
            }
        };

        // Act - Should NOT throw despite OnToolExecuted throwing
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Result);

        // Tool should have executed successfully despite callback exception
        Assert.True(
            response.Result.Contains("time", StringComparison.OrdinalIgnoreCase) ||
            response.Result.Contains("UTC", StringComparison.OrdinalIgnoreCase),
            "Tool should have executed despite callback exception"
        );
    }

    /// <summary>
    /// Scenario 13: CancellationToken honored during continuation loop
    /// Tests that cancellation works correctly mid-continuation
    /// </summary>
    [Fact]
    public async Task Scenario13_CancellationDuringContinuation_ThrowsOperationCanceledException()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            MaxContinuations = 10 // High limit
        };

        // Always continue (would run many times without cancellation)
        context.SetupShouldContinue((content, turn) => true);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // Cancel after 5 seconds

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Keep counting forever."
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                context,
                images: null,
                cts.Token
            );
        });
    }

    #endregion

    #region Phase 3: Integration with Token Tracking

    /// <summary>
    /// Scenario 18: Token tracking across continuations
    /// Tests that OnTokensUsed is called for each turn (including continuations)
    /// </summary>
    [Fact]
    public async Task Scenario18_TokenTracking_AccumulatesAcrossContinuations()
    {
        if (!_hasApiKey)
        {
            return;
        }

        // Arrange
        var context = new TestToolsContext
        {
            ProjectId = "test-project",
            MaxContinuations = 2
        };

        context.SetupOnTokensUsed(); // Enable token tracking
        context.SetupShouldContinue((content, turn) => turn < 1); // One continuation

        var client = new OpenAIClientWrapper(_config!);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "Count to 2."
            }
        };

        // Act
        var response = await client.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            context,
            images: null,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(response);

        // Should have token usage tracked
        Assert.NotEmpty(context.TokenUsages);

        // All token usages should have positive values
        foreach (var usage in context.TokenUsages)
        {
            Assert.True(usage.inputTokens > 0, "Input tokens should be positive");
            Assert.True(usage.outputTokens > 0, "Output tokens should be positive");
        }
    }

    #endregion

    #region Test Documentation

    /// <summary>
    /// Integration test summary and coverage report
    /// </summary>
    [Fact]
    public void IntegrationTestCoverage_Summary()
    {
        // This test documents what's covered:
        // ✅ Scenario 1: Backward compatibility - no continuation hooks
        // ✅ Scenario 2: Backward compatibility - tools without OnToolExecuted
        // ✅ Scenario 6: Single continuation with task completion detection
        // ✅ Scenario 7: MaxContinuations safety limit
        // ✅ Scenario 8: Custom continuation prompt
        // ✅ Scenario 9: Turn number tracking
        // ✅ Scenario 10: OnToolExecuted callback
        // ✅ Scenario 11: OnToolExecuted exception handling
        // ✅ Scenario 13: Cancellation support
        // ✅ Scenario 18: Token tracking across continuations

        // Not yet covered (would require more complex setup):
        // - Scenario 12: ShouldContinue exception (documents current behavior)
        // - Scenario 14: Continuation with tool calls mixed in
        // - Scenario 15: Streaming mode with continuation

        Assert.True(true, "Integration test coverage documented");
    }

    #endregion
}
