using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys.Tests;

internal sealed class StubHandler : HttpMessageHandler
{
    public List<(HttpMethod Method, Uri Uri, string Body, HttpRequestMessage Request)> Requests { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, body, request));
        return Respond(request);
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public StubHttpClientFactory(StubHandler handler, string baseUrl, Action<HttpClient>? configure = null)
    {
        _client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(baseUrl) };
        configure?.Invoke(_client);
    }

    public HttpClient CreateClient(string name) => _client;
}

public class HttpApiKeyLookupTests
{
    private static (HttpApiKeyLookup Lookup, StubHandler Handler) Create()
    {
        var handler = new StubHandler();
        var factory = new StubHttpClientFactory(handler, "https://iam.test/",
            c => c.DefaultRequestHeaders.Add("X-Api-Key", "service-key"));
        return (new HttpApiKeyLookup(factory, Options.Create(new HttpApiKeyLookupOptions { BaseUrl = "https://iam.test/" })), handler);
    }

    [Fact]
    public async Task SendsOnlyTheHash_ToTheIntrospectionEndpoint_AndMapsTheAnswer()
    {
        var (lookup, handler) = Create();
        var generated = ApiKeyGenerator.Generate("tst");
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new ApiKeyIntrospection
            {
                KeyId = "k1", KeyPrefix = generated.KeyPrefix, Name = "svc", Scope = "write", TenantId = "t1",
                Permissions = { "a:b" }, Claims = { ["email"] = new[] { "x@y.z" } },
            }), Encoding.UTF8, "application/json"),
        };

        var record = await lookup.FindByHashAsync(generated.KeyHash);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://iam.test/api/api-keys/introspect", request.Uri.ToString());
        Assert.Contains(generated.KeyHash, request.Body);
        Assert.DoesNotContain(generated.RawKey, request.Body);
        Assert.Equal("service-key", request.Request.Headers.GetValues("X-Api-Key").Single());

        Assert.NotNull(record);
        Assert.Equal(generated.KeyHash, record!.KeyHash);
        Assert.Equal(ApiKeyScope.Write, record.Scope);
        Assert.Equal("t1", record.TenantId);
        Assert.Contains("a:b", record.Permissions);
        Assert.Contains(record.ExtraClaims, c => c.Type == "email" && c.Value == "x@y.z");
    }

    [Fact]
    public async Task NotFound_IsAnUnknownKey()
    {
        var (lookup, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.Null(await lookup.FindByHashAsync("abc"));
    }

    [Fact]
    public async Task ServerError_Throws_SoTheCacheCanServeStale()
    {
        var (lookup, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<HttpRequestException>(() => lookup.FindByHashAsync("abc"));
    }

    [Fact]
    public void UnknownScopeFromTheAuthority_NeverWidensAccess()
    {
        var record = new ApiKeyIntrospection { KeyId = "k", KeyPrefix = "x_abcd_", Scope = "superuser" }.ToRecord("hash");

        Assert.Equal(ApiKeyScope.Read, record.Scope);
    }
}

public class VaultApiKeySecretVaultTests
{
    private static (VaultApiKeySecretVault Vault, StubHandler Handler) Create()
    {
        var handler = new StubHandler();
        var factory = new StubHttpClientFactory(handler, "https://vault.test/");
        var vault = new VaultApiKeySecretVault(factory, Options.Create(new VaultApiKeySecretVaultOptions
        {
            BaseUrl = "https://vault.test/", ApiKey = "vault-key", ProjectId = 42, Environment = "prod",
        }));
        return (vault, handler);
    }

    private static ApiKeySecretEntry Entry(string? existing = null) =>
        new("k1", "ci runner", "hzn_ab12_", "hzn_ab12_RAWSECRETRAWSECRETRAWSECRETRAWSECRET0", "t1", existing);

    [Fact]
    public async Task Create_PostsTheRawKeyAsAnApiKeyCredential_AndReturnsTheCredentialId()
    {
        var (vault, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"id":314,"name":"x"}""", Encoding.UTF8, "application/json") };

        var reference = await vault.StoreAsync(Entry());

        Assert.Equal("314", reference);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://vault.test/api/projects/42/credentials", request.Uri.ToString());
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("hzn_ab12_RAWSECRETRAWSECRETRAWSECRETRAWSECRET0", body.RootElement.GetProperty("password").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("type").GetInt32()); // CredentialType.ApiKey
        Assert.Equal("hzn_ab12_", body.RootElement.GetProperty("username").GetString());
        Assert.DoesNotContain("RAWSECRET", body.RootElement.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task Update_PutsToTheExistingCredential()
    {
        var (vault, handler) = Create();

        var reference = await vault.StoreAsync(Entry(existing: "314"));

        Assert.Equal("314", reference);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://vault.test/api/projects/42/credentials/314", request.Uri.ToString());
    }

    [Fact]
    public async Task Delete_TreatsAlreadyGoneAsFine_AndOtherFailuresAsErrors()
    {
        var (vault, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        await vault.DeleteAsync("314");

        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
        await Assert.ThrowsAsync<HttpRequestException>(() => vault.DeleteAsync("314"));
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Delete, r.Method));
    }

    [Fact]
    public async Task Failure_DoesNotEchoTheResponseBody()
    {
        var (vault, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("stack trace with SECRET-IN-BODY") };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => vault.StoreAsync(Entry()));

        Assert.DoesNotContain("SECRET-IN-BODY", ex.Message);
        Assert.DoesNotContain("RAWSECRET", ex.ToString());
    }
}

public class GeneratorAndLookupTests
{
    [Fact]
    public void Generate_ProducesHighEntropyPrefixedKeys_WhoseHashMatches()
    {
        var a = ApiKeyGenerator.Generate("iam");
        var b = ApiKeyGenerator.Generate("iam");

        Assert.Matches(@"^iam_[a-z0-9]{4}_[A-Za-z0-9_-]{43}$", a.RawKey);
        Assert.NotEqual(a.RawKey, b.RawKey);
        Assert.Equal(ApiKeyHasher.Hash(a.RawKey), a.KeyHash);
        Assert.Matches("^[0-9a-f]{64}$", a.KeyHash);
        Assert.StartsWith(a.KeyPrefix, a.RawKey);
        Assert.DoesNotContain(a.RawKey, a.KeyPrefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TOOLONGPREFIXX")]
    [InlineData("has space")]
    public void Generate_RejectsBadPrefixBases(string prefixBase) =>
        Assert.Throws<ArgumentException>(() => ApiKeyGenerator.Generate(prefixBase));

    [Theory]
    [InlineData("hunter2", false)]
    [InlineData("", false)]
    [InlineData("iam_ab12_short", false)]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiJ9.e30.abc", false)]
    public void TryExtractPrefix_OnlyForKeysShapedLikeOurs(string presented, bool expected) =>
        Assert.Equal(expected, ApiKeyGenerator.TryExtractPrefix(presented, out _));

    [Fact]
    public void TryExtractPrefix_ReturnsThePrefixOfAGeneratedKey()
    {
        var g = ApiKeyGenerator.Generate("iam");

        Assert.True(ApiKeyGenerator.TryExtractPrefix(g.RawKey, out var prefix));
        Assert.Equal(g.KeyPrefix, prefix);
    }

    [Fact]
    public async Task ConfigurationLookup_HashesKeys_SkipsUnsetPlaceholders_AndNeverExposesTheRawKey()
    {
        var lookup = new ConfigurationApiKeyLookup(new[]
        {
            new StaticApiKey { RawKey = "super-secret-static-key", Name = "mcp", Scope = ApiKeyScope.Admin, Claims = { ["role"] = new[] { "admin" } } },
            new StaticApiKey { RawKey = "  ", Name = "unset" },
        });

        Assert.Equal(1, lookup.Count);
        var record = await lookup.FindByHashAsync(ApiKeyHasher.Hash("super-secret-static-key"));
        Assert.NotNull(record);
        Assert.Equal(ApiKeyScope.Admin, record!.Scope);
        Assert.Null(record.TenantId); // platform key
        Assert.DoesNotContain("super-secret", record.KeyPrefix + record.Id + record.Name);
        Assert.Contains(record.ExtraClaims, c => c.Type == "role" && c.Value == "admin");
        Assert.Null(await lookup.FindByHashAsync(ApiKeyHasher.Hash("other")));
    }
}
