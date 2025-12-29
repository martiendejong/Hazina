# Hazina.AI.FaultDetection

**Adaptive Fault Detection and Self-Correction for LLM Responses**

Hazina.AI.FaultDetection automatically detects and corrects LLM errors, hallucinations, and inconsistencies in real-time. Get production-grade reliability from probabilistic language models.

## Table of Contents

- [Features](#features)
- [Why Use Fault Detection?](#why-use-fault-detection)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Tutorials](#tutorials)
- [Detection Capabilities](#detection-capabilities)
- [Examples](#examples)

---

## Features

### 🔍 **Hallucination Detection**
- Detects 7 types of hallucinations
- Cross-references with conversation history
- Identifies unsupported claims
- Catches fabricated details

### 🛡️ **Error Pattern Recognition**
- Learns from past errors
- Pattern database with 6+ built-in patterns
- Automatic pattern learning
- Regex and semantic matching

### 📊 **Confidence Scoring**
- Multi-factor confidence analysis
- Hedging language detection
- Specificity scoring
- Context consistency checking

### ✅ **Response Validation**
- JSON/XML/Code format validation
- Ground truth checking
- Custom validation rules
- Automatic format correction

### 🔄 **Self-Correction**
- Automatic retry with refined prompts
- Progressive error learning
- Confidence-based fallback
- Smart prompt refinement

---

## Why Use Fault Detection?

### The Problem

LLMs are probabilistic and make mistakes:

```csharp
// Traditional approach
var response = await llm.GetResponse(messages);
// Response might contain:
// ❌ Hallucinations (made-up facts)
// ❌ Contradictions (conflicts with earlier answers)
// ❌ Format errors (invalid JSON, broken code)
// ❌ Low confidence responses (hedging language)
```

### The Solution

Hazina.AI.FaultDetection catches these issues automatically:

```csharp
// With Fault Detection
var handler = new AdaptiveFaultHandler(
    orchestrator,
    validator,
    hallucinationDetector,
    errorPatternRecognizer,
    confidenceScorer
);

var validationContext = new ValidationContext
{
    Prompt = "What is the capital of France?",
    ResponseType = ResponseType.Text,
    MinConfidenceThreshold = 0.7
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext
);
// ✅ Hallucinations detected and corrected
// ✅ Format errors fixed automatically
// ✅ Low confidence responses retried
// ✅ Pattern errors learned for future prevention
```

---

## Quick Start

### 1. Create Validation Context

```csharp
using Hazina.AI.FaultDetection.Core;
using Hazina.AI.FaultDetection.Validators;
using Hazina.AI.FaultDetection.Detectors;
using Hazina.AI.FaultDetection.Analyzers;

var validationContext = new ValidationContext
{
    Prompt = "What is 2+2?",
    ResponseType = ResponseType.Text,
    MinConfidenceThreshold = 0.7,
    GroundTruth = new Dictionary<string, string>
    {
        { "answer", "4" }
    }
};
```

### 2. Create Detectors and Validators

```csharp
// Create validator
var validator = new BasicResponseValidator();

// Create hallucination detector
var hallucinationDetector = new BasicHallucinationDetector();

// Create error pattern recognizer
var errorPatternRecognizer = new BasicErrorPatternRecognizer();

// Create confidence scorer
var confidenceScorer = new BasicConfidenceScorer();
```

### 3. Create Adaptive Fault Handler

```csharp
var handler = new AdaptiveFaultHandler(
    orchestrator,              // IProviderOrchestrator
    validator,                 // IResponseValidator
    hallucinationDetector,     // IHallucinationDetector
    errorPatternRecognizer,    // IErrorPatternRecognizer
    confidenceScorer,          // IConfidenceScorer
    maxRetries: 3,
    minConfidenceThreshold: 0.7
);
```

### 4. Execute with Fault Detection

```csharp
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "What is 2+2?" }
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext,
    CancellationToken.None
);

Console.WriteLine($"Response: {response.Result}");
// If first response was invalid, automatically retried with corrections
```

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│            AdaptiveFaultHandler                      │
│         (Self-Correcting Orchestrator)               │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────┐│
│  │  Response    │  │Hallucination │  │  Error     ││
│  │  Validator   │  │  Detector    │  │  Pattern   ││
│  │              │  │              │  │Recognizer  ││
│  └──────────────┘  └──────────────┘  └────────────┘│
│                                                       │
│  ┌──────────────┐  ┌──────────────┐                │
│  │  Confidence  │  │   Pattern    │                │
│  │   Scorer     │  │   Learning   │                │
│  └──────────────┘  └──────────────┘                │
│                                                       │
└──────────────────────────────────────────────────────┘
                       │
                       ▼
              ┌─────────────────┐
              │      LLM        │
              │   (via Provider │
              │   Orchestrator) │
              └─────────────────┘
```

---

## Detection Capabilities

### 1. Hallucination Types

| Type | Description | Example |
|------|-------------|---------|
| **FabricatedFact** | Made-up information | "The Eiffel Tower was built in 1889" (correct) vs "1895" (fabricated) |
| **Contradiction** | Conflicts with earlier statements | First: "Paris is the capital" Later: "No it's not" |
| **ContextMismatch** | Doesn't match prompt | Prompt: "What is 2+2?" Response: "Paris is beautiful" |
| **UnsupportedClaim** | Claims without basis | "It is definitely proven that..." (with no source) |
| **AttributionError** | Wrong source attribution | "Einstein said X" (but he didn't) |
| **TemporalError** | Wrong dates/timing | "In 2030, World War II ended" |
| **QuantitativeError** | Wrong numbers | "Earth has 12 continents" |

### 2. Error Patterns

Built-in patterns:
- **EmptyResponse**: Response is empty or whitespace
- **ApologyPattern**: "I apologize...", "I'm sorry..."
- **CannotDoPattern**: "I cannot...", "I'm unable to..."
- **NoInformationPattern**: "I don't have information..."
- **JSONParseError**: Invalid JSON structure
- **ExceptionMention**: Response mentions errors/exceptions

### 3. Confidence Factors

Multi-factor scoring based on:
- **Length**: Too short or too long responses
- **Hedging**: Uncertain language ("maybe", "perhaps", "I think")
- **Specificity**: Level of detail (numbers, dates, proper nouns)
- **Consistency**: Keyword overlap with prompt
- **Format**: Compliance with expected format

### 4. Validation Rules

- **Format Validation**: JSON, XML, Code syntax checking
- **Ground Truth**: Validation against known facts
- **Custom Rules**: Your own validation logic
- **Error Indicators**: Common error phrases

---

## Tutorials

- [Tutorial 1: Basic Validation](./docs/tutorials/01-basic-validation.md)
- [Tutorial 2: Hallucination Detection](./docs/tutorials/02-hallucination-detection.md)
- [Tutorial 3: Custom Validation Rules](./docs/tutorials/03-custom-rules.md)
- [Tutorial 4: Error Pattern Learning](./docs/tutorials/04-pattern-learning.md)
- [Tutorial 5: Production Integration](./docs/tutorials/05-production.md)

---

## Examples

### Example 1: Detect JSON Errors

```csharp
var validationContext = new ValidationContext
{
    Prompt = "Return user data as JSON",
    ResponseType = ResponseType.Json
};

var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Get user data for ID 123 as JSON" }
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext
);

// If LLM returns invalid JSON like:
// ```json
// {"name": "John", "age": 30,}  // Trailing comma!
// ```

// Fault handler:
// 1. Detects JSON error
// 2. Attempts auto-correction (removes trailing comma)
// 3. OR retries with refined prompt: "Return VALID JSON for user 123"
```

### Example 2: Catch Hallucinations

```csharp
var validationContext = new ValidationContext
{
    Prompt = "What is the capital of France?",
    GroundTruth = new Dictionary<string, string>
    {
        { "capital", "Paris" }
    },
    MinConfidenceThreshold = 0.8
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext
);

// If LLM hallucinates: "The capital of France is Lyon"
// Fault handler:
// 1. Detects contradiction with ground truth
// 2. Scores low confidence
// 3. Retries with refined prompt: "IMPORTANT: Be accurate. What is the capital of France?"
```

### Example 3: Custom Validation Rules

```csharp
var validationContext = new ValidationContext
{
    Prompt = "Generate a valid email address",
    Rules = new List<ValidationRule>
    {
        new ValidationRule
        {
            Name = "ValidEmail",
            Description = "Must be valid email format",
            Validator = async (response) =>
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return emailRegex.IsMatch(response.Trim());
            },
            SeverityIfFailed = IssueSeverity.Critical
        },
        new ValidationRule
        {
            Name = "NoSpaces",
            Description = "Email should not contain spaces",
            Validator = async (response) =>
            {
                return !response.Contains(" ");
            },
            SeverityIfFailed = IssueSeverity.Error
        }
    }
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext
);

// If LLM returns: "john doe @example.com"
// Fault handler:
// 1. Detects space (violates "NoSpaces" rule)
// 2. Detects invalid format (violates "ValidEmail" rule)
// 3. Retries with refined prompt
```

### Example 4: Confidence-Based Retry

```csharp
var validationContext = new ValidationContext
{
    MinConfidenceThreshold = 0.8  // High confidence required
};

var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Is this claim true: XYZ?" }
};

var response = await handler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext
);

