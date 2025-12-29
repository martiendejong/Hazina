# Code Intelligence Guide

## Overview

Hazina's Code Intelligence provides AI-powered code analysis, refactoring, and pattern learning capabilities.

## Features

### 1. Multi-File Refactoring

Safely refactor code across multiple files with dependency analysis:

```csharp
var orchestrator = new ProviderOrchestrator();
var refactoringEngine = new MultiFileRefactoringEngine(orchestrator);

// Build project context
var context = new ProjectContext
{
    Name = "MyProject",
    Files = codeFiles,
    Symbols = symbolDictionary,
    DependencyGraph = dependencies
};

// Request refactoring
var request = new RefactoringRequest
{
    Type = RefactoringType.RenameSymbol,
    TargetSymbol = "OldClassName",
    NewName = "NewClassName",
    Scope = RefactoringScope.Project
};

// Analyze refactoring
var plan = await refactoringEngine.AnalyzeRefactoringAsync(request, context);

Console.WriteLine($"Risk Level: {plan.RiskAssessment.RiskLevel}");
Console.WriteLine($"Affected Files: {plan.AffectedFiles.Count}");
Console.WriteLine($"Breaking Changes: {plan.RiskAssessment.BreakingChanges.Count}");

// Review steps
foreach (var step in plan.Steps)
{
    Console.WriteLine($"- {step.Description}");
}

// Execute if acceptable
if (plan.RiskAssessment.RiskLevel <= RiskLevel.Medium)
{
    var result = await refactoringEngine.ExecuteRefactoringAsync(plan);
    Console.WriteLine($"Success: {result.Success}");
}
```

### 2. Logical Inconsistency Detection

Detect naming, architectural, and logical inconsistencies:

```csharp
var detector = new LogicalInconsistencyDetector(orchestrator);

// Detect all inconsistencies
var report = await detector.DetectInconsistenciesAsync(context);

Console.WriteLine($"Consistency Score: {report.ConsistencyScore:P0}");
Console.WriteLine($"Naming Issues: {report.NamingInconsistencies.Count}");
Console.WriteLine($"Architecture Issues: {report.ArchitecturalInconsistencies.Count}");

// Review issues
foreach (var issue in report.NamingInconsistencies)
{
    Console.WriteLine($"[{issue.Severity}] {issue.Description}");
    Console.WriteLine($"  Suggestion: {issue.Suggestion}");
}

// Analyze specific code
var codeIssues = await detector.AnalyzeCodeAsync(
    codeSnippet,
    "Authentication module"
);

foreach (var issue in codeIssues)
{
    Console.WriteLine($"{issue.Type}: {issue.Description}");
}
```

### 3. Project Pattern Learning

Automatically learn and enforce project conventions:

```csharp
var patternLearner = new ProjectPatternLearner(orchestrator);

// Learn patterns from codebase
var learningResult = await patternLearner.LearnPatternsAsync(context);

Console.WriteLine($"Patterns learned: {learningResult.PatternsLearned.Count}");

// Review learned patterns
foreach (var pattern in learningResult.PatternsLearned)
{
    Console.WriteLine($"{pattern.Category}: {pattern.Name}");
    Console.WriteLine($"  Description: {pattern.Description}");
    Console.WriteLine($"  Confidence: {pattern.Confidence:P0}");
    Console.WriteLine($"  Rule: {pattern.Rule}");
}

// Get suggestions for a file
var suggestions = patternLearner.SuggestImprovements(codeFile, context);

foreach (var suggestion in suggestions)
{
    Console.WriteLine($"[Priority {suggestion.Priority}] {suggestion.Pattern}");
    Console.WriteLine($"  File: {suggestion.File}:{suggestion.Location}");
    Console.WriteLine($"  Issue: {suggestion.Issue}");
    Console.WriteLine($"  Suggestion: {suggestion.Suggestion}");
}
```

## Refactoring Types

### Supported Refactorings

- **RenameSymbol**: Rename classes, methods, variables across entire project
- **ExtractMethod**: Extract code into new method
- **InlineMethod**: Inline method calls
- **MoveClass**: Move class to different namespace/file
- **ExtractInterface**: Extract interface from class
- **ChangeSignature**: Modify method signatures safely

### Refactoring Example

```csharp
// Extract method refactoring
var request = new RefactoringRequest
{
    Type = RefactoringType.ExtractMethod,
    SourceFile = "Services/UserService.cs",
    StartLine = 45,
    EndLine = 67,
    NewName = "ValidateUserCredentials",
    Parameters = new Dictionary<string, string>
    {
        ["username"] = "string",
        ["password"] = "string"
    }
};

var plan = await refactoringEngine.AnalyzeRefactoringAsync(request, context);

// Check for issues
if (plan.RiskAssessment.BreakingChanges.Any())
{
    Console.WriteLine("Warning: Breaking changes detected:");
    foreach (var breaking in plan.RiskAssessment.BreakingChanges)
    {
        Console.WriteLine($"  - {breaking}");
    }
}
```

