# Hazina.DynamicAPI

Dynamic API Integration for Hazina - Call any API without pre-configuration, inspired by the [data-analytics-agent](https://github.com/agency-ai-solutions/data-analytics-agent) repository.

## Features

### 🔑 Centralized Credential Management
- Store credentials in JSON files or environment variables
- Automatic credential loading for API calls
- Support for multiple authentication types (Bearer, API Key, Basic, OAuth2)
- Secure, file-based storage in `./credentials/` directory

### 🌐 Web Search Integration
- Search for API documentation using Bing Search API
- Fetch and read documentation from discovered URLs
- Help agents discover API endpoints dynamically

### 🚀 Dynamic API Client
- Call ANY HTTP API without pre-configuration
- Automatic authentication injection
- Support for GET, POST, PUT, DELETE, PATCH methods
- JSON request/response handling
- Query parameters and custom headers

### 🤖 Hazina Tool Integration
- Ready-to-use `HazinaChatTool` wrappers
- Seamless integration with Hazina agents
- Tools: `web_search`, `fetch_url`, `api_call`

## Installation

Add reference to your project:

```xml
<ProjectReference Include="..\Hazina.DynamicAPI\Hazina.DynamicAPI.csproj" />
```

## Quick Start

### 1. Setup Credentials

Create credentials directory and store API keys:

```csharp
var credStore = new CredentialStore("./credentials");

// Store Stripe credentials
await credStore.StoreCredential("stripe", "api_key", "sk_test_...");

// Or use environment variables
// STRIPE_API_KEY=sk_test_... dotnet run
```

Credential files are stored as JSON:
```json
// ./credentials/stripe.json
{
  "api_key": "sk_test_...",
  "webhook_secret": "whsec_..."
}
```

### 2. Make Direct API Calls

```csharp
var apiClient = new DynamicAPIClient(credStore);

// Public API (no auth)
var response = await apiClient.Get("https://api.github.com/repos/microsoft/typescript");

// Authenticated API call (auto-injects Bearer token from credentials)
var stripeResponse = await apiClient.Get(
    "https://api.stripe.com/v1/customers",
    serviceName: "stripe"
);

// POST with body
var createResponse = await apiClient.Post(
    "https://api.stripe.com/v1/payment_intents",
    new { amount = 2000, currency = "usd" },
    serviceName: "stripe"
);
```

### 3. Search for API Documentation

```csharp
var searchTool = new WebSearchTool(credStore);

// Search for documentation
var docs = await searchTool.SearchApiDocumentation("Stripe", "create payment");

foreach (var result in docs.Results)
{
    Console.WriteLine($"{result.Title}: {result.Url}");
}

// Fetch full documentation
var content = await searchTool.FetchUrl(docs.Results.First().Url);
```

### 4. Use with Hazina Agents

```csharp
// Create tools
var credStore = new CredentialStore("./credentials");
var apiClient = new DynamicAPIClient(credStore);
var searchTool = new WebSearchTool(credStore);

var tools = new ToolsContext();
tools.Add(new WebSearchHazinaTool(searchTool));
tools.Add(new FetchUrlHazinaTool(searchTool));
tools.Add(new DynamicAPIHazinaTool(apiClient));

// Create agent with dynamic API capabilities
var agent = new HazinaAgent("api_researcher", generator, tools);

// Agent can now discover and call APIs dynamically!
var response = await agent.Generator.GetResponse(
    "Find the Stripe API documentation for creating a payment intent, then create a $20 USD payment intent",
    cancel
);
```

## Agent Workflow Example

When you ask an agent with these tools to interact with an API:

```
User: "Get my latest Stripe transactions"

Agent Workflow:
1. web_search("Stripe API list transactions")
   → Finds: https://stripe.com/docs/api/balance_transactions/list

2. fetch_url("https://stripe.com/docs/api/balance_transactions/list")
   → Learns: GET /v1/balance_transactions with Bearer auth

3. api_call({
     url: "https://api.stripe.com/v1/balance_transactions",
     method: "GET",
     service_name: "stripe"
   })
   → Returns transaction data with auto-injected authentication
```

## Tools Reference

### `web_search`

Search the web for information and documentation.

**Parameters:**
- `query` (string, required): Search query
- `count` (number, optional): Number of results (1-50, default: 5)

**Example:**
```json
{
  "query": "Stripe API create payment intent",
  "count": 10
}
```

### `fetch_url`

Fetch and read content from a URL.

**Parameters:**
- `url` (string, required): URL to fetch

**Example:**
```json
{
  "url": "https://stripe.com/docs/api/payment_intents/create"
}
```

### `api_call`

Make HTTP requests to any API endpoint.

**Parameters:**
- `url` (string, required): Full API endpoint URL
- `method` (string, optional): HTTP method (GET, POST, PUT, DELETE, PATCH)
- `service_name` (string, optional): Service name for credential lookup
- `headers` (object, optional): Custom HTTP headers
- `query_params` (object, optional): URL query parameters
- `body` (object, optional): Request body for POST/PUT/PATCH
- `auth_type` (string, optional): Authentication type (none, bearer, api_key, basic, oauth2)

**Example:**
```json
{
  "url": "https://api.stripe.com/v1/payment_intents",
  "method": "POST",
  "service_name": "stripe",
  "body": {
    "amount": 2000,
    "currency": "usd"
  }
}
```

## Authentication Types

### Bearer Token (default)
Adds `Authorization: Bearer <token>` header.
```csharp
// Requires: credentials/{service}/api_key
await apiClient.Get(url, serviceName: "stripe");
```

### API Key
Adds custom header with API key.
```csharp
// Requires: credentials/{service}/api_key and api_key_header
var request = new ApiRequest {
    AuthType = AuthenticationType.ApiKey,
    ServiceName = "rapidapi"
};
```

### Basic Auth
Adds `Authorization: Basic <base64(username:password)>` header.
```csharp
// Requires: credentials/{service}/username and password
var request = new ApiRequest {
    AuthType = AuthenticationType.Basic,
    ServiceName = "myservice"
};
```

### OAuth2
Adds `Authorization: Bearer <access_token>` header.
```csharp
// Requires: credentials/{service}/access_token
var request = new ApiRequest {
    AuthType = AuthenticationType.OAuth2,
    ServiceName = "google"
};
```

## Credential Storage

### File-based (Recommended for development)
```
./credentials/
  ├── stripe.json
  ├── google_analytics.json
  ├── bing.json
  └── ...
```

### Environment Variables
```bash
# Format: {SERVICE}_API_KEY or {SERVICE}_{KEY}
export STRIPE_API_KEY=sk_test_...
export BING_API_KEY=your_bing_key
export GOOGLE_CLIENT_SECRET=your_secret
```

### Mixed Approach
Credentials are loaded in this order:
1. In-memory cache
2. JSON file in `./credentials/{service}.json`
3. Environment variable `{SERVICE}_{KEY}`

## Security Best Practices

1. ✅ **Never commit credentials to git**
   ```gitignore
   credentials/
   *.json
   ```

2. ✅ **Use separate credentials for dev/prod**
   - Development: `./credentials/`
   - Production: Environment variables

3. ✅ **Rotate credentials regularly**
   ```csharp
   await credStore.StoreCredential("stripe", "api_key", "new_key");
   credStore.ClearCache(); // Force reload
   ```

4. ✅ **Limit API key permissions**
   - Only grant necessary scopes
   - Use read-only keys when possible

## Examples

See `Examples/DynamicAPIExample.cs` for complete examples:
- Credential storage and retrieval
- Direct API calls
- Web search for documentation
- Agent integration
- Complete workflows

Run examples:
```csharp
await DynamicAPIExample.RunExamples();
```

## Requirements

- .NET 8.0+
- Bing Search API key (for web search functionality)
- API credentials for services you want to call

## Architecture

```
Hazina.DynamicAPI/
├── Core/
│   ├── CredentialStore.cs       # Credential management
│   └── DynamicAPIClient.cs      # HTTP client with auth
├── Tools/
│   ├── WebSearchTool.cs         # Bing search integration
│   ├── DynamicAPIHazinaTool.cs  # API call tool for agents
│   ├── WebSearchHazinaTool.cs   # Search tool for agents
│   └── FetchUrlHazinaTool.cs    # URL fetcher for agents
├── Models/
│   └── ApiRequest.cs            # Request/response models
└── Examples/
    └── DynamicAPIExample.cs     # Usage examples
```

## Inspiration

Inspired by the [data-analytics-agent](https://github.com/agency-ai-solutions/data-analytics-agent) approach to dynamic API integration, where agents discover and call APIs without pre-built connectors.

## License

Part of the Hazina project.
