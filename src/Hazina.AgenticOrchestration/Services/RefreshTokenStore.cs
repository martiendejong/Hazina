using System.Collections.Concurrent;

namespace Hazina.AgenticOrchestration.Services;

public class RefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new();

    public void StoreToken(string token, string username, DateTime expiresAt)
    {
        _tokens[token] = new RefreshTokenEntry
        {
            Username = username,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        // Clean up expired tokens periodically
        CleanupExpiredTokens();
    }

    public bool ValidateToken(string token, out string? username)
    {
        username = null;

        if (!_tokens.TryGetValue(token, out var entry))
            return false;

        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        username = entry.Username;
        return true;
    }

    public void RevokeToken(string token)
    {
        _tokens.TryRemove(token, out _);
    }

    private void CleanupExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _tokens.Where(kvp => kvp.Value.ExpiresAt < now)
                                   .Select(kvp => kvp.Key)
                                   .ToList();

        foreach (var token in expiredTokens)
        {
            _tokens.TryRemove(token, out _);
        }
    }

    private class RefreshTokenEntry
    {
        public string Username { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
