using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.Tools.Data;
using Microsoft.Extensions.Options;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// In-memory cache for frequently accessed project data.
    /// Reduces LLM token usage by providing instant access to analysis fields and gathered data.
    /// </summary>
    public class ProjectLocalCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ProjectFileLocator _fileLocator;
        private readonly LLMOptimizationOptions _options;

        public ProjectLocalCache(
            ProjectFileLocator fileLocator,
            IOptions<LLMOptimizationOptions> options)
        {
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
            _options = options?.Value ?? new LLMOptimizationOptions();
        }

        /// <summary>
        /// Get cached project data or load from disk if not cached/expired.
        /// </summary>
        public async Task<CachedProjectData> GetOrLoadAsync(string projectId)
        {
            if (!_options.CacheSettings.Enabled)
            {
                return await LoadFromDiskAsync(projectId);
            }

            if (_cache.TryGetValue(projectId, out var entry))
            {
                var age = DateTime.UtcNow - entry.CachedAt;
                if (age.TotalMinutes < _options.CacheSettings.TimeoutMinutes)
                {
                    return entry.Data;
                }
            }

            // Load from disk and cache
            var data = await LoadFromDiskAsync(projectId);
            _cache[projectId] = new CacheEntry
            {
                Data = data,
                CachedAt = DateTime.UtcNow
            };

            return data;
        }

        /// <summary>
        /// Get specific fields from cached data.
        /// Returns both the data and whether all requested fields were found.
        /// </summary>
        public virtual async Task<DataRetrievalResult> GetDataAsync(string projectId, IEnumerable<string> requestedFields)
        {
            var data = await GetOrLoadAsync(projectId);
            var result = new DataRetrievalResult();

            foreach (var field in requestedFields)
            {
                if (data.AnalysisFields.TryGetValue(field, out var value))
                {
                    result.FoundFields[field] = value;
                }
                else if (data.GatheredData.TryGetValue(field, out var gatheredValue))
                {
                    result.FoundFields[field] = gatheredValue?.ToString() ?? string.Empty;
                }
                else
                {
                    result.MissingFields.Add(field);
                }
            }

            result.AllFound = result.MissingFields.Count == 0;
            return result;
        }

        /// <summary>
        /// Invalidate cache for a project (e.g., after data update).
        /// </summary>
        public void Invalidate(string projectId, string[]? changedFields = null)
        {
            if (!_options.CacheSettings.InvalidateOnUpdate)
                return;

            if (changedFields == null || changedFields.Length == 0)
            {
                // Full invalidation
                _cache.TryRemove(projectId, out _);
            }
            else
            {
                // Partial invalidation - reload specific fields
                if (_cache.TryGetValue(projectId, out var entry))
                {
                    foreach (var field in changedFields)
                    {
                        entry.Data.AnalysisFields.TryRemove(field, out _);
                        entry.Data.GatheredData.TryRemove(field, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Clear all cached data (for testing or manual refresh).
        /// </summary>
        public void ClearAll()
        {
            _cache.Clear();
        }

        private async Task<CachedProjectData> LoadFromDiskAsync(string projectId)
        {
            var data = new CachedProjectData
            {
                ProjectId = projectId,
                CachedAt = DateTime.UtcNow
            };

            // Load analysis fields
            try
            {
                var analysisFolder = _fileLocator.GetPath(projectId, "analysis");
                if (Directory.Exists(analysisFolder))
                {
                    foreach (var file in Directory.GetFiles(analysisFolder, "*.txt"))
                    {
                        try
                        {
                            var key = Path.GetFileNameWithoutExtension(file);
                            var content = await File.ReadAllTextAsync(file);
                            data.AnalysisFields[key] = content;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Cache] Failed to load analysis field {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Failed to load analysis fields for {projectId}: {ex.Message}");
            }

            // Load gathered data
            try
            {
                var gatheredFile = _fileLocator.GetPath(projectId, "gathered-data.json");
                if (File.Exists(gatheredFile))
                {
                    var json = await File.ReadAllTextAsync(gatheredFile);
                    var gathered = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (gathered != null)
                    {
                        foreach (var kvp in gathered)
                        {
                            data.GatheredData[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Failed to load gathered data for {projectId}: {ex.Message}");
            }

            // Load available files list
            try
            {
                var projectFolder = _fileLocator.GetProjectFolder(projectId);
                if (Directory.Exists(projectFolder))
                {
                    var files = Directory.GetFiles(projectFolder, "*.*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(projectFolder, f))
                        .Where(f => !f.StartsWith("embeddings") && !f.StartsWith("metadata"))
                        .ToList();
                    data.AvailableFiles = files;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Failed to load files list for {projectId}: {ex.Message}");
            }

            return data;
        }

        private class CacheEntry
        {
            public CachedProjectData Data { get; set; } = new();
            public DateTime CachedAt { get; set; }
        }
    }

    /// <summary>
    /// Cached project data loaded from disk.
    /// </summary>
    public class CachedProjectData
    {
        public string ProjectId { get; set; } = string.Empty;
        public ConcurrentDictionary<string, string> AnalysisFields { get; set; } = new();
        public ConcurrentDictionary<string, object> GatheredData { get; set; } = new();
        public List<string> AvailableFiles { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }

    /// <summary>
    /// Result of attempting to retrieve specific data fields.
    /// </summary>
    public class DataRetrievalResult
    {
        public Dictionary<string, string> FoundFields { get; set; } = new();
        public List<string> MissingFields { get; set; } = new();
        public bool AllFound { get; set; }
    }
}
