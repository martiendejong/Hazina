namespace Hazina.Security.ApiKeys;

/// <summary>
/// What an API key may do. Scopes are hierarchical: <see cref="Admin"/> implies <see cref="Write"/>
/// implies <see cref="Read"/>. Enforced via <see cref="HazinaApiKeyPolicies"/>.
/// </summary>
public enum ApiKeyScope
{
    Read = 1,
    Write = 2,
    Admin = 3,
}

public static class ApiKeyScopes
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Admin = "admin";

    /// <summary>True when a key holding <paramref name="actual"/> may perform an action that needs <paramref name="required"/>.</summary>
    public static bool Satisfies(this ApiKeyScope actual, ApiKeyScope required) => actual >= required;

    public static string ToClaimValue(this ApiKeyScope scope) => scope switch
    {
        ApiKeyScope.Read => Read,
        ApiKeyScope.Write => Write,
        ApiKeyScope.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown API key scope."),
    };

    public static bool TryParse(string? value, out ApiKeyScope scope)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case Read: scope = ApiKeyScope.Read; return true;
            case Write: scope = ApiKeyScope.Write; return true;
            case Admin: scope = ApiKeyScope.Admin; return true;
            default: scope = default; return false;
        }
    }
}