// If LLM hedges: "I think maybe it could be true perhaps..."
// Confidence scorer detects:
// - Multiple hedging words ("think", "maybe", "could", "perhaps")
// - Low specificity
// - Confidence score: 0.3 (below 0.8 threshold)
//
// Fault handler retries with: "Answer with HIGH CONFIDENCE: Is XYZ true?"
```

### Example 5: Pattern Learning

```csharp
var errorRecognizer = new BasicErrorPatternRecognizer();

// System learns from errors
for (int i = 0; i < 100; i++)
{
    var response = await handler.ExecuteWithFaultDetectionAsync(
        messages,
        validationContext
    );

    // If error occurs, pattern is automatically learned
}

// Check learned patterns
var patterns = errorRecognizer.GetKnownPatterns();
foreach (var pattern in patterns.OrderByDescending(p => p.OccurrenceCount))
{
    Console.WriteLine($"{pattern.Name}: {pattern.OccurrenceCount} occurrences");
}

// Output:
// ApologyPattern: 15 occurrences
// CannotDoPattern: 8 occurrences
// JSONParseError: 5 occurrences
// CustomPattern_XYZ: 3 occurrences  // Learned during execution
```

---

## Best Practices

### 1. Set Appropriate Confidence Thresholds

```csharp
// ❌ Too strict: Many false positives
validationContext.MinConfidenceThreshold = 0.95;

