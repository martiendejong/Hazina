using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// File-based implementation of hierarchical metadata store.
/// Enables knowledge sharing across projects via scope inheritance.
/// </summary>
public class HierarchicalMetadataFileStore : IHierarchicalMetadataStore
{
    private readonly string _basePath;
    private readonly Dictionary<string, QueryableMetadataFileStore> _scopeStores = new();
    private readonly Dictionary<string, ScopeHierarchy> _hierarchies = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HierarchicalMetadataFileStore(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(Path.Combine(_basePath, "global"));
        Directory.CreateDirectory(Path.Combine(_basePath, "workspaces"));
        Directory.CreateDirectory(Path.Combine(_basePath, "projects"));
    }

    #region IHierarchicalMetadataStore Implementation

    public Task<ScopeHierarchy> GetScopeHierarchyAsync(
        string projectId,
        CancellationToken ct = default)
    {
        if (_hierarchies.TryGetValue(projectId, out var hierarchy))
        {
            return Task.FromResult(hierarchy);
        }

        // Create default hierarchy
        hierarchy = new ScopeHierarchy
        {
            Project = new ScopeConfiguration
            {
                Scope = KnowledgeScope.Project,
                ScopeId = projectId,
                Name = projectId,
                StorePath = GetScopePath(projectId, KnowledgeScope.Project),
                Priority = 100,
                Enabled = true
            },
            Global = new ScopeConfiguration
            {
                Scope = KnowledgeScope.Global,
                ScopeId = "global",
                Name = "Global Knowledge",
                StorePath = GetScopePath("global", KnowledgeScope.Global),
                Priority = 10,
                Enabled = true
            }
        };

        _hierarchies[projectId] = hierarchy;
        return Task.FromResult(hierarchy);
    }

    public async Task<HierarchicalQueryResult> QueryHierarchicalAsync(
        string projectId,
        MetadataFilter filter,
        HierarchicalQueryOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new HierarchicalQueryOptions();
        var hierarchy = await GetScopeHierarchyAsync(projectId, ct);
        var result = new HierarchicalQueryResult();
        var seenDocIds = new HashSet<string>();

        foreach (var scope in hierarchy.GetInheritanceChain())
        {
            if (scope.Scope > options.MaxScope) continue;
            if (!scope.Enabled) continue;

            result.QueriedScopes.Add(scope.Scope);
            var store = GetOrCreateStore(scope);

            var scopeFilter = new MetadataFilter
            {
                MimeType = filter.MimeType,
                MimeTypePrefix = filter.MimeTypePrefix,
                PathPattern = filter.PathPattern,
                CreatedAfter = filter.CreatedAfter,
                CreatedBefore = filter.CreatedBefore,
                Tags = filter.Tags,
                AnyTags = filter.AnyTags,
                CustomMetadata = filter.CustomMetadata,
                IsBinary = filter.IsBinary,
                Limit = options.MaxResultsPerScope
            };

            var scopeResults = await store.QueryAsync(scopeFilter, ct);
            var weight = options.GetWeight(scope.Scope);
            var scopeCount = 0;

            foreach (var doc in scopeResults)
            {
                // Handle duplicates
                if (seenDocIds.Contains(doc.Id))
                {
                    if (options.DuplicateHandling == DuplicateHandling.PreferMostSpecific)
                        continue;
                    if (options.DuplicateHandling == DuplicateHandling.PreferLeastSpecific)
                    {
                        // Remove the more specific version
                        result.Documents.RemoveAll(d => d.Document.Id == doc.Id);
                    }
                }

                seenDocIds.Add(doc.Id);

                result.Documents.Add(new ScopedDocument
                {
                    Document = doc,
                    SourceScope = scope.Scope,
                    SourceScopeId = scope.ScopeId,
                    RelevanceScore = 1.0,
                    AdjustedScore = weight
                });

                scopeCount++;
            }

            result.CountByScope[scope.Scope] = scopeCount;
        }

        // Sort by adjusted score then by scope priority
        result.Documents = result.Documents
            .OrderByDescending(d => d.AdjustedScore)
            .ThenBy(d => d.SourceScope)
            .ToList();

        return result;
    }

    public async Task<HierarchicalQueryResult> SearchTextHierarchicalAsync(
        string projectId,
        string searchText,
        MetadataFilter? filter = null,
        HierarchicalQueryOptions? options = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        options ??= new HierarchicalQueryOptions();
        var hierarchy = await GetScopeHierarchyAsync(projectId, ct);
        var result = new HierarchicalQueryResult();
        var seenDocIds = new HashSet<string>();

        foreach (var scope in hierarchy.GetInheritanceChain())
        {
            if (scope.Scope > options.MaxScope) continue;
            if (!scope.Enabled) continue;

            result.QueriedScopes.Add(scope.Scope);
            var store = GetOrCreateStore(scope);

            var scopeResults = await store.SearchTextAsync(
                searchText,
                filter,
                options.MaxResultsPerScope,
                ct);

            var weight = options.GetWeight(scope.Scope);
            var scopeCount = 0;

            foreach (var doc in scopeResults)
            {
                if (seenDocIds.Contains(doc.Id) &&
                    options.DuplicateHandling == DuplicateHandling.PreferMostSpecific)
                {
                    continue;
                }

                seenDocIds.Add(doc.Id);

                result.Documents.Add(new ScopedDocument
                {
                    Document = doc,
                    SourceScope = scope.Scope,
                    SourceScopeId = scope.ScopeId,
                    RelevanceScore = 1.0,
                    AdjustedScore = weight
                });

                scopeCount++;
            }

            result.CountByScope[scope.Scope] = scopeCount;
        }

        result.Documents = result.Documents
            .OrderByDescending(d => d.AdjustedScore)
            .Take(limit)
            .ToList();

        return result;
    }

