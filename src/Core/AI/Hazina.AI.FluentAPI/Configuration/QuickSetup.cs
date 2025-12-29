using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Hazina.AI.FluentAPI.Core;
using Hazina.LLMs.Anthropic;

namespace Hazina.AI.FluentAPI.Configuration;

/// <summary>
/// Quick setup helpers for common scenarios
/// </summary>
public static class QuickSetup
{
    /// <summary>
    /// Setup for single OpenAI provider
    /// </summary>
    public static IProviderOrchestrator SetupOpenAI(string apiKey, string model = "gpt-4o-mini")
    {
        var orchestrator = new ProviderOrchestrator();

        var config = new OpenAIConfig
        {
            ApiKey = apiKey,
            Model = model
        };

        var client = new OpenAIClientWrapper(config);

        orchestrator.RegisterProvider("openai", client, new ProviderMetadata
        {
            Name = "openai",
            DisplayName = $"OpenAI {model}",
            Type = ProviderType.OpenAI,
            Priority = 1,
            Capabilities = new ProviderCapabilities
            {
                SupportsChat = true,
                SupportsStreaming = true,
                SupportsEmbeddings = true,
                SupportsImages = true,
                MaxTokens = 128000
            },
            Pricing = model.Contains("gpt-4o-mini")
                ? new ProviderPricing
                {
                    InputCostPer1KTokens = 0.00015m,
                    OutputCostPer1KTokens = 0.0006m
                }
                : new ProviderPricing
                {
                    InputCostPer1KTokens = 0.0025m,
                    OutputCostPer1KTokens = 0.01m
                }
        });

        orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

        return orchestrator;
    }

    /// <summary>
    /// Setup with OpenAI + Anthropic failover
    /// </summary>
    public static IProviderOrchestrator SetupWithFailover(
        string openAIKey,
        string anthropicKey,
        string openAIModel = "gpt-4o-mini",
        string anthropicModel = "claude-3-5-sonnet-20241022")
    {
        var orchestrator = new ProviderOrchestrator();

        // Register OpenAI (priority 1)
        var openaiConfig = new OpenAIConfig
        {
            ApiKey = openAIKey,
            Model = openAIModel
        };
        var openaiClient = new OpenAIClientWrapper(openaiConfig);

        orchestrator.RegisterProvider("openai", openaiClient, new ProviderMetadata
        {
            Name = "openai",
            DisplayName = $"OpenAI {openAIModel}",
            Type = ProviderType.OpenAI,
            Priority = 1,
            Capabilities = new ProviderCapabilities
            {
                SupportsChat = true,
                SupportsStreaming = true,
                MaxTokens = 128000
            },
            Pricing = new ProviderPricing
            {
                InputCostPer1KTokens = 0.00015m,
                OutputCostPer1KTokens = 0.0006m
            }
        });

        // Register Anthropic (priority 2 - fallback)
        var claudeConfig = new AnthropicConfig
        {
            ApiKey = anthropicKey,
            Model = anthropicModel
        };
        var claudeClient = new ClaudeClientWrapper(claudeConfig);

        orchestrator.RegisterProvider("anthropic", claudeClient, new ProviderMetadata
        {
            Name = "anthropic",
            DisplayName = $"Claude {anthropicModel}",
            Type = ProviderType.Anthropic,
            Priority = 2,
            Capabilities = new ProviderCapabilities
            {
                SupportsChat = true,
                SupportsStreaming = true,
                MaxContextWindow = 200000
            },
            Pricing = new ProviderPricing
            {
                InputCostPer1KTokens = 0.003m,
                OutputCostPer1KTokens = 0.015m
            }
        });

        orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

        return orchestrator;
    }

    /// <summary>
    /// Setup cost-optimized (cheapest providers)
    /// </summary>
    public static IProviderOrchestrator SetupCostOptimized(
        string openAIKey,
        string? anthropicKey = null)
    {
        var orchestrator = new ProviderOrchestrator();

        // GPT-4o-mini (cheapest)
        var gpt4oMiniConfig = new OpenAIConfig
        {
            ApiKey = openAIKey,
            Model = "gpt-4o-mini"
        };
        var gpt4oMiniClient = new OpenAIClientWrapper(gpt4oMiniConfig);

        orchestrator.RegisterProvider("gpt-4o-mini", gpt4oMiniClient, new ProviderMetadata
        {
            Name = "gpt-4o-mini",
            DisplayName = "OpenAI GPT-4o Mini",
            Type = ProviderType.OpenAI,
            Priority = 1,
            Capabilities = new ProviderCapabilities
            {
                SupportsChat = true,
                SupportsStreaming = true,
                MaxTokens = 128000
            },
            Pricing = new ProviderPricing
            {
                InputCostPer1KTokens = 0.00015m,
                OutputCostPer1KTokens = 0.0006m
            }
        });

        // Claude Haiku if key provided
        if (!string.IsNullOrEmpty(anthropicKey))
        {
            var haikuConfig = new AnthropicConfig
            {
                ApiKey = anthropicKey,
                Model = "claude-3-5-haiku-20241022"
            };
            var haikuClient = new ClaudeClientWrapper(haikuConfig);

            orchestrator.RegisterProvider("claude-haiku", haikuClient, new ProviderMetadata
            {
                Name = "claude-haiku",
                DisplayName = "Claude 3.5 Haiku",
                Type = ProviderType.Anthropic,
                Priority = 2,
                Capabilities = new ProviderCapabilities
                {
                    SupportsChat = true,
                    SupportsStreaming = true
                },
                Pricing = new ProviderPricing
                {
                    InputCostPer1KTokens = 0.0008m,
                    OutputCostPer1KTokens = 0.004m
                }
            });
        }

        orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

        return orchestrator;
    }

    /// <summary>
    /// Setup and configure as default for Hazina.AI()
    /// </summary>
    public static IProviderOrchestrator SetupAndConfigure(
        string openAIKey,
        string? anthropicKey = null,
        bool withFailover = true)
    {
        var orchestrator = withFailover && !string.IsNullOrEmpty(anthropicKey)
            ? SetupWithFailover(openAIKey, anthropicKey)
            : SetupOpenAI(openAIKey);

        Core.Hazina.ConfigureDefaultOrchestrator(orchestrator);

        return orchestrator;
    }
}
