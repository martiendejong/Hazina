# Tutorial 1: Basic Response Validation

**Time to Complete:** 15 minutes
**Difficulty:** Beginner
**Prerequisites:** Basic C# knowledge, Hazina.AI.Providers setup

## What You'll Learn

- How to validate LLM responses
- How to detect common errors
- How to auto-correct invalid responses
- How to implement custom validation rules

## The Problem

LLMs make mistakes:

```csharp
// LLM asked for JSON, returns markdown
var response = "```json\n{\"name\": \"John\"}\n```";  // Invalid JSON!

// LLM hedges when you need confidence
var response = "I think maybe it could possibly be Paris?";  // Too uncertain!

// LLM returns incomplete data
var response = "{\"name\": \"John\"}";  // Missing required "age" field!
```

## The Solution

Use `BasicResponseValidator` to catch and fix these issues.

## Step 1: Create Validation Context

```csharp
using Hazina.AI.FaultDetection.Core;
using Hazina.AI.FaultDetection.Validators;

var validationContext = new ValidationContext
{
    Prompt = "Return user data as JSON",
    ResponseType = ResponseType.Json,  // Expect JSON
    MinConfidenceThreshold = 0.7
};
```

## Step 2: Create Validator

```csharp
var validator = new BasicResponseValidator();
```

## Step 3: Validate a Response

```csharp
// Simulate LLM response with JSON error
var response = "```json\n{\"name\": \"John\", \"age\": 30}\n```";

var result = await validator.ValidateAsync(
    response,
    validationContext,
    CancellationToken.None
);

if (result.IsValid)
{
    Console.WriteLine("✅ Response is valid");
}
else
{
    Console.WriteLine($"❌ Validation failed:");
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"  - {issue.Description} ({issue.Severity})");
    }
}
```

### Output

```
❌ Validation failed:
  - Invalid JSON: Unexpected character encountered (Error)
```

## Step 4: Auto-Correct the Response

```csharp
// Use ValidateAndCorrectAsync to automatically fix common issues
var result = await validator.ValidateAndCorrectAsync(
    response,
    validationContext,
    CancellationToken.None
);

if (result.CorrectedResponse != null)
{
    Console.WriteLine($"✅ Auto-corrected:");
    Console.WriteLine($"   Original: {response}");
    Console.WriteLine($"   Corrected: {result.CorrectedResponse}");
}
```

### Output

```
✅ Auto-corrected:
   Original: ```json
{"name": "John", "age": 30}
```
   Corrected: {"name": "John", "age": 30}
```

The validator automatically:
1. Detected markdown code blocks
2. Extracted JSON from inside
3. Validated the extracted JSON

## Step 5: Add Custom Validation Rules

```csharp
var validationContext = new ValidationContext
{
    Prompt = "Return user age between 0 and 120",
    ResponseType = ResponseType.Text,
    Rules = new List<ValidationRule>
    {
        new ValidationRule
        {
            Name = "ValidAge",
            Description = "Age must be between 0 and 120",
            Validator = async (response) =>
            {
                if (int.TryParse(response.Trim(), out int age))
                {
                    return age >= 0 && age <= 120;
                }
                return false;
            },
            SeverityIfFailed = IssueSeverity.Error
        }
    }
};

// Test with invalid age
var response = "150";  // Too old!

var result = await validator.ValidateAsync(response, validationContext);

// Output:
// ❌ Rule 'ValidAge' failed: Age must be between 0 and 120 (Error)
```

## Step 6: Validate Against Ground Truth

```csharp
var validationContext = new ValidationContext
{
    Prompt = "What is the capital of France?",
    GroundTruth = new Dictionary<string, string>
    {
        { "capital", "Paris" }
    }
};

// Test correct response
var response1 = "The capital of France is Paris.";
var result1 = await validator.ValidateAsync(response1, validationContext);
Console.WriteLine($"Valid: {result1.IsValid}");  // ✅ True

// Test incorrect response
var response2 = "The capital of France is Lyon.";  // Hallucination!
var result2 = await validator.ValidateAsync(response2, validationContext);
Console.WriteLine($"Valid: {result2.IsValid}");  // ❌ False
Console.WriteLine($"Issue: {result2.Issues[0].Description}");
// Output: Response missing expected value for 'capital': 'Paris'
```

## Step 7: Validate Different Response Types

### JSON Validation

```csharp
var jsonContext = new ValidationContext
{
    ResponseType = ResponseType.Json
};

var invalidJson = "{name: 'John'}";  // Missing quotes!
var result = await validator.ValidateAsync(invalidJson, jsonContext);
// ❌ Invalid JSON: Expected property name enclosed in double quotes
```

### XML Validation

```csharp
var xmlContext = new ValidationContext
{
    ResponseType = ResponseType.Xml
};

var invalidXml = "<user><name>John</user>";  // Unclosed tag!
var result = await validator.ValidateAsync(invalidXml, xmlContext);
// ❌ Invalid XML: Expected end tag
```

### Code Validation

```csharp
var codeContext = new ValidationContext
{
    ResponseType = ResponseType.Code
};

var code = @"
function add(a, b) {
    return a + b;
}
";

var result = await validator.ValidateAsync(code, codeContext);
// ✅ Valid (contains code indicators: function, {, }, return)
```

## Step 8: Confidence Scoring

```csharp
var response1 = "I think maybe it could be Paris.";  // Low confidence
var result1 = await validator.ValidateAsync(response1, validationContext);
Console.WriteLine($"Confidence: {result1.ConfidenceScore:F2}");
// Output: Confidence: 0.60 (lowered due to hedging)

