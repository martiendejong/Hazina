namespace Hazina.Tools.Services.Database.Core;

/// <summary>
/// Defines mandatory restrictions for multi-tenant database access.
/// These restrictions are applied as a subquery wrapper and cannot be bypassed by agents.
/// </summary>
public class RestrictionPolicy
{
    /// <summary>
    /// Name of the policy for logging/debugging.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Required predicates that must always be applied.
    /// </summary>
    public List<RestrictionPredicate> RequiredPredicates { get; set; } = new();

    /// <summary>
    /// Whether to fail silently (return empty results) or throw an error
    /// when restrictions yield no results.
    /// Default is true (silent) to prevent information leakage.
    /// </summary>
    public bool SilentRestriction { get; set; } = true;

    /// <summary>
    /// Tables this policy applies to. Empty means all tables.
    /// </summary>
    public HashSet<string> ApplicableTables { get; set; } = new();

    /// <summary>
    /// Checks if this policy applies to a given table.
    /// </summary>
    public bool AppliesToTable(string tableName)
    {
        if (ApplicableTables.Count == 0)
            return true;

        return ApplicableTables.Contains(tableName);
    }
}

/// <summary>
/// A mandatory predicate in a restriction policy.
/// </summary>
public class RestrictionPredicate
{
    /// <summary>
    /// Column name to restrict on.
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Comparison operator (=, IN, etc.).
    /// </summary>
    public string Operator { get; set; } = "=";

    /// <summary>
    /// Source of the restriction value. Supported sources:
    /// - "context.ProjectId" - Current project ID
    /// - "context.UserId" - Current user ID
    /// - "context.AccountId" - Current connected account ID
    /// - "literal:value" - A literal value
    /// </summary>
    public string ValueSource { get; set; } = string.Empty;
}

/// <summary>
/// Context for resolving restriction values.
/// </summary>
public class RestrictionContext
{
    /// <summary>
    /// Current project ID.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Current user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Current connected account ID (for social imports).
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Additional context values that can be referenced.
    /// </summary>
    public Dictionary<string, object> AdditionalContext { get; set; } = new();

    /// <summary>
    /// Resolves a value source to an actual value.
    /// </summary>
    public object? ResolveValueSource(string valueSource)
    {
        if (string.IsNullOrEmpty(valueSource))
            return null;

        // Literal values
        if (valueSource.StartsWith("literal:"))
            return valueSource.Substring(8);

        // Context values
        return valueSource.ToLowerInvariant() switch
        {
            "context.projectid" => ProjectId,
            "context.userid" => UserId,
            "context.accountid" => AccountId,
            _ => AdditionalContext.TryGetValue(valueSource, out var val) ? val : null
        };
    }
}

/// <summary>
/// Result of applying restrictions.
/// </summary>
public class RestrictionResult
{
    /// <summary>
    /// SQL fragment for the restriction (to be used in subquery).
    /// </summary>
    public string SqlFragment { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the restriction.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Whether all restrictions could be resolved.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Errors if restrictions couldn't be resolved.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
