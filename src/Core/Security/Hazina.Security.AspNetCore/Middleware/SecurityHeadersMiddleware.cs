using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hazina.Security.AspNetCore.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses
/// Protects against common web vulnerabilities (XSS, clickjacking, MIME sniffing, etc.)
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        SecurityHeadersOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        var headers = context.Response.Headers;

        if (_options.EnableStrictTransportSecurity)
        {
            headers.Append("Strict-Transport-Security",
                $"max-age={_options.HstsMaxAge}; includeSubDomains; preload");
        }

        if (_options.EnableXContentTypeOptions)
        {
            headers.Append("X-Content-Type-Options", "nosniff");
        }

        if (_options.EnableXFrameOptions)
        {
            headers.Append("X-Frame-Options", _options.XFrameOptionsValue);
        }

        if (_options.EnableXXssProtection)
        {
            headers.Append("X-XSS-Protection", "1; mode=block");
        }

        if (_options.EnableReferrerPolicy)
        {
            headers.Append("Referrer-Policy", _options.ReferrerPolicyValue);
        }

        if (_options.EnableContentSecurityPolicy && !string.IsNullOrEmpty(_options.ContentSecurityPolicy))
        {
            headers.Append("Content-Security-Policy", _options.ContentSecurityPolicy);
        }

        if (_options.EnablePermissionsPolicy && !string.IsNullOrEmpty(_options.PermissionsPolicy))
        {
            headers.Append("Permissions-Policy", _options.PermissionsPolicy);
        }

        // Remove headers that leak information
        if (_options.RemoveServerHeader)
        {
            headers.Remove("Server");
        }

        if (_options.RemoveXPoweredByHeader)
        {
            headers.Remove("X-Powered-By");
        }

        _logger.LogDebug("[SECURITY HEADERS] Applied security headers to {Path}", context.Request.Path);

        await _next(context);
    }
}

/// <summary>
/// Configuration options for security headers
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Enable Strict-Transport-Security (HSTS) header
    /// </summary>
    public bool EnableStrictTransportSecurity { get; set; } = true;

    /// <summary>
    /// HSTS max-age in seconds (default: 1 year)
    /// </summary>
    public int HstsMaxAge { get; set; } = 31536000;

    /// <summary>
    /// Enable X-Content-Type-Options header (prevents MIME sniffing)
    /// </summary>
    public bool EnableXContentTypeOptions { get; set; } = true;

    /// <summary>
    /// Enable X-Frame-Options header (prevents clickjacking)
    /// </summary>
    public bool EnableXFrameOptions { get; set; } = true;

    /// <summary>
    /// X-Frame-Options value (DENY, SAMEORIGIN, ALLOW-FROM uri)
    /// </summary>
    public string XFrameOptionsValue { get; set; } = "DENY";

    /// <summary>
    /// Enable X-XSS-Protection header
    /// </summary>
    public bool EnableXXssProtection { get; set; } = true;

    /// <summary>
    /// Enable Referrer-Policy header
    /// </summary>
    public bool EnableReferrerPolicy { get; set; } = true;

    /// <summary>
    /// Referrer-Policy value
    /// </summary>
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Enable Content-Security-Policy header
    /// </summary>
    public bool EnableContentSecurityPolicy { get; set; } = true;

    /// <summary>
    /// Content-Security-Policy value
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>
    /// Enable Permissions-Policy header
    /// </summary>
    public bool EnablePermissionsPolicy { get; set; } = true;

    /// <summary>
    /// Permissions-Policy value
    /// </summary>
    public string PermissionsPolicy { get; set; } =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    /// <summary>
    /// Remove Server header
    /// </summary>
    public bool RemoveServerHeader { get; set; } = true;

    /// <summary>
    /// Remove X-Powered-By header
    /// </summary>
    public bool RemoveXPoweredByHeader { get; set; } = true;
}