// ✅ Balanced: Catches real issues
validationContext.MinConfidenceThreshold = 0.7;

// ❌ Too lenient: Misses issues
validationContext.MinConfidenceThreshold = 0.3;
```

### 2. Provide Ground Truth When Available

```csharp
// ✅ Good: Validate against known facts
var validationContext = new ValidationContext
{
    GroundTruth = new Dictionary<string, string>
    {
        { "capital", "Paris" },
        { "country", "France" }
    }
};
```

### 3. Use Format-Specific Validation

```csharp
// ✅ Good: Specify expected format
validationContext.ResponseType = ResponseType.Json;  // Enable JSON validation
```

### 4. Add Custom Rules for Domain Logic

```csharp
// ✅ Good: Domain-specific validation
validationContext.Rules.Add(new ValidationRule
{
    Name = "ValidAge",
    Validator = async (response) =>
    {
        if (int.TryParse(response, out int age))
        {
            return age >= 0 && age <= 150;  // Reasonable age range
        }
        return false;
    }
});
```

### 5. Monitor Pattern Learning

```csharp
// ✅ Good: Periodically review learned patterns
var patterns = errorRecognizer.GetKnownPatterns();
var recentPatterns = patterns.Where(p =>
    p.LastSeen > DateTime.UtcNow.AddDays(-7));

