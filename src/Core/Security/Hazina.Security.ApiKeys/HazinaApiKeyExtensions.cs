using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

/// <summary>Fluent continuation of <see cref="HazinaApiKeyExtensions.AddHazinaApiKeyAuth"/>.</summary>
public interface IHazinaApiKeyBuilder
{
    IServiceCollection Services { get; }
}

internal sealed class HazinaApiKeyBuilder : IHazinaApiKeyBuilder
{
    public HazinaApiKeyBuilder(IServiceCollection services) => Services = services;
    public IServiceCollection Services { get; }
}

/// <summary>
/// One call in <c>Program.cs</c> (same pattern as AddHazinaSecurity):
/// <code>
/// builder.Services.AddHazinaApiKeyAuth(o => o.KeyPrefixBase = "iam").UseStore&lt;MyApiKeyStore&gt;();
/// app.UseHazinaApiKeyAuth();   // before UseAuthorization()
/// [Authorize(Policy = HazinaApiKeyPolicies.Write)]
/// </code>
/// </summary>
public static class HazinaApiKeyExtensions
{
    public const string AuthenticationScheme = "HazinaApiKey";

    public static IHazinaApiKeyBuilder AddHazinaApiKeyAuth(this IServiceCollection services, Action<HazinaApiKeyOptions>? configure = null)
    {
        var options = services.AddOptions<HazinaApiKeyOptions>();
        if (configure is not null) options.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.TryAddSingleton<CachingApiKeyLookup>();
        services.TryAddSingleton<IApiKeyCache>(sp => sp.GetRequiredService<CachingApiKeyLookup>());
        services.TryAddSingleton<IApiKeyValidator, ApiKeyValidator>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApiKeyAuditSink, LoggerApiKeyAuditSink>());

