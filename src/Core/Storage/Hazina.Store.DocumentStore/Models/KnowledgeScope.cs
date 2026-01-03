using System;
using System.Collections.Generic;

/// <summary>
/// Scope levels for knowledge sharing.
/// </summary>
public enum KnowledgeScope
{
    /// <summary>
    /// Project-specific knowledge (default, most restrictive).
    /// </summary>
    Project = 0,

    /// <summary>
    /// Workspace-level knowledge (shared across projects in workspace).
    /// </summary>
    Workspace = 1,

    /// <summary>
    /// Global knowledge (shared across all projects and workspaces).
    /// </summary>
    Global = 2
}

/// <summary>
/// Configuration for a knowledge scope.
/// </summary>
public class ScopeConfiguration
{
    /// <summary>
    /// Scope level.
    /// </summary>
    public KnowledgeScope Scope { get; set; }

    /// <summary>
    /// Identifier for this scope (project ID, workspace ID, or "global").
    /// </summary>
    public string ScopeId { get; set; } = "";

    /// <summary>
    /// Display name for this scope.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Path to the knowledge store for this scope.
    /// </summary>
    public string StorePath { get; set; } = "";

    /// <summary>
    /// Parent scope (null for global).
    /// </summary>
    public string? ParentScopeId { get; set; }

    /// <summary>
    /// Whether this scope is enabled for queries.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority when merging results (higher = more important).
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// Hierarchy of scopes for a project.
/// </summary>
public class ScopeHierarchy
{
    /// <summary>
    /// Project-level scope (most specific).
    /// </summary>
    public ScopeConfiguration Project { get; set; } = new();

    /// <summary>
    /// Workspace-level scope (if applicable).
    /// </summary>
    public ScopeConfiguration? Workspace { get; set; }

    /// <summary>
    /// Global scope (least specific).
    /// </summary>
    public ScopeConfiguration? Global { get; set; }

    /// <summary>
    /// Get all enabled scopes in inheritance order (project → workspace → global).
    /// </summary>
    public List<ScopeConfiguration> GetInheritanceChain()
    {
        var chain = new List<ScopeConfiguration> { Project };
        if (Workspace?.Enabled == true) chain.Add(Workspace);
        if (Global?.Enabled == true) chain.Add(Global);
        return chain;
    }
}

/// <summary>
/// A document with its scope information.
/// </summary>
public class ScopedDocument
{
    /// <summary>
    /// Document metadata.
    /// </summary>
    public DocumentMetadata Document { get; set; } = new();

    /// <summary>
    /// Scope where this document was found.
    /// </summary>
    public KnowledgeScope SourceScope { get; set; }

    /// <summary>
    /// Scope identifier.
    /// </summary>
    public string SourceScopeId { get; set; } = "";

    /// <summary>
    /// Whether this document is inherited (from parent scope).
    /// </summary>
    public bool IsInherited => SourceScope != KnowledgeScope.Project;

    /// <summary>
    /// Original similarity/relevance score.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Adjusted score after scope weighting.
    /// </summary>
    public double AdjustedScore { get; set; }
}

/// <summary>
/// Result of a hierarchical query.
/// </summary>
public class HierarchicalQueryResult
{
    /// <summary>
    /// Documents found, merged and ranked.
    /// </summary>
    public List<ScopedDocument> Documents { get; set; } = new();

    /// <summary>
    /// Count of documents per scope.
    /// </summary>
    public Dictionary<KnowledgeScope, int> CountByScope { get; set; } = new();

    /// <summary>
    /// Scopes that were queried.
    /// </summary>
    public List<KnowledgeScope> QueriedScopes { get; set; } = new();

    /// <summary>
    /// Total documents found.
    /// </summary>
    public int TotalCount => Documents.Count;

    /// <summary>
    /// Whether any inherited documents were included.
    /// </summary>
    public bool IncludesInheritedDocuments =>
        Documents.Any(d => d.IsInherited);
}

/// <summary>
/// Options for hierarchical queries.
/// </summary>
public class HierarchicalQueryOptions
{
    /// <summary>
    /// Whether to include parent scopes in query.
    /// Default: true
    /// </summary>
    public bool IncludeParentScopes { get; set; } = true;

    /// <summary>
    /// Maximum scope level to query.
    /// Default: Global (query all levels)
    /// </summary>
    public KnowledgeScope MaxScope { get; set; } = KnowledgeScope.Global;

    /// <summary>
    /// Weight multiplier for project-level results.
    /// Default: 1.0
    /// </summary>
    public double ProjectWeight { get; set; } = 1.0;

    /// <summary>
    /// Weight multiplier for workspace-level results.
    /// Default: 0.8
    /// </summary>
    public double WorkspaceWeight { get; set; } = 0.8;

    /// <summary>
    /// Weight multiplier for global-level results.
    /// Default: 0.6
    /// </summary>
    public double GlobalWeight { get; set; } = 0.6;

    /// <summary>
    /// How to handle duplicates across scopes.
    /// Default: PreferMostSpecific
    /// </summary>
    public DuplicateHandling DuplicateHandling { get; set; } = DuplicateHandling.PreferMostSpecific;

    /// <summary>
    /// Maximum results per scope.
    /// Default: 20
    /// </summary>
    public int MaxResultsPerScope { get; set; } = 20;

    /// <summary>
    /// Get weight for a scope.
    /// </summary>
    public double GetWeight(KnowledgeScope scope)
    {
        return scope switch
        {
            KnowledgeScope.Project => ProjectWeight,
            KnowledgeScope.Workspace => WorkspaceWeight,
            KnowledgeScope.Global => GlobalWeight,
            _ => 1.0
        };
    }
}

/// <summary>
/// How to handle duplicate documents across scopes.
/// </summary>
public enum DuplicateHandling
{
    /// <summary>
    /// Keep the version from the most specific scope.
    /// </summary>
    PreferMostSpecific,

    /// <summary>
    /// Keep the version from the least specific scope.
    /// </summary>
    PreferLeastSpecific,

    /// <summary>
    /// Keep all versions.
    /// </summary>
    KeepAll,

    /// <summary>
    /// Merge metadata from all scopes.
    /// </summary>
    Merge
}