foreach (var pattern in recentPatterns)
{
    Console.WriteLine($"New pattern: {pattern.Name} ({pattern.OccurrenceCount}x)");
}
```

---

## API Reference

### AdaptiveFaultHandler

```csharp
// Execute with fault detection and auto-correction
Task<LLMResponse<string>> ExecuteWithFaultDetectionAsync(
    List<HazinaChatMessage> messages,
    ValidationContext validationContext,
    CancellationToken cancellationToken = default
)
```

### ValidationContext

```csharp
public class ValidationContext
{
    public string Prompt { get; set; }                        // Original prompt
    public List<HazinaChatMessage> ConversationHistory { get; set; }
    public ResponseType ResponseType { get; set; }             // Text, Json, Code, etc.
    public Dictionary<string, string> GroundTruth { get; set; }
    public List<ValidationRule> Rules { get; set; }
    public double MinConfidenceThreshold { get; set; } = 0.7;
}
```

### ValidationResult

```csharp
public class ValidationResult
{
    public bool IsValid { get; set; }
    public double ConfidenceScore { get; set; }
    public List<ValidationIssue> Issues { get; set; }
    public string? CorrectedResponse { get; set; }
    public bool HasCriticalIssues { get; }
    public bool RequiresCorrection { get; }
}
```

---

## Integration with Providers

Fault Detection works seamlessly with Provider Orchestrator:

```csharp
// Create orchestrator
var orchestrator = new ProviderOrchestrator();
orchestrator.RegisterProvider("openai", openaiClient, metadata);
orchestrator.RegisterProvider("anthropic", claudeClient, metadata);

// Create fault handler with orchestrator
var handler = new AdaptiveFaultHandler(
    orchestrator,  // Uses multi-provider with failover
    validator,
    hallucinationDetector,
    errorPatternRecognizer,
    confidenceScorer
);

// Now you have:
// ✅ Multi-provider support
// ✅ Automatic failover
// ✅ Fault detection
// ✅ Self-correction
// ✅ Cost tracking
```

---

## Troubleshooting

### Issue: Too many retries

```csharp
// Lower confidence threshold
validationContext.MinConfidenceThreshold = 0.6;

// Or reduce max retries
var handler = new AdaptiveFaultHandler(
    orchestrator,
    validator,
    hallucinationDetector,
    errorPatternRecognizer,
    confidenceScorer,
    maxRetries: 2  // Reduced from 3
);
```

### Issue: False positive hallucinations

```csharp
// Provide more ground truth
validationContext.GroundTruth = new Dictionary<string, string>
{
    { "fact1", "value1" },
    { "fact2", "value2" },
    // More context = better detection
};
```

### Issue: Pattern not learning

```csharp
// Manually add pattern
await errorRecognizer.LearnPatternAsync(new ErrorPattern
{
    Name = "CustomError",
    Description = "My custom error pattern",
    Type = PatternType.Regex,
    Pattern = @"error:\s+\w+",
    Severity = IssueSeverity.Error
});
```

---

## Next Steps

- [Tutorial 1: Basic Validation](./docs/tutorials/01-basic-validation.md) - Start here
- [Tutorial 2: Hallucination Detection](./docs/tutorials/02-hallucination-detection.md) - Detect fake facts
- [Tutorial 5: Production Integration](./docs/tutorials/05-production.md) - Deploy reliably

---

## Key Takeaways

✅ **Automatic hallucination detection** - Catch fabricated facts
✅ **Self-correcting retries** - Fix errors automatically
✅ **Pattern learning** - Get smarter over time
✅ **Confidence scoring** - Know when to trust responses
✅ **Format validation** - Ensure correct output structure

Make your LLM applications production-ready with adaptive fault detection!
