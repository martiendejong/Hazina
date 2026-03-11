using System.Text.Json;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Service for managing session display ordering
/// </summary>
public interface ISessionOrderingService
{
    Task<Dictionary<string, int>> GetOrderingAsync();
    Task SaveOrderingAsync(Dictionary<string, int> ordering);
    Task UpdateSessionOrderAsync(string sessionId, int order);
    Task RemoveSessionAsync(string sessionId);
}

public class SessionOrderingService : ISessionOrderingService
{
    private const string OrderingFilePath = @"E:\orchestration-sessions\session-ordering.json";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionOrderingService()
    {
        EnsureDirectory();
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(OrderingFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<Dictionary<string, int>> GetOrderingAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(OrderingFilePath))
            {
                return new Dictionary<string, int>();
            }

            var json = await File.ReadAllTextAsync(OrderingFilePath);
            var ordering = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            return ordering ?? new Dictionary<string, int>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveOrderingAsync(Dictionary<string, int> ordering)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(ordering, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(OrderingFilePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSessionOrderAsync(string sessionId, int order)
    {
        var ordering = await GetOrderingAsync();
        ordering[sessionId] = order;
        await SaveOrderingAsync(ordering);
    }

    public async Task RemoveSessionAsync(string sessionId)
    {
        var ordering = await GetOrderingAsync();
        ordering.Remove(sessionId);
        await SaveOrderingAsync(ordering);
    }
}
