# Hazina.DynamicAPI - Usage Guide

## Wat is dit?

Hazina.DynamicAPI is een systeem waarmee AI agents **elke API kunnen aanroepen zonder dat je van tevoren de API hoeft te configureren**. De agent kan zelf:

1. API documentatie zoeken op internet
2. De documentatie lezen en begrijpen
3. API calls maken met automatische authenticatie
4. De responses verwerken

## Waarom is dit bruut?

**Traditionele aanpak:**
```csharp
// Je moet voor elke API een aparte tool maken:
var stripeClient = new StripeClient(apiKey);
var googleAnalytics = new GoogleAnalyticsClient(credentials);
var salesforce = new SalesforceClient(username, password);
// etc... voor ELKE API die je wilt gebruiken
```

**Dynamic API aanpak:**
```csharp
// ÉÉN tool die ALLE APIs kan aanroepen:
var apiClient = new DynamicAPIClient(credStore);

// Agent kan nu ELKE API bellen:
await apiClient.Get("https://api.stripe.com/v1/customers", "stripe");
await apiClient.Get("https://www.googleapis.com/analytics/v3/data/ga", "google");
await apiClient.Post("https://api.salesforce.com/services/data/v50.0/sobjects/Account", data, "salesforce");
```

## Kern Componenten

### 1. CredentialStore - Centraal Credential Beheer

```csharp
var credStore = new CredentialStore("./credentials");

// Opslaan
await credStore.StoreCredential("stripe", "api_key", "sk_test_...");
await credStore.StoreCredential("google", "client_secret", "...");

// Ophalen (automatisch uit file of env var)
var apiKey = await credStore.GetCredential("stripe", "api_key");

// Alle services weergeven
var services = credStore.ListServices(); // ["stripe", "google", "bing"]
```

**Credential Files:**
```
./credentials/
  ├── stripe.json       → {"api_key": "sk_test_..."}
  ├── google.json       → {"client_id": "...", "client_secret": "..."}
  └── bing.json         → {"api_key": "..."}
```

### 2. WebSearchTool - Zoek API Documentatie

```csharp
var searchTool = new WebSearchTool(credStore);

// Zoek documentatie
var results = await searchTool.SearchApiDocumentation("Stripe", "payment intents");

// Results:
// - Stripe: Create a PaymentIntent - https://stripe.com/docs/api/payment_intents/create
// - Stripe: PaymentIntent object - https://stripe.com/docs/api/payment_intents/object
// etc...

// Lees volledige documentatie
var docContent = await searchTool.FetchUrl(results.Results[0].Url);
```

### 3. DynamicAPIClient - Roep APIs Aan

```csharp
var apiClient = new DynamicAPIClient(credStore);

// GET request
var response = await apiClient.Get(
    "https://api.stripe.com/v1/customers",
    serviceName: "stripe"  // Injecteert automatisch Bearer token
);

// POST request met body
var createResponse = await apiClient.Post(
    "https://api.stripe.com/v1/payment_intents",
    new {
        amount = 2000,
        currency = "usd",
        payment_method_types = new[] { "card" }
    },
    serviceName: "stripe"
);

// Response handling
if (response.IsSuccess)
{
    var json = JsonDocument.Parse(response.Body);
    // Verwerk data...
}
```

## Gebruik met Hazina Agents

### Setup: Maak Tools aan

```csharp
using Hazina.DynamicAPI.Core;
using Hazina.DynamicAPI.Tools;

// Initialize
var credStore = new CredentialStore("./credentials");
var apiClient = new DynamicAPIClient(credStore);
var searchTool = new WebSearchTool(credStore);

// Maak tools context
var tools = new ToolsContext();
tools.Add(new WebSearchHazinaTool(searchTool));      // web_search
tools.Add(new FetchUrlHazinaTool(searchTool));       // fetch_url
tools.Add(new DynamicAPIHazinaTool(apiClient));      // api_call

// Maak agent met deze tools
var agent = new HazinaAgent(
    name: "api_researcher",
    generator: generator,
    tools: tools
);
```