## Pattern Categories

### Automatic Pattern Detection

The system can learn these pattern categories:

1. **Naming Conventions**
   - Class naming (PascalCase, prefixes)
   - Method naming (verbs, conventions)
   - Variable naming (camelCase, Hungarian notation)
   - Constant naming (UPPER_CASE)

2. **Architectural Patterns**
   - Layered architecture
   - Clean architecture
   - Microservices patterns
   - Repository patterns

3. **Design Patterns**
   - Factory, Singleton, Observer
   - Dependency injection patterns
   - SOLID principles adherence

4. **Coding Style**
   - Error handling patterns
   - Logging conventions
   - Comment styles
   - Code organization

### Pattern Learning Example

```csharp
// Learn from specific category
var namingPatterns = patternLearner.GetPatternsByCategory(PatternCategory.NamingConvention);

foreach (var pattern in namingPatterns)
{
    Console.WriteLine($"{pattern.Name}:");
    Console.WriteLine($"  Applies to: {pattern.Description}");
    Console.WriteLine($"  Examples:");
    foreach (var example in pattern.Examples)
    {
        Console.WriteLine($"    - {example}");
    }
}
```

## Inconsistency Types

### Detection Examples

```csharp
// Naming inconsistencies
// Detects: mix of camelCase and PascalCase, inconsistent prefixes

// Architectural inconsistencies
// Detects: circular dependencies, layer violations, wrong dependencies

// Logical inconsistencies
// Detects: unreachable code, contradictory logic, type mismatches

// Documentation inconsistencies
// Detects: outdated comments, missing documentation, comment-code mismatch
```

## Best Practices

### 1. Build Complete Context

```csharp
var context = new ProjectContext
{
    Name = projectName,
    Files = await ScanAllFilesAsync(projectPath),
    Symbols = await ExtractAllSymbolsAsync(files),
    DependencyGraph = BuildDependencyGraph(files),
    Architecture = AnalyzeArchitecture(files)
};
```

### 2. Review Before Executing

```csharp
var plan = await refactoringEngine.AnalyzeRefactoringAsync(request, context);

// Always review
Console.WriteLine($"Risk: {plan.RiskAssessment.RiskLevel}");
Console.WriteLine($"Estimated effort: {plan.EstimatedEffort}");

// Get user confirmation for high-risk changes
if (plan.RiskAssessment.RiskLevel >= RiskLevel.High)
{
    if (!GetUserConfirmation())
        return;
}

await refactoringEngine.ExecuteRefactoringAsync(plan);
```

### 3. Incremental Learning

```csharp
// Learn patterns incrementally as codebase evolves
var existingPatterns = patternLearner.GetPatterns();

// Add new files
context.Files.AddRange(newFiles);

// Re-learn
var newPatterns = await patternLearner.LearnPatternsAsync(context);

// Compare
var changedPatterns = DetectPatternChanges(existingPatterns, newPatterns);
```

### 4. Continuous Monitoring

```csharp
// Run inconsistency detection on commits
var report = await detector.DetectInconsistenciesAsync(context);

if (report.ConsistencyScore < 0.8)
{
    Console.WriteLine("Warning: Code consistency below threshold");
    // Trigger review or block merge
}
```

## Integration Examples

### Pre-commit Hook

```csharp
// Check code before commit
var changedFiles = GetChangedFiles();
var context = BuildContextFromFiles(changedFiles);

var issues = await detector.AnalyzeCodeAsync(
    string.Join("\n", changedFiles.Select(f => f.Content)),
    "Pre-commit check"
);

if (issues.Any(i => i.Severity == InconsistencySeverity.High))
{
    Console.WriteLine("High severity issues detected. Commit blocked.");
    return false;
}
```

### Code Review Assistant

```csharp
// Analyze pull request
var prFiles = GetPullRequestFiles();
var context = BuildContext(prFiles);

// Check inconsistencies
var report = await detector.DetectInconsistenciesAsync(context);

// Check patterns
var suggestions = patternLearner.SuggestImprovements(prFiles[0], context);

// Generate review comments
var comments = GenerateReviewComments(report, suggestions);
PostReviewComments(pullRequestId, comments);
```

## Performance Considerations

- **Context Building**: Cache parsed symbols and dependencies
- **Pattern Learning**: Run periodically, not on every file change
- **Incremental Analysis**: Only analyze changed files when possible
- **Parallel Processing**: Analyze files in parallel for large codebases

## Error Handling

```csharp
try
{
    var plan = await refactoringEngine.AnalyzeRefactoringAsync(request, context);

    if (!plan.IsSafe)
    {
        Console.WriteLine("Refactoring deemed unsafe:");
        foreach (var risk in plan.RiskAssessment.Risks)
        {
            Console.WriteLine($"  - {risk}");
        }
        return;
    }

    var result = await refactoringEngine.ExecuteRefactoringAsync(plan);

    if (!result.Success)
    {
        Console.WriteLine($"Refactoring failed: {result.Error}");
        // Rollback if needed
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```