        // Scope + tenant policies for [Authorize(Policy = ...)].
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, ApiKeyScopeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, ApiKeyTenantHandler>());
        services.AddAuthorization(auth =>
        {
            AddPolicy(auth, HazinaApiKeyPolicies.Read, ApiKeyScope.Read);
            AddPolicy(auth, HazinaApiKeyPolicies.Write, ApiKeyScope.Write);
            AddPolicy(auth, HazinaApiKeyPolicies.Admin, ApiKeyScope.Admin);
        });

        // An authentication scheme so Challenge/Forbid behave (401/403) even in an app with no other scheme.
        // It only becomes the default when the app configured none of its own.
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthenticationScheme, null);
        services.AddOptions<AuthenticationOptions>().PostConfigure(auth =>
        {
            if (auth.DefaultScheme is null && auth.DefaultAuthenticateScheme is null && auth.DefaultChallengeScheme is null)
                auth.DefaultScheme = AuthenticationScheme;
        });

        ApiKeyRateLimiting.Configure(services);

        return new HazinaApiKeyBuilder(services);
    }

    private static void AddPolicy(AuthorizationOptions auth, string name, ApiKeyScope minimum) =>
        auth.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new ApiKeyScopeRequirement(minimum), new ApiKeyTenantRequirement()));

    /// <summary>Adds the API-key middleware (and, unless disabled in options, <c>UseRateLimiter()</c>). Place before <c>UseAuthorization()</c>.</summary>
    public static IApplicationBuilder UseHazinaApiKeyAuth(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<HazinaApiKeyOptions>>().Value;
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        if (options.RateLimitingEnabled && options.AddRateLimiterMiddleware)
            app.UseRateLimiter();
        return app;
    }

    // ---- key sources ------------------------------------------------------------------

    /// <summary>The app's own key table: read side only (validation).</summary>
    public static IHazinaApiKeyBuilder UseLookup<TLookup>(this IHazinaApiKeyBuilder builder) where TLookup : class, IApiKeyLookup
    {
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.AddScoped<IApiKeyLookup, TLookup>();
        return builder;
    }

    /// <summary>The app's own key table: read + write, so <see cref="IApiKeyManager"/> can issue/rotate/revoke.</summary>
    public static IHazinaApiKeyBuilder UseStore<TStore>(this IHazinaApiKeyBuilder builder) where TStore : class, IApiKeyStore
    {
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.RemoveAll<IApiKeyStore>();
        builder.Services.AddScoped<TStore>();
        builder.Services.AddScoped<IApiKeyStore>(sp => sp.GetRequiredService<TStore>());
        builder.Services.AddScoped<IApiKeyLookup>(sp => sp.GetRequiredService<TStore>());
        return builder;
    }

    public static IHazinaApiKeyBuilder UseInMemoryStore(this IHazinaApiKeyBuilder builder)
    {
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.RemoveAll<IApiKeyStore>();
        builder.Services.AddSingleton<InMemoryApiKeyStore>();
        builder.Services.AddSingleton<IApiKeyStore>(sp => sp.GetRequiredService<InMemoryApiKeyStore>());
        builder.Services.AddSingleton<IApiKeyLookup>(sp => sp.GetRequiredService<InMemoryApiKeyStore>());
        return builder;
    }

    /// <summary>Fixed keys from code or configuration (small apps without a key table). Raw values are hashed at startup.</summary>
    public static IHazinaApiKeyBuilder UseStaticKeys(this IHazinaApiKeyBuilder builder, params StaticApiKey[] keys)
    {
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.AddSingleton<IApiKeyLookup>(_ => new ConfigurationApiKeyLookup(keys));
        return builder;
    }

    /// <summary>Fixed keys read lazily from a configuration section holding an array of <see cref="StaticApiKey"/>.</summary>
    public static IHazinaApiKeyBuilder UseStaticKeys(this IHazinaApiKeyBuilder builder, IConfiguration section)
    {
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.AddSingleton<IApiKeyLookup>(_ =>
            new ConfigurationApiKeyLookup(section.Get<List<StaticApiKey>>() ?? new List<StaticApiKey>()));
        return builder;
    }

    /// <summary>
    /// Hybrid mode: validate against an authority (IAM) through <see cref="HttpApiKeyLookup"/>, with the
    /// <see cref="CachingApiKeyLookup"/> in front so the hot path stays local and IAM outages are survived.
    /// </summary>
    public static IHazinaApiKeyBuilder UseHttpLookup(this IHazinaApiKeyBuilder builder, Action<HttpApiKeyLookupOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddHttpClient(HttpApiKeyLookup.HttpClientName, (sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<HttpApiKeyLookupOptions>>().Value;
            if (string.IsNullOrWhiteSpace(o.BaseUrl))
                throw new InvalidOperationException("HttpApiKeyLookupOptions.BaseUrl is required.");
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = o.Timeout;
            if (!string.IsNullOrEmpty(o.ServiceApiKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation(o.HeaderName, o.ServiceApiKey);
        });
        builder.Services.RemoveAll<IApiKeyLookup>();
        builder.Services.AddSingleton<IApiKeyLookup, HttpApiKeyLookup>();
        return builder;
    }

    // ---- key issuing ------------------------------------------------------------------

    /// <summary>Raw keys go to Vault; registers <see cref="IApiKeyManager"/> (needs an <see cref="IApiKeyStore"/> from UseStore / UseInMemoryStore).</summary>
    public static IHazinaApiKeyBuilder UseVault(this IHazinaApiKeyBuilder builder, Action<VaultApiKeySecretVaultOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddHttpClient(VaultApiKeySecretVault.HttpClientName, (sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<VaultApiKeySecretVaultOptions>>().Value;
            if (string.IsNullOrWhiteSpace(o.BaseUrl))
                throw new InvalidOperationException("VaultApiKeySecretVaultOptions.BaseUrl is required.");
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = o.Timeout;
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", o.ApiKey);
        });
        builder.Services.TryAddSingleton<IApiKeySecretVault, VaultApiKeySecretVault>();
        builder.Services.TryAddScoped<IApiKeyManager, ApiKeyManager>();
        return builder;
    }

    /// <summary>Use your own vault (or a test double) instead of <see cref="VaultApiKeySecretVault"/>.</summary>
    public static IHazinaApiKeyBuilder UseSecretVault<TVault>(this IHazinaApiKeyBuilder builder) where TVault : class, IApiKeySecretVault
    {
        builder.Services.RemoveAll<IApiKeySecretVault>();
        builder.Services.AddSingleton<IApiKeySecretVault, TVault>();
        builder.Services.TryAddScoped<IApiKeyManager, ApiKeyManager>();
        return builder;
    }

    // ---- observers --------------------------------------------------------------------

    public static IHazinaApiKeyBuilder AddAuditSink<TSink>(this IHazinaApiKeyBuilder builder) where TSink : class, IApiKeyAuditSink
    {
        builder.Services.AddScoped<IApiKeyAuditSink, TSink>();
        return builder;
    }

    public static IHazinaApiKeyBuilder UseUsageRecorder<TRecorder>(this IHazinaApiKeyBuilder builder) where TRecorder : class, IApiKeyUsageRecorder
    {
        builder.Services.AddScoped<IApiKeyUsageRecorder, TRecorder>();
        return builder;
    }
}

/// <summary>Scheme handler: surfaces the principal the middleware built and gives Challenge/Forbid their 401/403.</summary>
internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Context.Items.ContainsKey(ApiKeyDefaults.RecordItemKey) && Context.User.IsApiKey()
            ? AuthenticateResult.Success(new AuthenticationTicket(Context.User, Scheme.Name))
            : AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
