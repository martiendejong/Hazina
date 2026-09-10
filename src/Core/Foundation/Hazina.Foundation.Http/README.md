# Hazina.Foundation.Http

HTTP utilities for the Hazina framework, providing centralized HttpClient creation with configurable SSL certificate validation bypass.

## Features

- **Configurable SSL Bypass**: Enable/disable SSL certificate validation via configuration
- **Environment-Aware**: Configuration-driven approach for different environments
- **Multiple Creation Methods**: Factory methods for different scenarios
- **Safe Defaults**: Secure by default (SSL validation enabled)

## Installation

```bash
dotnet add package Hazina.Foundation.Http
```

## Usage

### Basic Usage with Configuration

```csharp
using Hazina.Foundation.Http;
using Microsoft.Extensions.Configuration;

// Read from appsettings.json
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Create HttpClient with configuration-based SSL bypass
var httpClient = HttpClientFactory.Create(configuration);

// Make request
var response = await httpClient.GetAsync("https://localhost:5001/api/data");
```

### Configuration Setup

Add to your `appsettings.Development.json`:

```json
{
  "HttpClient": {
    "BypassSslValidation": true
  }
}
```

For production (`appsettings.json`), either omit the setting or set it to `false`:

```json
{
  "HttpClient": {
    "BypassSslValidation": false
  }
}
```

### Explicit SSL Bypass (Development Only)

```csharp
// Create HttpClient with SSL bypass for localhost/self-signed certs
var httpClient = HttpClientFactory.CreateWithSslBypass();

// ⚠️ WARNING: Only use in development/testing!
```

### Secure Client (Production)

```csharp
// Create HttpClient with standard SSL validation
var httpClient = HttpClientFactory.CreateSecure();
```

### Check if SSL Bypass is Configured

```csharp
if (HttpClientFactory.IsSslBypassConfigured(configuration))
{
    Console.WriteLine("⚠️ SSL validation is bypassed - development mode");
}
```

### Custom Configuration Key

```csharp
// Use a different configuration key
var httpClient = HttpClientFactory.Create(
    configuration,
    configKey: "MyService:IgnoreSslErrors"
);
```

## Use Cases

### 1. Development with Localhost APIs

```csharp
// appsettings.Development.json
{
  "HttpClient": {
    "BypassSslValidation": true
  },
  "ApiBaseUrl": "https://localhost:5001"
}

// Code
var client = HttpClientFactory.Create(configuration);
var data = await client.GetStringAsync(configuration["ApiBaseUrl"] + "/api/data");
```

### 2. Service with Configurable SSL Behavior

```csharp
public class MyApiClient
{
    private readonly HttpClient _httpClient;

    public MyApiClient(IConfiguration configuration)
    {
        _httpClient = HttpClientFactory.Create(configuration);
    }

    public async Task<string> GetDataAsync()
    {
        return await _httpClient.GetStringAsync("https://api.example.com/data");
    }
}
```

### 3. Updating Existing Services

**Before:**
```csharp
public class WordPressService
{
    private readonly HttpClient _httpClient;

    public WordPressService()
    {
        // Hardcoded logic
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        _httpClient = new HttpClient(handler);
    }
}
```

**After:**
```csharp
using Hazina.Foundation.Http;

public class WordPressService
{
    private readonly HttpClient _httpClient;

    public WordPressService(IConfiguration configuration)
    {
        _httpClient = HttpClientFactory.Create(configuration);
    }
}
```

## Security Considerations

⚠️ **WARNING**: SSL certificate validation bypass should **NEVER** be used in production environments!

### When to Use SSL Bypass
- ✅ Local development with self-signed certificates
- ✅ Testing environments with invalid certificates
- ✅ Development APIs on localhost
- ✅ Internal testing scenarios

### When NOT to Use SSL Bypass
- ❌ Production environments
- ❌ Public-facing services
- ❌ Handling sensitive data
- ❌ Third-party API integration in production

### Best Practices
1. **Environment-Specific Configuration**: Use `appsettings.Development.json` for bypass settings
2. **Never Commit Production Configs with Bypass**: Ensure `appsettings.json` has bypass disabled
3. **Code Reviews**: Flag any SSL bypass in production code
4. **CI/CD Validation**: Add checks to prevent deploying with SSL bypass enabled

## API Reference

### `HttpClientFactory.Create()`
Creates an HttpClient with optional SSL bypass based on configuration.

**Parameters:**
- `configuration` (IConfiguration?): Configuration provider
- `configKey` (string): Configuration key to check (default: "HttpClient:BypassSslValidation")
- `existingClient` (HttpClient?): Existing client to reuse if bypass not needed

**Returns:** HttpClient instance

### `HttpClientFactory.CreateWithSslBypass()`
Creates an HttpClient that bypasses SSL certificate validation.

**Returns:** HttpClient with SSL validation disabled

⚠️ **WARNING**: Development/testing only!

### `HttpClientFactory.CreateSecure()`
Creates an HttpClient with standard SSL validation (secure).

**Returns:** HttpClient with default SSL validation

### `HttpClientFactory.IsSslBypassConfigured()`
Determines if SSL bypass is configured.

**Parameters:**
- `configuration` (IConfiguration?): Configuration provider
- `configKey` (string): Configuration key to check (default: "HttpClient:BypassSslValidation")

**Returns:** bool - True if SSL bypass is configured, false otherwise

## Migration Guide

### From WordpressBaseService Pattern

**Before:**
```csharp
bool useInsecureClient = false;
try
{
    var insecureValue = configuration["UseInsecureWordpressClient"];
    if (!string.IsNullOrWhiteSpace(insecureValue))
    {
        useInsecureClient = bool.TryParse(insecureValue, out var parsed)
            ? parsed
            : (insecureValue == "1");
    }
}
catch { }

_httpClient = httpClient ?? (useInsecureClient ? CreateInsecureClient() : new HttpClient());

private static HttpClient CreateInsecureClient()
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return new HttpClient(handler);
}
```

**After:**
```csharp
using Hazina.Foundation.Http;

_httpClient = httpClient ?? HttpClientFactory.Create(
    configuration,
    configKey: "UseInsecureWordpressClient"
);
```

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please ensure:
1. All methods have XML documentation
2. Security warnings are prominent
3. Unit tests cover all factory methods
4. Configuration examples are provided

## Support

For issues or questions:
- GitHub Issues: https://github.com/prospergenics/devgpt/issues
- Documentation: https://docs.hazina.ai
