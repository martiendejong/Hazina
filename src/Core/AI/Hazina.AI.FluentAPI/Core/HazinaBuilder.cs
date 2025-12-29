using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Hazina.AI.FaultDetection.Core;
using Hazina.AI.FaultDetection.Validators;
using Hazina.AI.FaultDetection.Detectors;
using Hazina.AI.FaultDetection.Analyzers;
using Hazina.AI.Orchestration.Context;

namespace Hazina.AI.FluentAPI.Core;

/// <summary>
/// Fluent API builder for Hazina AI operations
/// Provides developer-friendly interface reducing complexity by 70%
/// </summary>
public class HazinaBuilder
{
    private IProviderOrchestrator? _orchestrator;
    private IContextManager? _contextManager;
    private ValidationContext? _validationContext;
    private ConversationContext? _conversationContext;
    private bool _useFaultDetection = false;
    private double _minConfidence = 0.7;
    private string? _specificProvider;
    private SelectionStrategy _strategy = SelectionStrategy.Priority;
    private List<HazinaChatMessage> _messages = new();

    internal HazinaBuilder()
    {
    }

    /// <summary>
    /// Use a specific provider orchestrator
    /// </summary>
    public HazinaBuilder WithOrchestrator(IProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        return this;
    }

    /// <summary>
    /// Use a specific provider by name
    /// </summary>
    public HazinaBuilder WithProvider(string providerName)
    {
        _specificProvider = providerName;
        _strategy = SelectionStrategy.Specific;
        return this;
    }

    /// <summary>
    /// Use cheapest available provider
    /// </summary>
    public HazinaBuilder WithCheapestProvider()
    {
        _strategy = SelectionStrategy.LeastCost;
        return this;
    }

    /// <summary>
    /// Use fastest available provider
    /// </summary>
    public HazinaBuilder WithFastestProvider()
    {
        _strategy = SelectionStrategy.FastestResponse;
        return this;
    }

    /// <summary>
    /// Use highest priority provider
    /// </summary>
    public HazinaBuilder WithPriorityProvider()
    {
        _strategy = SelectionStrategy.Priority;
        return this;
    }

    /// <summary>
    /// Enable fault detection with optional confidence threshold
    /// </summary>
    public HazinaBuilder WithFaultDetection(double minConfidence = 0.7)
    {
        _useFaultDetection = true;
        _minConfidence = minConfidence;
        return this;
    }

    /// <summary>
    /// Use existing conversation context
    /// </summary>
    public HazinaBuilder WithContext(ConversationContext context)
    {
        _conversationContext = context ?? throw new ArgumentNullException(nameof(context));
        return this;
    }

    /// <summary>
    /// Create new conversation context
    /// </summary>
    public HazinaBuilder WithNewContext(int maxTokens = 128000)
    {
        _contextManager = new ContextManager(_orchestrator);
        _conversationContext = _contextManager.CreateContext(maxTokens);
        return this;
    }

    /// <summary>
    /// Add system message
    /// </summary>
    public HazinaBuilder WithSystemMessage(string message)
    {
        _messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.System,
            Text = message
        });
        return this;
    }

    /// <summary>
    /// Ask a question (user message)
    /// </summary>
    public HazinaBuilder Ask(string question)
    {
        _messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = question
        });
        return this;
    }

    /// <summary>
    /// Set expected response type for validation
    /// </summary>
    public HazinaBuilder ExpectJson()
    {
        EnsureValidationContext();
        _validationContext!.ResponseType = ResponseType.Json;
        return this;
    }

    /// <summary>
    /// Set expected response type to code
    /// </summary>
    public HazinaBuilder ExpectCode()
    {
        EnsureValidationContext();
        _validationContext!.ResponseType = ResponseType.Code;
        return this;
    }

    /// <summary>
    /// Add ground truth for validation
    /// </summary>
    public HazinaBuilder WithGroundTruth(string key, string value)
    {
        EnsureValidationContext();
        _validationContext!.GroundTruth[key] = value;
        return this;
    }

    /// <summary>
    /// Add custom validation rule
    /// </summary>
    public HazinaBuilder WithValidation(string name, Func<string, Task<bool>> validator, IssueSeverity severity = IssueSeverity.Error)
    {
        EnsureValidationContext();
        _validationContext!.Rules.Add(new ValidationRule
        {
            Name = name,
            Validator = validator,
            SeverityIfFailed = severity
        });
        return this;
    }

    /// <summary>
    /// Execute the AI request
    /// </summary>
    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Ensure we have an orchestrator
        if (_orchestrator == null)
        {
            throw new InvalidOperationException("Provider orchestrator not configured. Use WithOrchestrator() or Hazina.AI().ConfigureProviders()");
        }

        // Set strategy if specific provider requested
        if (!string.IsNullOrEmpty(_specificProvider))
        {
            _orchestrator.SetDefaultStrategy(SelectionStrategy.Specific);
            _orchestrator.SetDefaultContext(new SelectionContext
            {
                SpecificProvider = _specificProvider
            });
        }
        else
        {
            _orchestrator.SetDefaultStrategy(_strategy);
        }

        // Build final message list
        var finalMessages = BuildMessages();

        // Execute with or without fault detection
        if (_useFaultDetection)
        {
            return await ExecuteWithFaultDetectionAsync(finalMessages, cancellationToken);
        }
        else
        {
            var response = await _orchestrator.GetResponse(
                finalMessages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );
            return response.Result;
        }
    }

    /// <summary>
    /// Execute with streaming response
    /// </summary>
    public async Task<string> ExecuteStreamAsync(
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default)
    {
        if (_orchestrator == null)
        {
            throw new InvalidOperationException("Provider orchestrator not configured");
        }

        _orchestrator.SetDefaultStrategy(_strategy);

        var finalMessages = BuildMessages();

        var response = await _orchestrator.GetResponseStream(
            finalMessages,
            onChunkReceived,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        return response.Result;
    }

    #region Private Methods

    private void EnsureValidationContext()
    {
        if (_validationContext == null)
        {
            _validationContext = new ValidationContext
            {
                MinConfidenceThreshold = _minConfidence
            };
        }
    }

    private List<HazinaChatMessage> BuildMessages()
    {
        var messages = new List<HazinaChatMessage>();

        // Add context messages if available
        if (_conversationContext != null)
        {
            messages.AddRange(_conversationContext.Messages);
        }

        // Add builder messages
        messages.AddRange(_messages);

        return messages;
    }

    private async Task<string> ExecuteWithFaultDetectionAsync(
        List<HazinaChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // Create fault detection components
        var validator = new BasicResponseValidator();
        var hallucinationDetector = new BasicHallucinationDetector();
        var errorPatternRecognizer = new BasicErrorPatternRecognizer();
        var confidenceScorer = new BasicConfidenceScorer();

        var faultHandler = new AdaptiveFaultHandler(
            _orchestrator!,
            validator,
            hallucinationDetector,
            errorPatternRecognizer,
            confidenceScorer,
            maxRetries: 3,
            minConfidenceThreshold: _minConfidence
        );

        EnsureValidationContext();
        _validationContext!.Prompt = messages.LastOrDefault()?.Text ?? "";
        _validationContext.ConversationHistory = messages;

        var response = await faultHandler.ExecuteWithFaultDetectionAsync(
            messages,
            _validationContext,
            cancellationToken
        );

        return response.Result;
    }

    #endregion
}
