using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// Per-key rate limiting on top of Microsoft.AspNetCore.RateLimiting (no hand-rolled limiter): a
/// sliding-window limiter partitioned by key id, chained into <see cref="RateLimiterOptions.GlobalLimiter"/>
/// next to whatever limiter the app already has. Requests that are not API-key authenticated get no
/// partition here and are left to the app's own limiters.
/// </summary>
internal static class ApiKeyRateLimiting
{
    internal const int SegmentsPerWindow = 6;

    internal static void Configure(IServiceCollection services)
    {
        services.AddRateLimiter(_ => { }); // make sure the rate limiter services exist

        // PostConfigure runs after every app-level Configure, so we wrap the app's own GlobalLimiter/OnRejected instead of racing them.
        services.AddOptions<RateLimiterOptions>().PostConfigure<IOptions<HazinaApiKeyOptions>>((limiter, keyOptions) =>
        {
            var options = keyOptions.Value;
            if (!options.RateLimitingEnabled) return;

            var apiKeyLimiter = CreateLimiter(options);
            limiter.GlobalLimiter = limiter.GlobalLimiter is null
                ? apiKeyLimiter
                : PartitionedRateLimiter.CreateChained(limiter.GlobalLimiter, apiKeyLimiter);

            // The framework default rejection status is 503; a throttled client should see 429.
            if (limiter.RejectionStatusCode == StatusCodes.Status503ServiceUnavailable)
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            var previous = limiter.OnRejected;
            limiter.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                if (http.Items.ContainsKey(ApiKeyDefaults.RecordItemKey))
                {
                    http.Items[ApiKeyDefaults.AuditOutcomeItemKey] = ApiKeyAuditOutcome.RateLimited;
                    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    // The limiter does not always report when a permit frees up; fall back to one window segment.
                    var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                        : (int)(TimeSpan.FromMinutes(1).TotalSeconds / SegmentsPerWindow);
                    http.Response.Headers.RetryAfter = Math.Max(1, retryAfterSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    http.Response.ContentType = "application/json";
                    await http.Response.WriteAsync("""{"error":"Rate limit exceeded","message":"Too many requests for this API key. Please try again later."}""", ct);
                }
                else if (previous is not null)
                {
                    await previous(context, ct);
                }
            };
        });
    }

    private static PartitionedRateLimiter<HttpContext> CreateLimiter(HazinaApiKeyOptions options) =>
        PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            if (http.Items[ApiKeyDefaults.RecordItemKey] is not ApiKeyRecord key)
                return RateLimitPartition.GetNoLimiter("hazina-apikey:none");

            var limit = key.RateLimitPerMinute is > 0 ? key.RateLimitPerMinute.Value : options.DefaultRequestsPerMinute;
            return RateLimitPartition.GetSlidingWindowLimiter($"hazina-apikey:{key.Id}:{limit}", _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = SegmentsPerWindow,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });
}
