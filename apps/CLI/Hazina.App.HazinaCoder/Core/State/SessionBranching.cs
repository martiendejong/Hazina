using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.State;

/// <summary>
/// Session branching for exploring alternatives
/// Iteration 40: Session branching
/// </summary>
public class SessionBranching
{
    private readonly string _sessionsPath;

    public SessionBranching(string sessionsPath = ".hazinacoder/sessions")
    {
        _sessionsPath = sessionsPath;
        Directory.CreateDirectory(_sessionsPath);
    }

    public class SessionSnapshot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<string> Messages { get; set; } = new();
        public Dictionary<string, string> State { get; set; } = new();
        public string? ParentId { get; set; }
    }

    public async Task<string> SaveSessionAsync(string name, List<string> messages, Dictionary<string, string> state)
    {
        var snapshot = new SessionSnapshot
        {
            Name = name,
            Messages = messages,
            State = state
        };

        var path = Path.Combine(_sessionsPath, $"{snapshot.Id}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        return snapshot.Id;
    }

    public async Task<SessionSnapshot?> LoadSessionAsync(string id)
    {
        var path = Path.Combine(_sessionsPath, $"{id}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<SessionSnapshot>(json);
    }

    public async Task<string> BranchSessionAsync(string parentId, string branchName)
    {
        var parent = await LoadSessionAsync(parentId);
        if (parent == null)
            throw new InvalidOperationException($"Session {parentId} not found");

        var branch = new SessionSnapshot
        {
            Name = branchName,
            Messages = new List<string>(parent.Messages),
            State = new Dictionary<string, string>(parent.State),
            ParentId = parentId
        };

        var path = Path.Combine(_sessionsPath, $"{branch.Id}.json");
        var json = JsonSerializer.Serialize(branch, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        return branch.Id;
    }

    public async Task<List<SessionSnapshot>> ListSessionsAsync()
    {
        var sessions = new List<SessionSnapshot>();

        foreach (var file in Directory.GetFiles(_sessionsPath, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var session = JsonSerializer.Deserialize<SessionSnapshot>(json);
            if (session != null)
                sessions.Add(session);
        }

        return sessions.OrderByDescending(s => s.Timestamp).ToList();
    }
}
