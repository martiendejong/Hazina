using Hazina.AI.Providers.Core;
using Hazina.CodeIntelligence.Core;
using Hazina.Neurochain.Core;
using System.Text;

namespace Hazina.CodeIntelligence.Refactoring;

/// <summary>
/// Multi-file refactoring engine with architectural awareness
/// </summary>
public class MultiFileRefactoringEngine
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly NeuroChainOrchestrator? _neurochain;
    private readonly RefactoringConfig _config;

    public MultiFileRefactoringEngine(
        IProviderOrchestrator orchestrator,
        NeuroChainOrchestrator? neurochain = null,
        RefactoringConfig? config = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _neurochain = neurochain;
        _config = config ?? new RefactoringConfig();
    }

    /// <summary>
    /// Analyze refactoring impact across multiple files
    /// </summary>
    public async Task<RefactoringPlan> AnalyzeRefactoringAsync(
        RefactoringRequest request,
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var plan = new RefactoringPlan
        {
            Request = request,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Identify affected files
            plan.AffectedFiles = IdentifyAffectedFiles(request, context);

            // Analyze dependencies
            plan.DependencyImpact = AnalyzeDependencyImpact(plan.AffectedFiles, context);

            // Generate refactoring steps using AI
            plan.Steps = await GenerateRefactoringStepsAsync(request, plan.AffectedFiles, context, cancellationToken);

            // Estimate risk
            plan.RiskAssessment = AssessRisk(plan);

            // Generate recommendations
            plan.Recommendations = GenerateRecommendations(plan);

            plan.IsValid = true;
        }
        catch (Exception ex)
        {
            plan.IsValid = false;
            plan.Error = ex.Message;
        }

        return plan;
    }

    /// <summary>
    /// Execute refactoring plan
    /// </summary>
    public async Task<RefactoringResult> ExecuteRefactoringAsync(
        RefactoringPlan plan,
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new RefactoringResult
        {
            Plan = plan,
            StartTime = DateTime.UtcNow
        };

        try
        {
            foreach (var step in plan.Steps)
            {
                var stepResult = await ExecuteStepAsync(step, context, cancellationToken);
                result.StepResults.Add(stepResult);

                if (!stepResult.Success && _config.StopOnError)
                {
                    result.Success = false;
                    result.Error = $"Step failed: {step.Description}";
                    break;
                }
            }

            result.Success = result.StepResults.All(s => s.Success);
            result.ModifiedFiles = result.StepResults
                .SelectMany(s => s.ModifiedFiles)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// Validate refactoring before execution
    /// </summary>
    public async Task<ValidationResult> ValidateRefactoringAsync(
        RefactoringPlan plan,
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();

        // Check for circular dependencies
        if (plan.DependencyImpact.IntroducesCircularDependency)
        {
            issues.Add(new ValidationIssue
            {
                Type = "CircularDependency",
                Description = "Refactoring would introduce circular dependency",
                Severity = 0.9
            });
        }

        // Check for breaking changes
        var breakingChanges = await DetectBreakingChangesAsync(plan, context, cancellationToken);
        foreach (var change in breakingChanges)
        {
            issues.Add(new ValidationIssue
            {
                Type = "BreakingChange",
                Description = change,
                Severity = 0.8
            });
        }

        // Check architectural violations
        if (context.Architecture != null)
        {
            var violations = DetectArchitecturalViolations(plan, context.Architecture);
            issues.AddRange(violations);
        }

        return new ValidationResult
        {
            IsValid = issues.Count == 0 || issues.All(i => i.Severity < 0.7),
            Confidence = issues.Count == 0 ? 0.9 : 0.6,
            Issues = issues
        };
    }

    #region Private Methods

    private List<string> IdentifyAffectedFiles(RefactoringRequest request, ProjectContext context)
    {
        var affected = new HashSet<string>();

        // Add target files
        affected.UnionWith(request.TargetFiles);

        // Add files with direct dependencies
        foreach (var file in request.TargetFiles)
        {
            var codeFile = context.Files.FirstOrDefault(f => f.Path == file);
            if (codeFile != null)
            {
                affected.UnionWith(codeFile.Dependents);
            }
        }

        // Add files that reference affected symbols
        if (request.AffectedSymbols.Count > 0)
        {
            foreach (var symbolName in request.AffectedSymbols)
            {
                if (context.Symbols.TryGetValue(symbolName, out var symbol))
                {
                    affected.UnionWith(symbol.References.Select(r => r.File));
                }
            }
        }

        return affected.ToList();
    }

    private DependencyImpact AnalyzeDependencyImpact(List<string> affectedFiles, ProjectContext context)
    {
        var impact = new DependencyImpact
        {
            AffectedFileCount = affectedFiles.Count
        };

        // Check for circular dependencies
        var dependencies = new HashSet<string>();
        foreach (var file in affectedFiles)
        {
            if (context.DependencyGraph.TryGetValue(file, out var deps))
            {
                dependencies.UnionWith(deps);
            }
        }

        // Simple cycle detection
        impact.IntroducesCircularDependency = dependencies.Intersect(affectedFiles).Any();

        // Calculate dependency depth
        impact.MaxDependencyDepth = CalculateDependencyDepth(affectedFiles, context);

        return impact;
    }

    private int CalculateDependencyDepth(List<string> files, ProjectContext context)
    {
        int maxDepth = 0;

        foreach (var file in files)
        {
            var depth = CalculateDepth(file, context, new HashSet<string>());
            maxDepth = Math.Max(maxDepth, depth);
        }

        return maxDepth;
    }

    private int CalculateDepth(string file, ProjectContext context, HashSet<string> visited)
    {
        if (visited.Contains(file))
            return 0;

        visited.Add(file);

        if (!context.DependencyGraph.TryGetValue(file, out var deps))
            return 1;

        int maxChildDepth = 0;
        foreach (var dep in deps)
        {
            var depth = CalculateDepth(dep, context, visited);
            maxChildDepth = Math.Max(maxChildDepth, depth);
        }

        return maxChildDepth + 1;
    }

    private async Task<List<RefactoringStep>> GenerateRefactoringStepsAsync(
        RefactoringRequest request,
        List<string> affectedFiles,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var steps = new List<RefactoringStep>();

        // Build prompt for AI
        var prompt = BuildRefactoringPrompt(request, affectedFiles, context);

        // Use Neurochain if available for higher confidence
        string response;
        if (_neurochain != null)
        {
            var result = await _neurochain.ReasonAsync(prompt, new ReasoningContext
            {
                MinConfidence = 0.9,
                Domain = "Software Engineering - Code Refactoring"
            }, cancellationToken);
            response = result.FinalAnswer;
        }
        else
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "You are an expert software architect. Generate detailed refactoring steps."
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = prompt
                }
            };

            var llmResponse = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            response = llmResponse.Result;
        }

        // Parse steps from response
        steps = ParseRefactoringSteps(response, affectedFiles);

        return steps;
    }

    private string BuildRefactoringPrompt(RefactoringRequest request, List<string> affectedFiles, ProjectContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Refactoring Task: {request.Description}");
        sb.AppendLine();
        sb.AppendLine($"Type: {request.Type}");
        sb.AppendLine($"Affected Files ({affectedFiles.Count}):");
        foreach (var file in affectedFiles.Take(10))
        {
            sb.AppendLine($"  - {file}");
        }
        if (affectedFiles.Count > 10)
        {
            sb.AppendLine($"  ... and {affectedFiles.Count - 10} more");
        }

        if (context.Architecture != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Architecture: {context.Architecture.ArchitecturalPattern}");
            sb.AppendLine($"Structure: {context.Architecture.StructureDescription}");
        }

        sb.AppendLine();
        sb.AppendLine("Generate step-by-step refactoring plan. Format:");
        sb.AppendLine("STEP 1: [description]");
        sb.AppendLine("FILE: [file path]");
        sb.AppendLine("ACTION: [what to do]");
        sb.AppendLine("STEP 2: ...");

        return sb.ToString();
    }

    private List<RefactoringStep> ParseRefactoringSteps(string response, List<string> affectedFiles)
    {
        var steps = new List<RefactoringStep>();
        var lines = response.Split('\n');

        RefactoringStep? currentStep = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("STEP ", StringComparison.OrdinalIgnoreCase))
            {
                if (currentStep != null)
                {
                    steps.Add(currentStep);
                }

                currentStep = new RefactoringStep
                {
                    Order = steps.Count + 1,
                    Description = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim()
                };
            }
            else if (currentStep != null)
            {
                if (trimmed.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
                {
                    var file = trimmed.Substring("FILE:".Length).Trim();
                    currentStep.TargetFiles.Add(file);
                }
                else if (trimmed.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
                {
                    currentStep.Action = trimmed.Substring("ACTION:".Length).Trim();
                }
            }
        }

        if (currentStep != null)
        {
            steps.Add(currentStep);
        }

        return steps;
    }

    private RiskAssessment AssessRisk(RefactoringPlan plan)
    {
        var assessment = new RiskAssessment();

        // Risk based on number of affected files
        if (plan.AffectedFiles.Count > 20)
            assessment.OverallRisk = RiskLevel.High;
        else if (plan.AffectedFiles.Count > 10)
            assessment.OverallRisk = RiskLevel.Medium;
        else if (plan.AffectedFiles.Count > 3)
            assessment.OverallRisk = RiskLevel.Low;
        else
            assessment.OverallRisk = RiskLevel.VeryLow;

        // Risk based on dependency complexity
        if (plan.DependencyImpact.IntroducesCircularDependency)
        {
            assessment.Risks.Add("Introduces circular dependency");
            assessment.OverallRisk = RiskLevel.High;
        }

        if (plan.DependencyImpact.MaxDependencyDepth > 5)
        {
            assessment.Risks.Add("Deep dependency chain affected");
        }

        // Risk based on refactoring type
        if (plan.Request.Type == RefactoringType.ArchitecturalChange)
        {
            assessment.Risks.Add("Architectural change - high impact");
            if (assessment.OverallRisk < RiskLevel.Medium)
                assessment.OverallRisk = RiskLevel.Medium;
        }

        return assessment;
    }

    private List<string> GenerateRecommendations(RefactoringPlan plan)
    {
        var recommendations = new List<string>();

        if (plan.AffectedFiles.Count > 5)
        {
            recommendations.Add("Create a backup before refactoring");
            recommendations.Add("Commit current changes to version control");
        }

        if (plan.DependencyImpact.MaxDependencyDepth > 3)
        {
            recommendations.Add("Test thoroughly after each step");
        }

        if (plan.RiskAssessment.OverallRisk >= RiskLevel.Medium)
        {
            recommendations.Add("Consider performing refactoring in smaller increments");
            recommendations.Add("Add integration tests before refactoring");
        }

        return recommendations;
    }

    private async Task<RefactoringStepResult> ExecuteStepAsync(
        RefactoringStep step,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var result = new RefactoringStepResult
        {
            Step = step,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // In a real implementation, this would perform actual code modifications
            // For now, we simulate success
            result.Success = true;
            result.ModifiedFiles = step.TargetFiles;
            result.Message = $"Step {step.Order} completed successfully";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private async Task<List<string>> DetectBreakingChangesAsync(
        RefactoringPlan plan,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var breakingChanges = new List<string>();

        // Check for public API changes
        foreach (var symbol in plan.Request.AffectedSymbols)
        {
            if (context.Symbols.TryGetValue(symbol, out var symbolInfo))
            {
                if (symbolInfo.Visibility == "public" && symbolInfo.References.Count > 0)
                {
                    breakingChanges.Add($"Public symbol '{symbol}' has {symbolInfo.References.Count} external references");
                }
            }
        }

        return breakingChanges;
    }

    private List<ValidationIssue> DetectArchitecturalViolations(
        RefactoringPlan plan,
        ArchitecturalInsights architecture)
    {
        var violations = new List<ValidationIssue>();

        // Check layer violations (simplified)
        foreach (var dep in plan.DependencyImpact.NewDependencies)
        {
            // Add logic to detect violations based on architecture
        }

        return violations;
    }

    #endregion
}

/// <summary>
/// Refactoring configuration
/// </summary>
public class RefactoringConfig
{
    public bool StopOnError { get; set; } = true;
    public bool ValidateBeforeExecution { get; set; } = true;
    public bool CreateBackup { get; set; } = true;
}

/// <summary>
/// Refactoring request
/// </summary>
public class RefactoringRequest
{
    public string Description { get; set; } = string.Empty;
    public RefactoringType Type { get; set; }
    public List<string> TargetFiles { get; set; } = new();
    public List<string> AffectedSymbols { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Types of refactoring
/// </summary>
public enum RefactoringType
{
    Rename,
    ExtractMethod,
    ExtractClass,
    MoveMethod,
    InlineMethod,
    ChangeSignature,
    ArchitecturalChange,
    DesignPatternApplication,
    CodeCleanup
}

/// <summary>
/// Refactoring plan
/// </summary>
public class RefactoringPlan
{
    public RefactoringRequest Request { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public List<string> AffectedFiles { get; set; } = new();
    public DependencyImpact DependencyImpact { get; set; } = new();
    public List<RefactoringStep> Steps { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Dependency impact analysis
/// </summary>
public class DependencyImpact
{
    public int AffectedFileCount { get; set; }
    public bool IntroducesCircularDependency { get; set; }
    public int MaxDependencyDepth { get; set; }
    public List<string> NewDependencies { get; set; } = new();
}

/// <summary>
/// Refactoring step
/// </summary>
public class RefactoringStep
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<string> TargetFiles { get; set; } = new();
    public List<string> Prerequisites { get; set; } = new();
}

/// <summary>
/// Risk assessment
/// </summary>
public class RiskAssessment
{
    public RiskLevel OverallRisk { get; set; }
    public List<string> Risks { get; set; } = new();
}

/// <summary>
/// Risk levels
/// </summary>
public enum RiskLevel
{
    VeryLow,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Refactoring result
/// </summary>
public class RefactoringResult
{
    public RefactoringPlan Plan { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<RefactoringStepResult> StepResults { get; set; } = new();
    public List<string> ModifiedFiles { get; set; } = new();
}

/// <summary>
/// Result of a single refactoring step
/// </summary>
public class RefactoringStepResult
{
    public RefactoringStep Step { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ModifiedFiles { get; set; } = new();
}