### Voorbeeld: Agent Workflow

**User vraagt:**
> "Haal mijn laatste Stripe betalingen op"

**Agent doet:**

```
1. web_search("Stripe API list payments")

   Vindt: https://stripe.com/docs/api/payment_intents/list

2. fetch_url("https://stripe.com/docs/api/payment_intents/list")

   Leest: "GET /v1/payment_intents - Requires Bearer authentication"

3. api_call({
     "url": "https://api.stripe.com/v1/payment_intents",
     "method": "GET",
     "service_name": "stripe"
   })

   → Gebruikt automatisch credentials uit CredentialStore
   → Injecteert Bearer token
   → Retourneert payment data

4. Verwerkt response en geeft resultaat aan user
```

## Complete Voorbeeld: Stripe Integration

```csharp
// 1. Setup credentials
var credStore = new CredentialStore("./credentials");
await credStore.StoreCredential("stripe", "api_key", "sk_test_YOUR_KEY");

// 2. Setup tools
var apiClient = new DynamicAPIClient(credStore);
var searchTool = new WebSearchTool(credStore);

var tools = new ToolsContext();
tools.Add(new WebSearchHazinaTool(searchTool));
tools.Add(new DynamicAPIHazinaTool(apiClient));

// 3. Maak agent
var agent = new HazinaAgent("payment_agent", generator, tools);

// 4. Stel vraag
var response = await agent.Generator.GetResponse(
    "Maak een Stripe payment intent voor €20 en vertel me de client secret",
    cancellationToken
);

// Agent zal:
// - Stripe API docs zoeken
// - Begrijpen hoe payment intents werken
// - POST request maken naar /v1/payment_intents
// - Client secret uit response halen
// - Antwoord geven
```

## Authentication Types

### Bearer Token (meest gebruikt)

```csharp
// credentials/stripe.json
{
  "api_key": "sk_test_..."
}

// Gebruik
await apiClient.Get(url, serviceName: "stripe");
// → Adds: Authorization: Bearer sk_test_...
```

### API Key in Custom Header

```csharp
// credentials/rapidapi.json
{
  "api_key": "your_key",
  "api_key_header": "X-RapidAPI-Key"
}

// Gebruik
var request = new ApiRequest {
    Url = url,
    ServiceName = "rapidapi",
    AuthType = AuthenticationType.ApiKey
};
// → Adds: X-RapidAPI-Key: your_key
```

### Basic Authentication

```csharp
// credentials/myservice.json
{
  "username": "user@example.com",
  "password": "secret"
}

// Gebruik
var request = new ApiRequest {
    Url = url,
    ServiceName = "myservice",
    AuthType = AuthenticationType.Basic
};
// → Adds: Authorization: Basic base64(user:pass)
```

### OAuth2 Access Token

```csharp
// credentials/google.json
{
  "access_token": "ya29.a0..."
}

// Gebruik
var request = new ApiRequest {
    Url = url,
    ServiceName = "google",
    AuthType = AuthenticationType.OAuth2
};
// → Adds: Authorization: Bearer ya29.a0...
```

## Tool Parameters voor Agents

### web_search

```json
{
  "query": "Stripe API create payment",
  "count": 10
}
```

### fetch_url

```json
{
  "url": "https://stripe.com/docs/api/payment_intents/create"
}
```

### api_call

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

## Best Practices

### 1. Credential Opslag

```csharp
// ✅ GOOD: Gebruik CredentialStore
var credStore = new CredentialStore("./credentials");
await credStore.StoreCredential("stripe", "api_key", apiKey);

// ❌ BAD: Hardcoded credentials
var apiClient = new HttpClient();
apiClient.DefaultRequestHeaders.Add("Authorization", "Bearer sk_test_...");
```

### 2. Agent Instructions

