using Hazina.AI.FaultDetection.Analyzers;
using Hazina.AI.FaultDetection.Detectors;
using Hazina.AI.Providers.Core;

namespace Hazina.AI.FaultDetection.Core;

/// <summary>
/// Adaptive fault handler that automatically detects and corrects LLM errors
/// </summary>
public class AdaptiveFaultHandler
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly IResponseValidator _validator;
    private readonly IHallucinationDetector _hallucinationDetector;
    private readonly IErrorPatternRecognizer _errorPatternRecognizer;
    private readonly IConfidenceScorer _confidenceScorer;

    private readonly int _maxRetries;
    private readonly double _minConfidenceThreshold;

    public AdaptiveFaultHandler(
        IProviderOrchestrator orchestrator,
        IResponseValidator validator,
        IHallucinationDetector hallucinationDetector,
        IErrorPatternRecognizer errorPatternRecognizer,
        IConfidenceScorer confidenceScorer,
        int maxRetries = 3,
        double minConfidenceThreshold = 0.7)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _hallucinationDetector = hallucinationDetector ?? throw new ArgumentNullException(nameof(hallucinationDetector));
        _errorPatternRecognizer = errorPatternRecognizer ?? throw new ArgumentNullException(nameof(errorPatternRecognizer));
        _confidenceScorer = confidenceScorer ?? throw new ArgumentNullException(nameof(confidenceScorer));

        _maxRetries = maxRetries;
        _minConfidenceThreshold = minConfidenceThreshold;
    }

    /// <summary>
    /// Execute LLM request with automatic fault detection and correction
    /// </summary>
    public async Task<LLMResponse<string>> ExecuteWithFaultDetectionAsync(
        List<HazinaChatMessage> messages,
        ValidationContext validationContext,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        List<string> failedResponses = new();
        List<ValidationResult> validationHistory = new();

        while (attempt < _maxRetries)
        {
            attempt++;

            // Get response from orchestrator
            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );

            // Validate response
            var validation = await ValidateResponseAsync(
                response.Result,
                validationContext,
                cancellationToken
            );

            validationHistory.Add(validation);

            // Check if response is valid and meets confidence threshold
            if (validation.IsValid && validation.ConfidenceScore >= _minConfidenceThreshold)
            {
                // Success!
                return response;
            }

            // Response failed validation - record it
            failedResponses.Add(response.Result);

            // If we have a corrected response, return it
            if (validation.CorrectedResponse != null)
            {
                return new LLMResponse<string>(
                    validation.CorrectedResponse,
                    response.TokenUsage
                );
            }

            // Refine prompt based on validation issues
            messages = RefinePromptBasedOnIssues(messages, validation);

            // Learn from the error pattern
            if (validation.HasIssues)
            {
                foreach (var issue in validation.Issues)
                {
                    await LearnFromErrorAsync(issue, response.Result);
                }
            }
        }

        // All retries failed - return best attempt
        var bestValidation = validationHistory.OrderByDescending(v => v.ConfidenceScore).First();
        var bestResponse = failedResponses[validationHistory.IndexOf(bestValidation)];

        return new LLMResponse<string>(
            bestValidation.CorrectedResponse ?? bestResponse,
            new TokenUsageInfo()
        );
    }

    /// <summary>
    /// Validate a response using all available detectors
    /// </summary>
    private async Task<ValidationResult> ValidateResponseAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken)
    {
        var result = new ValidationResult { IsValid = true };

        // 1. Basic validation
        var basicValidation = await _validator.ValidateAsync(response, context, cancellationToken);
        if (!basicValidation.IsValid)
        {
            result.IsValid = false;
            result.Issues.AddRange(basicValidation.Issues);
        }

        // 2. Hallucination detection
        var hallucinationResult = await _hallucinationDetector.DetectAsync(response, context, cancellationToken);
        if (hallucinationResult.ContainsHallucination)
        {
            result.IsValid = false;
            foreach (var instance in hallucinationResult.Instances)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = $"Potential hallucination: {instance.Content}",
                    Category = IssueCategory.Hallucination,
                    Severity = IssueSeverity.Critical,
                    SuggestedFix = instance.SuggestedCorrection
                });
            }
        }

        // 3. Error pattern recognition
        var errorPatternResult = await _errorPatternRecognizer.RecognizeAsync(response, context, cancellationToken);
        if (errorPatternResult.ContainsErrorPattern)
        {
            result.IsValid = false;
            foreach (var matched in errorPatternResult.MatchedPatterns)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = $"Known error pattern: {matched.Pattern.Name}",
                    Category = IssueCategory.General,
                    Severity = matched.Pattern.Severity,
                    SuggestedFix = matched.Pattern.SuggestedFix
                });
            }
        }

        // 4. Confidence scoring
        var confidenceScore = await _confidenceScorer.ScoreAsync(response, context, cancellationToken);
        result.ConfidenceScore = confidenceScore.Score;

        if (confidenceScore.IsLowConfidence)
        {
            result.Issues.Add(new ValidationIssue
            {
                Description = $"Low confidence score: {confidenceScore.Score:F2}. {confidenceScore.Reasoning}",
                Category = IssueCategory.General,
                Severity = IssueSeverity.Warning
            });
        }

        return result;
    }

    /// <summary>
    /// Refine prompt based on validation issues
    /// </summary>
    private List<HazinaChatMessage> RefinePromptBasedOnIssues(
        List<HazinaChatMessage> originalMessages,
        ValidationResult validation)
    {
        var refinedMessages = new List<HazinaChatMessage>(originalMessages);

        if (validation.HasIssues)
        {
            var issuesSummary = string.Join(", ", validation.Issues.Select(i => i.Description));
            var refinementMessage = new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = $"Previous response had issues: {issuesSummary}. Please provide a corrected response addressing these issues."
            };

            refinedMessages.Insert(refinedMessages.Count - 1, refinementMessage);
        }

        return refinedMessages;
    }

    /// <summary>
    /// Learn from error to improve future detection
    /// </summary>
    private async Task LearnFromErrorAsync(ValidationIssue issue, string response)
    {
        // Create error pattern from issue
        var pattern = new ErrorPattern
        {
            Name = $"Issue: {issue.Category}",
            Description = issue.Description,
            Severity = issue.Severity,
            Type = PatternType.Semantic,
            Pattern = response.Length > 100 ? response.Substring(0, 100) : response
        };

        await _errorPatternRecognizer.LearnPatternAsync(pattern);
    }
}
