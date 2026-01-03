using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Metadata store with hierarchical scope support.
/// Enables knowledge sharing across projects and workspaces.
/// </summary>
public interface IHierarchicalMetadataStore : IQueryableMetadataStore
{
    /// <summary>
    /// Get the scope hierarchy for a project.
    /// </summary>
    Task<ScopeHierarchy> GetScopeHierarchyAsync(
        string projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Query documents across multiple scopes.
    /// </summary>
    /// <param name="projectId">Project ID (determines scope hierarchy)</param>
    /// <param name="filter">Metadata filter</param>
    /// <param name="options">Hierarchical query options</param>
    /// <param name="ct">Cancellation token</param>
    Task<HierarchicalQueryResult> QueryHierarchicalAsync(
        string projectId,
        MetadataFilter filter,
        HierarchicalQueryOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Search text across multiple scopes.
    /// </summary>
    Task<HierarchicalQueryResult> SearchTextHierarchicalAsync(
        string projectId,
        string searchText,
        MetadataFilter? filter = null,
        HierarchicalQueryOptions? options = null,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Store a document in a specific scope.
    /// </summary>
    /// <param name="scopeId">Scope identifier</param>
    /// <param name="scope">Scope level</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="metadata">Document metadata</param>
    /// <param name="ct">Cancellation token</param>
    Task StoreInScopeAsync(
        string scopeId,
        KnowledgeScope scope,
        string documentId,
        DocumentMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Get documents from a specific scope.
    /// </summary>
    Task<List<DocumentMetadata>> GetFromScopeAsync(
        string scopeId,
        KnowledgeScope scope,
        MetadataFilter? filter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Move a document to a different scope.
    /// </summary>
    Task MoveToScopeAsync(
        string documentId,
        string fromScopeId,
        KnowledgeScope fromScope,
        string toScopeId,
        KnowledgeScope toScope,
        CancellationToken ct = default);

    /// <summary>
    /// Copy a document to a parent scope (promote).
    /// </summary>
    Task PromoteToParentScopeAsync(
        string documentId,
        string projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Configure scope hierarchy for a project.
    /// </summary>
    Task ConfigureScopesAsync(
        string projectId,
        ScopeHierarchy hierarchy,
        CancellationToken ct = default);
}
