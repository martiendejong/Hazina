using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hazina.Security.ApiKeys;

/// <summary>SHA-256 hashing of raw keys. Keys are 256-bit random, so a fast hash is the right tool (nothing to brute-force).</summary>
public static class ApiKeyHasher
{
    /// <summary>Lowercase hex SHA-256 of <paramref name="rawKey"/> (the format IAM already stores).</summary>
    public static string Hash(string rawKey)
    {
        ArgumentNullException.ThrowIfNull(rawKey);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }
}

/// <summary>A freshly minted key. <see cref="RawKey"/> must go to the caller (once) and Vault, never to a database or log.</summary>
public readonly record struct GeneratedApiKey(string RawKey, string KeyPrefix, string KeyHash);

public static partial class ApiKeyGenerator
{
    private const string PrefixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    [GeneratedRegex(@"^[a-z0-9]{1,8}$")]
    private static partial Regex PrefixBaseRegex();

    // "<base>_<4 alnum>_<token>" - the token is >= 32 url-safe chars (43 for the 32 random bytes we generate).
    [GeneratedRegex(@"^(?<prefix>[a-z0-9]{1,8}_[a-z0-9]{4}_)[A-Za-z0-9_-]{32,}$")]
    private static partial Regex KeyShapeRegex();

    /// <summary>Generate a new key "{prefixBase}_{4 random chars}_{43 url-safe chars of 256-bit entropy}".</summary>
    public static GeneratedApiKey Generate(string prefixBase = "hzn")
    {
        if (!PrefixBaseRegex().IsMatch(prefixBase))
            throw new ArgumentException("Prefix base must be 1-8 lowercase alphanumeric characters.", nameof(prefixBase));

        var prefix = $"{prefixBase}_{RandomAlphanumeric(4)}_";
        return GenerateWithPrefix(prefix);
    }

    /// <summary>New key material under an existing prefix (rotation keeps the prefix so the key stays identifiable).</summary>
    public static GeneratedApiKey GenerateWithPrefix(string keyPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        var rawKey = keyPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new GeneratedApiKey(rawKey, keyPrefix, ApiKeyHasher.Hash(rawKey));
    }

    /// <summary>
    /// Extract the non-secret prefix from a presented key, but ONLY when it has the shape of a key we
    /// mint. Anything else (a stray password pasted into the header, random garbage) yields no prefix,
    /// so audit logs can never end up holding the first characters of an arbitrary secret.
    /// </summary>
    public static bool TryExtractPrefix(string? presentedKey, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrEmpty(presentedKey) || presentedKey.Length > 256) return false;
        var match = KeyShapeRegex().Match(presentedKey);
        if (!match.Success) return false;
        prefix = match.Groups["prefix"].Value;
        return true;
    }

    private static string RandomAlphanumeric(int length)
    {
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = PrefixAlphabet[RandomNumberGenerator.GetInt32(PrefixAlphabet.Length)];
        return new string(result);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