```markdown
# API Researcher Agent Instructions

## Workflow
1. ALWAYS search for API documentation FIRST using web_search
2. READ the documentation using fetch_url
3. IDENTIFY the correct endpoint, method, and required parameters
4. MAKE the API call using api_call with appropriate authentication
5. VERIFY the response and handle errors

## Example
User: "Get Stripe customer data"

Steps:
1. web_search("Stripe API list customers")
2. fetch_url(found documentation URL)
3. Learn: GET /v1/customers with Bearer auth
4. api_call with service_name="stripe"
5. Parse and return customer list
```

### 3. Error Handling

```csharp
var response = await apiClient.Get(url, serviceName);

if (response.IsSuccess)
{
    // Parse response.Body
}
else
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
    Console.WriteLine($"Status: {response.StatusCode}");
    Console.WriteLine($"Body: {response.Body}");
}
```

## Security

### Credentials NEVER in Git

```gitignore
# .gitignore
credentials/
*.json
.env
```

### Aparte Keys voor Dev/Prod

```
Development:
  ./credentials/stripe.json → sk_test_...

Production:
  Environment variable: STRIPE_API_KEY=sk_live_...
```

### Read-Only Keys waar mogelijk

```json
// credentials/stripe.json
{
  "api_key": "sk_test_...",     // Voor development
  "restricted_key": "rk_test_..." // Read-only key voor queries
}
```

## Vergelijking: Voor vs Na

### VOOR (Traditional)

```csharp
// Voor elke API een aparte implementatie nodig:

public class StripeService
{
    private readonly StripeClient _client;

    public async Task<List<Customer>> GetCustomers()
    {
        return await _client.Customers.ListAsync();
    }

    public async Task<PaymentIntent> CreatePayment(decimal amount)
    {
        return await _client.PaymentIntents.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = "usd"
        });
    }
}

public class GoogleAnalyticsService { /* ... */ }
public class SalesforceService { /* ... */ }
// etc... VOOR ELKE API
```

### NA (Dynamic)

```csharp
// ÉÉN client voor ALLE APIs:

var credStore = new CredentialStore("./credentials");
var apiClient = new DynamicAPIClient(credStore);

// Stripe
await apiClient.Get("https://api.stripe.com/v1/customers", "stripe");
await apiClient.Post("https://api.stripe.com/v1/payment_intents", data, "stripe");

// Google Analytics
await apiClient.Get("https://www.googleapis.com/analytics/v3/data/ga", "google");

// Salesforce
await apiClient.Post("https://api.salesforce.com/services/data/v50.0/sobjects/Account", data, "salesforce");

// ELKE API werkt hetzelfde!
```

## Wanneer Gebruiken?

### ✅ Perfect voor:
- AI agents die verschillende APIs moeten aanroepen
- Prototype/MVP development (snel nieuwe APIs toevoegen)
- Data aggregatie uit meerdere bronnen
- Research/exploration van APIs

### ⚠️ Minder geschikt voor:
- Mission-critical production systems (gebruik typed clients)
- Complexe OAuth flows (gebruik dedicated libraries)
- Zeer hoge performance requirements (overhead van dynamic calls)

## Volgende Stappen

1. **Installeer Bing Search API key**
   - Nodig voor web_search functionaliteit
   - https://www.microsoft.com/en-us/bing/apis/bing-web-search-api

2. **Configureer je eerste service**
   ```csharp
   var credStore = new CredentialStore("./credentials");
   await credStore.StoreCredential("stripe", "api_key", "sk_test_...");
   ```

3. **Test met agent**
   ```csharp
   var response = await agent.Generator.GetResponse(
       "Find Stripe API docs and get my customer list",
       cancel
   );
   ```

4. **Bekijk voorbeelden**
   - `Examples/DynamicAPIExample.cs`
   - `README.md`

## Support

Vragen? Check:
- `README.md` - Volledige documentatie
- `Examples/DynamicAPIExample.cs` - Code voorbeelden
- `ANALYSIS_data-analytics-agent.md` - Inspiratie achter dit project
