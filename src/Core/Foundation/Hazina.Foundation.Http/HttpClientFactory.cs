// <copyright file="HttpClientFactory.cs" company="Prospergenics">
// Copyright (c) Prospergenics. All rights reserved.
// </copyright>

using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Hazina.Foundation.Http;

/// <summary>
/// Factory for creating HttpClient instances with optional SSL certificate validation bypass.
/// Useful for development scenarios with self-signed certificates.
/// </summary>
public static class HttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient with optional SSL certificate validation bypass based on configuration.
    /// </summary>
    /// <param name="configuration">Configuration provider to read SSL bypass setting.</param>
    /// <param name="configKey">Configuration key to check for bypass flag (default: "HttpClient:BypassSslValidation").</param>
    /// <param name="existingClient">Optional existing HttpClient to use if bypass is not needed.</param>
    /// <returns>HttpClient instance, either with or without SSL validation bypass.</returns>
    /// <remarks>
    /// Configuration example in appsettings.json:
    /// <code>
    /// {
    ///   "HttpClient": {
    ///     "BypassSslValidation": true
    ///   }
    /// }
    /// </code>
    /// WARNING: Only use SSL bypass in development/testing environments. Never in production.
    /// </remarks>
    public static HttpClient Create(
        IConfiguration? configuration = null,
        string configKey = "HttpClient:BypassSslValidation",
        HttpClient? existingClient = null)
    {
        bool bypassSsl = false;

        if (configuration != null)
        {
            try
            {
                var bypassValue = configuration[configKey];
                if (!string.IsNullOrWhiteSpace(bypassValue))
                {
                    bypassSsl = bool.TryParse(bypassValue, out var parsed) ? parsed : string.Equals(bypassValue, "1", StringComparison.Ordinal);
                }
            }
            catch (InvalidOperationException)
            {
                // If configuration read fails, default to secure (no bypass)
            }
        }

        return bypassSsl ? CreateWithSslBypass() : (existingClient ?? new HttpClient());
    }

    /// <summary>
    /// Creates an HttpClient that bypasses SSL certificate validation.
    /// </summary>
    /// <returns>HttpClient with SSL validation disabled.</returns>
    /// <remarks>
    /// WARNING: This should only be used in development/testing environments.
    /// Using this in production exposes your application to man-in-the-middle attacks.
    /// </remarks>
#pragma warning disable MA0039 // Do not write your own certificate validation method - intentional for development scenarios
#pragma warning disable CA5400 // Do not disable certificate validation - intentional for development scenarios
#pragma warning disable S4830 // Enable server certificate validation - intentional for development scenarios
#pragma warning disable CA2000 // Dispose objects before losing scope - HttpClient takes ownership of handler
    public static HttpClient CreateWithSslBypass()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler);
    }
#pragma warning restore CA2000
#pragma warning restore S4830
#pragma warning restore CA5400
#pragma warning restore MA0039

    /// <summary>
    /// Creates an HttpClient with standard SSL validation (secure).
    /// </summary>
    /// <returns>HttpClient with default SSL validation.</returns>
    public static HttpClient CreateSecure()
    {
        return new HttpClient();
    }

    /// <summary>
    /// Determines if SSL bypass is configured based on configuration settings.
    /// </summary>
    /// <param name="configuration">Configuration provider.</param>
    /// <param name="configKey">Configuration key to check (default: "HttpClient:BypassSslValidation").</param>
    /// <returns>True if SSL bypass is configured, false otherwise.</returns>
    public static bool IsSslBypassConfigured(
        IConfiguration? configuration,
        string configKey = "HttpClient:BypassSslValidation")
    {
        if (configuration == null)
        {
            return false;
        }

        try
        {
            var bypassValue = configuration[configKey];
            if (string.IsNullOrWhiteSpace(bypassValue))
            {
                return false;
            }

            return bool.TryParse(bypassValue, out var parsed) ? parsed : string.Equals(bypassValue, "1", StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