var response2 = "The capital of France is Paris.";  // High confidence
var result2 = await validator.ValidateAsync(response2, validationContext);
Console.WriteLine($"Confidence: {result2.ConfidenceScore:F2}");
// Output: Confidence: 1.00
```

## Step 9: Understand Validation Issues

```csharp
var result = await validator.ValidateAsync(response, validationContext);

foreach (var issue in result.Issues)
{
    Console.WriteLine($"Category: {issue.Category}");
    Console.WriteLine($"Severity: {issue.Severity}");
    Console.WriteLine($"Description: {issue.Description}");

    if (issue.SuggestedFix != null)
    {
        Console.WriteLine($"Suggested Fix: {issue.SuggestedFix}");
    }
}
```

### Issue Categories

- `General` - Generic issues
- `Hallucination` - Fabricated facts
- `LogicalInconsistency` - Contradictions
- `FormatError` - Invalid format
- `MissingInformation` - Incomplete response
- `Contradiction` - Conflicts with earlier statements
- `FactualError` - Wrong facts
- `SyntaxError` - Syntax problems
- `TypeMismatch` - Type errors

### Issue Severities

- `Info` - Informational, non-blocking
- `Warning` - Minor issue, may proceed
- `Error` - Significant issue, should fix
- `Critical` - Blocking issue, must fix

## Complete Example

Here's a complete validation workflow:

```csharp
using Hazina.AI.FaultDetection.Core;
using Hazina.AI.FaultDetection.Validators;

// Create validator
var validator = new BasicResponseValidator();

// Create context with multiple checks
var validationContext = new ValidationContext
{
    Prompt = "Return user data as JSON",
    ResponseType = ResponseType.Json,
    GroundTruth = new Dictionary<string, string>
    {
        { "name", "John" },
        { "age", "30" }
    },
    Rules = new List<ValidationRule>
    {
        new ValidationRule
        {
            Name = "HasName",
            Description = "Must contain 'name' field",
            Validator = async (response) => response.Contains("name"),
            SeverityIfFailed = IssueSeverity.Critical
        },
        new ValidationRule
        {
            Name = "HasAge",
            Description = "Must contain 'age' field",
            Validator = async (response) => response.Contains("age"),
            SeverityIfFailed = IssueSeverity.Critical
        }
    },
    MinConfidenceThreshold = 0.7
};

// Simulate LLM response
var response = @"```json
{
    ""name"": ""John"",
    ""age"": 30
}
```";

// Validate and correct
var result = await validator.ValidateAndCorrectAsync(
    response,
    validationContext,
    CancellationToken.None
);

Console.WriteLine($"Valid: {result.IsValid}");
Console.WriteLine($"Confidence: {result.ConfidenceScore:F2}");

if (result.CorrectedResponse != null)
{
    Console.WriteLine($"Corrected Response:\n{result.CorrectedResponse}");
}

if (result.HasIssues)
{
    Console.WriteLine("\nIssues:");
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"  [{issue.Severity}] {issue.Description}");
    }
}
```

### Output

```
Valid: True
Confidence: 0.95
Corrected Response:
{
    "name": "John",
    "age": 30
}
```

## Best Practices

### 1. Always Specify ResponseType

```csharp
// ❌ Bad: Generic validation
validationContext.ResponseType = ResponseType.Text;

// ✅ Good: Specific format validation
validationContext.ResponseType = ResponseType.Json;  // Enables JSON validation
```

### 2. Use Ground Truth When Available

```csharp
// ✅ Good: Validate against known facts
validationContext.GroundTruth = new Dictionary<string, string>
{
    { "capital", "Paris" }
};
```

### 3. Add Domain-Specific Rules

```csharp
// ✅ Good: Custom business logic
validationContext.Rules.Add(new ValidationRule
{
    Name = "ValidEmail",
    Validator = async (response) => new Regex(@"^[^@]+@[^@]+$").IsMatch(response)
});
```

### 4. Set Appropriate Confidence Thresholds

```csharp
// High-stakes: Require high confidence
validationContext.MinConfidenceThreshold = 0.9;

// Normal: Balanced
validationContext.MinConfidenceThreshold = 0.7;

// Exploratory: Allow uncertainty
validationContext.MinConfidenceThreshold = 0.5;
```

## Troubleshooting

### Issue: Valid JSON marked as invalid

```csharp
// Check if JSON is wrapped in markdown
var result = await validator.ValidateAndCorrectAsync(response, context);
// Auto-corrects markdown-wrapped JSON
```

### Issue: Too many false positives

```csharp
// Lower confidence threshold
validationContext.MinConfidenceThreshold = 0.6;
```

### Issue: Missing validation

```csharp
// Add specific rule
validationContext.Rules.Add(new ValidationRule
{
    Name = "MyCheck",
    Validator = async (r) => /* your logic */
});
```

## Next Steps

- [Tutorial 2: Hallucination Detection](./02-hallucination-detection.md) - Detect fake facts
- [Tutorial 3: Custom Validation Rules](./03-custom-rules.md) - Advanced rules
- [Tutorial 4: Error Pattern Learning](./04-pattern-learning.md) - Learn from mistakes

## Key Takeaways

✅ **BasicResponseValidator** catches format errors automatically
✅ **Custom rules** add domain-specific validation
✅ **Ground truth** validates factual accuracy
✅ **Auto-correction** fixes common formatting issues
✅ **Confidence scoring** identifies uncertain responses

You now have reliable LLM response validation!
