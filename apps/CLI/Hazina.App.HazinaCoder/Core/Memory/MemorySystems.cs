using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Identity;

/// <summary>
/// Memory systems - episodic, semantic, procedural, working memory
/// Mirrors human memory architecture
/// </summary>
public class MemorySystems
{
    private readonly string _basePath;

    public MemorySystems(string basePath)
    {
        _basePath = basePath;
    }

    // Episodic Memory (what happened when)
    public List<SessionMemory> Episodes { get; set; } = new();

    // Semantic Memory (facts and knowledge) - references knowledge-base/
    public Dictionary<string, string> Facts { get; set; } = new();

    // Procedural Memory (how to do things) - references tools/ and skills/
    public List<string> Procedures { get; set; } = new();

    // Working Memory (current context) - limited capacity
    public WorkingMemory Current { get; set; } = new();

    /// <summary>
    /// Recall similar episodes from episodic memory
    /// </summary>
    public async Task<List<SessionMemory>> RecallSimilar(string query)
    {
        // TODO: Implement semantic search across reflection log
        return Episodes.Where(e => e.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Consolidate session into long-term memory
    /// </summary>
    public async Task ConsolidateMemory(SessionMemory session)
    {
        Episodes.Add(session);

        // Extract facts for semantic memory
        // TODO: NLP-based fact extraction

        // Identify repeated patterns for procedural memory
        // TODO: Pattern recognition across episodes

        await Task.CompletedTask;
    }

    /// <summary>
    /// Load semantic knowledge from knowledge-base/
    /// </summary>
    public async Task LoadKnowledgeBase()
    {
        var kbPath = Path.Combine(Path.GetDirectoryName(_basePath)!, "knowledge-base");
        if (!Directory.Exists(kbPath))
            return;

        // Load essential facts from INDEX.md files
        var indexFiles = Directory.GetFiles(kbPath, "INDEX.md", SearchOption.AllDirectories);
        foreach (var file in indexFiles)
        {
            var category = Path.GetFileName(Path.GetDirectoryName(file));
            Facts[$"{category}_index"] = await File.ReadAllTextAsync(file);
        }
    }
}

/// <summary>
/// Session memory - single episode in episodic memory
/// </summary>
public class SessionMemory
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
    public List<string> Outcomes { get; set; } = new();
    public List<string> Learnings { get; set; } = new();
    public List<string> Patterns { get; set; } = new();
}

/// <summary>
/// Working memory - limited capacity, current session context
/// </summary>
public class WorkingMemory
{
    public List<string> RecentlyAccessedFiles { get; set; } = new();
    public List<string> RecentlyAccessedKnowledge { get; set; } = new();
    public List<string> RecentPatterns { get; set; } = new();
    public Dictionary<string, object> CurrentContext { get; set; } = new();

    private const int MaxCapacity = 20; // Limited working memory

    public void AddFile(string filePath)
    {
        RecentlyAccessedFiles.Insert(0, filePath);
        if (RecentlyAccessedFiles.Count > MaxCapacity)
            RecentlyAccessedFiles = RecentlyAccessedFiles.Take(MaxCapacity).ToList();
    }

    public void AddKnowledge(string knowledge)
    {
        RecentlyAccessedKnowledge.Insert(0, knowledge);
        if (RecentlyAccessedKnowledge.Count > MaxCapacity)
            RecentlyAccessedKnowledge = RecentlyAccessedKnowledge.Take(MaxCapacity).ToList();
    }
}