    public async Task StoreInScopeAsync(
        string scopeId,
        KnowledgeScope scope,
        string documentId,
        DocumentMetadata metadata,
        CancellationToken ct = default)
    {
        var config = new ScopeConfiguration
        {
            Scope = scope,
            ScopeId = scopeId,
            StorePath = GetScopePath(scopeId, scope)
        };

        var store = GetOrCreateStore(config);
        await store.Store(documentId, metadata);
    }

    public async Task<List<DocumentMetadata>> GetFromScopeAsync(
        string scopeId,
        KnowledgeScope scope,
        MetadataFilter? filter = null,
        CancellationToken ct = default)
    {
        var config = new ScopeConfiguration
        {
            Scope = scope,
            ScopeId = scopeId,
            StorePath = GetScopePath(scopeId, scope)
        };

        var store = GetOrCreateStore(config);
        return await store.QueryAsync(filter ?? new MetadataFilter(), ct);
    }

    public async Task MoveToScopeAsync(
        string documentId,
        string fromScopeId,
        KnowledgeScope fromScope,
        string toScopeId,
        KnowledgeScope toScope,
        CancellationToken ct = default)
    {
        var fromConfig = new ScopeConfiguration
        {
            Scope = fromScope,
            ScopeId = fromScopeId,
            StorePath = GetScopePath(fromScopeId, fromScope)
        };

        var toConfig = new ScopeConfiguration
        {
            Scope = toScope,
            ScopeId = toScopeId,
            StorePath = GetScopePath(toScopeId, toScope)
        };

        var fromStore = GetOrCreateStore(fromConfig);
        var toStore = GetOrCreateStore(toConfig);

        // Get document from source
        var doc = await fromStore.Get(documentId);
        if (doc == null) return;

        // Store in destination
        await toStore.Store(documentId, doc);

        // Remove from source
        await fromStore.Remove(documentId);
    }

    public async Task PromoteToParentScopeAsync(
        string documentId,
        string projectId,
        CancellationToken ct = default)
    {
        var hierarchy = await GetScopeHierarchyAsync(projectId, ct);
        var projectStore = GetOrCreateStore(hierarchy.Project);

        var doc = await projectStore.Get(documentId);
        if (doc == null) return;

        // Promote to workspace if available, otherwise global
        ScopeConfiguration? targetScope = hierarchy.Workspace ?? hierarchy.Global;
        if (targetScope == null) return;

        var targetStore = GetOrCreateStore(targetScope);
        await targetStore.Store(documentId, doc);
    }

    public Task ConfigureScopesAsync(
        string projectId,
        ScopeHierarchy hierarchy,
        CancellationToken ct = default)
    {
        _hierarchies[projectId] = hierarchy;
        return Task.CompletedTask;
    }

    #endregion

    #region IQueryableMetadataStore Implementation (delegates to project scope)

    public Task<List<DocumentMetadata>> QueryAsync(MetadataFilter filter, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use QueryHierarchicalAsync with projectId");
    }

    public Task<List<string>> GetMatchingIdsAsync(MetadataFilter filter, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use QueryHierarchicalAsync with projectId");
    }

    public Task<int> CountAsync(MetadataFilter filter, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use QueryHierarchicalAsync with projectId");
    }

    public Task<List<DocumentMetadata>> SearchTextAsync(string searchText, MetadataFilter? filter, int limit, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use SearchTextHierarchicalAsync with projectId");
    }

    #endregion

    #region IDocumentMetadataStore Implementation

    public Task<bool> Store(string documentId, DocumentMetadata metadata)
    {
        throw new NotSupportedException("Use StoreInScopeAsync");
    }

    public Task<DocumentMetadata?> Get(string documentId)
    {
        throw new NotSupportedException("Use GetFromScopeAsync");
    }

    public Task<bool> Remove(string documentId)
    {
        throw new NotSupportedException("Use scope-specific delete");
    }

    public Task<bool> Exists(string documentId)
    {
        throw new NotSupportedException("Use scope-specific check");
    }

    #endregion

    #region Private Helpers

    private string GetScopePath(string scopeId, KnowledgeScope scope)
    {
        return scope switch
        {
            KnowledgeScope.Global => Path.Combine(_basePath, "global"),
            KnowledgeScope.Workspace => Path.Combine(_basePath, "workspaces", scopeId),
            KnowledgeScope.Project => Path.Combine(_basePath, "projects", scopeId),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
    }

    private QueryableMetadataFileStore GetOrCreateStore(ScopeConfiguration config)
    {
        var key = $"{config.Scope}:{config.ScopeId}";
        if (!_scopeStores.TryGetValue(key, out var store))
        {
            Directory.CreateDirectory(config.StorePath);
            store = new QueryableMetadataFileStore(config.StorePath);
            _scopeStores[key] = store;
        }
        return store;
    }

    #endregion
}
