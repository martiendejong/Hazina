using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Services;

/// <summary>
/// Orchestrates social media import operations.
/// Manages account connections, content import, and token refresh.
/// </summary>
public class SocialImportService
{
    private readonly ILogger<SocialImportService> _logger;
    private readonly ISocialAccountStore _accountStore;
    private readonly ISocialContentStore _contentStore;
    private readonly Dictionary<string, ISocialProvider> _providers;

    public SocialImportService(
        ILogger<SocialImportService> logger,
        ISocialAccountStore accountStore,
        ISocialContentStore contentStore,
        IEnumerable<ISocialProvider> providers)
    {
        _logger = logger;
        _accountStore = accountStore;
        _contentStore = contentStore;
        _providers = providers.ToDictionary(p => p.ProviderId);
    }

    /// <summary>
    /// Gets all available providers.
    /// </summary>
    public IReadOnlyDictionary<string, ISocialProvider> Providers => _providers;

    /// <summary>
    /// Gets the authorization URL for a provider.
    /// </summary>
    public string GetAuthorizationUrl(string providerId, string redirectUri, string state)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            throw new ArgumentException($"Unknown provider: {providerId}");
        }

        return provider.GetAuthorizationUrl(redirectUri, state);
    }

    /// <summary>
    /// Completes OAuth flow and connects an account.
    /// </summary>
    public async Task<ConnectedAccount?> CompleteAuthorizationAsync(
        string projectId,
        string providerId,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            _logger.LogWarning("Unknown provider: {ProviderId}", providerId);
            return null;
        }

        // Exchange code for tokens
        var authResult = await provider.ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (!authResult.Success || string.IsNullOrEmpty(authResult.AccessToken))
        {
            _logger.LogWarning("Failed to exchange auth code for {ProviderId}: {Error}",
                providerId, authResult.Error);
            return null;
        }

        // Get user profile
        var profile = await provider.GetProfileAsync(authResult.AccessToken, cancellationToken);
        if (string.IsNullOrEmpty(profile.Id))
        {
            _logger.LogWarning("Failed to get profile for {ProviderId}", providerId);
            return null;
        }

        // Create connected account
        var account = new ConnectedAccount
        {
            ProjectId = projectId,
            ProviderId = providerId,
            ProviderUserId = profile.Id,
            DisplayName = profile.Name,
            ProfileUrl = profile.ProfileUrl,
            AvatarUrl = profile.AvatarUrl,
            AccessToken = authResult.AccessToken,
            RefreshToken = authResult.RefreshToken,
            TokenExpiresAt = authResult.ExpiresAt,
            Status = AccountStatus.Active,
            Metadata = profile.Metadata
        };

        // Save account
        await _accountStore.SaveAccountAsync(projectId, account, cancellationToken);
        _logger.LogInformation("Connected {ProviderId} account {DisplayName} to project {ProjectId}",
            providerId, account.DisplayName, projectId);

        return account;
    }

    /// <summary>
    /// Imports content from a connected account.
    /// </summary>
    public async Task<SocialImportResult> ImportContentAsync(
        string projectId,
        string accountId,
        SocialImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SocialImportOptions();

        var account = await _accountStore.GetAccountAsync(projectId, accountId, cancellationToken);
        if (account == null)
        {
            return new SocialImportResult
            {
                Success = false,
                Error = "Account not found"
            };
        }

        if (!_providers.TryGetValue(account.ProviderId, out var provider))
        {
            return new SocialImportResult
            {
                Success = false,
                Error = $"Provider not configured: {account.ProviderId}"
            };
        }

        // Check if token needs refresh
        if (account.TokenExpiresAt.HasValue && account.TokenExpiresAt.Value < DateTime.UtcNow)
        {
            if (!string.IsNullOrEmpty(account.RefreshToken))
            {
                var refreshResult = await provider.RefreshTokenAsync(account.RefreshToken, cancellationToken);
                if (refreshResult.Success && !string.IsNullOrEmpty(refreshResult.AccessToken))
                {
                    await _accountStore.UpdateTokensAsync(
                        projectId, accountId,
                        refreshResult.AccessToken,
                        refreshResult.RefreshToken,
                        refreshResult.ExpiresAt,
                        cancellationToken);
                    account.AccessToken = refreshResult.AccessToken;
                }
                else
                {
                    _logger.LogWarning("Failed to refresh token for account {AccountId}", accountId);
                    return new SocialImportResult
                    {
                        Success = false,
                        Error = "Token expired and refresh failed"
                    };
                }
            }
            else
            {
                return new SocialImportResult
                {
                    Success = false,
                    Error = "Token expired and no refresh token available"
                };
            }
        }

        // Import content
        _logger.LogInformation("Starting import for account {AccountId} in project {ProjectId}",
            accountId, projectId);

        var result = await provider.ImportContentAsync(account.AccessToken, options, cancellationToken);

        if (result.Success)
        {
            // Save imported content
            if (result.Posts.Count > 0)
            {
                await _contentStore.SavePostsAsync(projectId, accountId, result.Posts, cancellationToken);
            }

            if (result.Articles.Count > 0)
            {
                await _contentStore.SaveArticlesAsync(projectId, accountId, result.Articles, cancellationToken);
            }

            // Update account import stats
            account.LastImportAt = DateTime.UtcNow;
            account.ImportedItemCount += result.TotalImported;
            await _accountStore.SaveAccountAsync(projectId, account, cancellationToken);

            _logger.LogInformation("Imported {Count} items from account {AccountId}",
                result.TotalImported, accountId);
        }

        return result;
    }

    /// <summary>
    /// Disconnects an account and optionally deletes its data.
    /// </summary>
    public async Task<bool> DisconnectAccountAsync(
        string projectId,
        string accountId,
        bool revokeAccess = true,
        bool deleteData = true,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountStore.GetAccountAsync(projectId, accountId, cancellationToken);
        if (account == null)
        {
            return false;
        }

        // Revoke access on provider if requested
        if (revokeAccess && _providers.TryGetValue(account.ProviderId, out var provider))
        {
            try
            {
                await provider.RevokeAccessAsync(account.AccessToken, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke access for account {AccountId}", accountId);
                // Continue with local removal even if revocation fails
            }
        }

        // Remove account and optionally its data
        return await _accountStore.RemoveAccountAsync(projectId, accountId, deleteData, cancellationToken);
    }

    /// <summary>
    /// Gets all connected accounts for a project.
    /// </summary>
    public Task<List<ConnectedAccount>> GetAccountsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        return _accountStore.GetAccountsAsync(projectId, cancellationToken);
    }

    /// <summary>
    /// Searches imported content.
    /// </summary>
    public Task<List<SocialContentSearchResult>> SearchContentAsync(
        string projectId,
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _contentStore.SearchAsync(projectId, query, options, cancellationToken);
    }

    /// <summary>
    /// Gets content statistics.
    /// </summary>
    public Task<ContentStats> GetContentStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        return _contentStore.GetStatsAsync(projectId, cancellationToken);
    }
}
