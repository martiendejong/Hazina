using System.Text.Json;
using Microsoft.Extensions.Configuration;

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
    private readonly string _orderingFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionOrderingService(IConfiguration configuration)
    {
        var configuredPath = configuration["AgenticOrchestration:SessionLogging:BasePath"];
        var baseDir = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HazinaOrchestration", "sessions");
        _orderingFilePath = Path.Combine(baseDir, "session-ordering.json");
        EnsureDirectory();
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_orderingFilePath);
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
            if (!File.Exists(_orderingFilePath))
            {
                return new Dictionary<string, int>();
            }

            var json = await File.ReadAllTextAsync(_orderingFilePath);
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
            await File.WriteAllTextAsync(_orderingFilePath, json);
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
