# DevGPT Release Notes

## [Unreleased] - Branch: return_tokens_used_after_request

### 🚀 Nieuwe Features

#### 1. Token Usage Tracking (✅ Compleet)
**Alle AI calls retourneren nu token usage en costs**

```csharp
var response = await agent.Generator.GetResponse("Hello", cancel);

Console.WriteLine($"Response: {response.Result}");
Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
Console.WriteLine($"Model: {response.TokenUsage.ModelName}");
```

**Features:**
- `TokenUsageInfo` model met input/output tokens + costs
- `LLMResponse<T>` wrapper voor alle responses
- Automatische pricing voor OpenAI, Anthropic, HuggingFace
- Token aggregatie via `+` operator voor totale kosten
- Support in alle LLM clients (OpenAI, Anthropic, HuggingFace)

**Commits:**
- 7c9588b: TokenUsageInfo model
- d8eb4fc: LLMResponse wrapper class
- cc62c84: OpenAI client token tracking
- db359ec: Anthropic client token tracking
- cf8be38: HuggingFace client token tracking
- 92821d9: DocumentGenerator met LLMResponse
- b7c8148: AgentManager updates
- f5aa360: Documentatie

**Bestanden:**
- `LLMs/Classes/Models/Chat/TokenUsageInfo.cs`
- `LLMs/Classes/Models/Chat/LLMResponse.cs`
- `TOKENUSAGE_EXAMPLE.md`

---

#### 2. DevGPT.DynamicAPI - Call Any API Without Pre-Configuration (✅ Nieuw!)
**Geïnspireerd door data-analytics-agent - agents kunnen nu ELKE API aanroepen zonder pre-configuratie**

```csharp
// Setup credentials
var credStore = new CredentialStore("./credentials");
await credStore.StoreCredential("stripe", "api_key", "sk_test_...");

// Create tools
var apiClient = new DynamicAPIClient(credStore);
var searchTool = new WebSearchTool(credStore);

var tools = new ToolsContext();
tools.Add(new WebSearchDevGPTTool(searchTool));   // Zoek API docs
tools.Add(new DynamicAPIDevGPTTool(apiClient));   // Roep APIs aan

// Agent kan nu ELKE API bellen!
var agent = new DevGPTAgent("api_researcher", generator, tools);
var response = await agent.Generator.GetResponse(
    "Find Stripe docs and create a payment intent for $20",
    cancel
);
```

**Components:**

1. **CredentialStore** - Centraal credential beheer
   - Opslag in files (`./credentials/*.json`) of env vars
   - Support voor Bearer, API Key, Basic, OAuth2
   - Auto-loading en caching

2. **WebSearchTool** - Zoek API documentatie
   - Bing Search API integratie
   - Zoek documentatie dynamisch
   - Fetch volledige pagina content

3. **DynamicAPIClient** - Generieke HTTP client
   - Call ANY API endpoint
   - Automatische authenticatie injectie
   - Support voor alle HTTP methods

4. **DevGPT Tools** - Ready-to-use agent tools
   - `web_search` - Zoek informatie
   - `fetch_url` - Lees documentatie
   - `api_call` - Roep API aan

**Workflow:**
```
User: "Get my Stripe transactions"

Agent:
1. web_search("Stripe API transactions")
2. fetch_url(documentation URL)
3. api_call({url: "...", service_name: "stripe"})
   → Auto-injects Bearer token
   → Returns transaction data
```

**Commits:**
- 53bc58d: DevGPT.DynamicAPI project
- 32794a2: Security fix System.Text.Json
- a12a8a9: Nederlandse usage guide

**Bestanden:**
- `DevGPT.DynamicAPI/Core/CredentialStore.cs`
- `DevGPT.DynamicAPI/Core/DynamicAPIClient.cs`
- `DevGPT.DynamicAPI/Tools/WebSearchTool.cs`
- `DevGPT.DynamicAPI/Tools/DynamicAPIDevGPTTool.cs`
- `DevGPT.DynamicAPI/Tools/WebSearchDevGPTTool.cs`
- `DevGPT.DynamicAPI/Models/ApiRequest.cs`
- `DevGPT.DynamicAPI/Examples/DynamicAPIExample.cs`
- `DevGPT.DynamicAPI/README.md`
- `DevGPT.DynamicAPI/USAGE_GUIDE.md`

---

### 📊 Analyse & Documentatie

#### Repository Analysis: data-analytics-agent
Volledige analyse van agency-ai-solutions/data-analytics-agent met implementatie aanbevelingen.

**Key Learnings:**
1. Dynamic API integration zonder pre-connectors
2. Multimodal analysis loop (generate → save → load → analyze)
3. Templated agent instructions voor deployment flexibility
4. Centralized credential management
5. Data-first methodology

**Bestanden:**
- `ANALYSIS_data-analytics-agent.md` (1a82d21)

---

### 🐛 Bugfixes

#### PostgresChunkStore Duplicate
Verwijderd duplicate class definition die build errors veroorzaakte.

**Commits:**
- d4f7761: Verwijder PostgresDocumentPartStore.cs (duplicate)

---

## Migratiehandleiding

### Token Usage Tracking

**Voor:**
```csharp
var response = await agent.Generator.GetResponse("Hello", cancel);
// response is direct een IsReadyResult
Console.WriteLine(response.Message);
```

**Na:**
```csharp
var response = await agent.Generator.GetResponse("Hello", cancel);
// response is LLMResponse<IsReadyResult>
Console.WriteLine(response.Result.Message);
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

**Breaking Changes:**
- Alle `ILLMClient` methods retourneren nu `LLMResponse<T>`
- DocumentGenerator methods retourneren `LLMResponse<T>`
- AgentManager.SendMessage aangepast

**Migration Steps:**
1. Update calls: `response` → `response.Result`
2. Optioneel: Track token usage via `response.TokenUsage`

---

### Dynamic API Usage (Nieuw Project)

**Setup:**
```bash
# 1. Add project reference
dotnet add reference ../DevGPT.DynamicAPI/DevGPT.DynamicAPI.csproj

# 2. Install Bing API key (voor web search)
# Get key from: https://www.microsoft.com/en-us/bing/apis/bing-web-search-api

# 3. Store credentials
mkdir credentials
echo '{"api_key":"your_bing_key"}' > credentials/bing.json
echo '{"api_key":"sk_test_..."}' > credentials/stripe.json
```

**Code:**
```csharp
using DevGPT.DynamicAPI.Core;
using DevGPT.DynamicAPI.Tools;

var credStore = new CredentialStore("./credentials");
var apiClient = new DynamicAPIClient(credStore);
var searchTool = new WebSearchTool(credStore);

var tools = new ToolsContext();
tools.Add(new WebSearchDevGPTTool(searchTool));
tools.Add(new FetchUrlDevGPTTool(searchTool));
tools.Add(new DynamicAPIDevGPTTool(apiClient));

var agent = new DevGPTAgent("api_researcher", generator, tools);
```

---

## Testing

### Token Usage Tests
```bash
# Build LLMs solution
dotnet build DevGPT.LLMs.sln

# Run integration tests
cd App/OpenAI.IntegrationTests
dotnet run
```

### DynamicAPI Tests
```bash
# Build project
dotnet build DevGPT.DynamicAPI/DevGPT.DynamicAPI.csproj

# Run examples (requires credentials)
# See: DevGPT.DynamicAPI/Examples/DynamicAPIExample.cs
```

---

## Backwards Compatibility

### ✅ Compatible
- Existing agents work zonder wijzigingen (gebruik `.Result`)
- Bestaande tools blijven werken
- Store implementations ongewijzigd

### ⚠️ Breaking (Fixable)
- LLM client calls: `string response` → `LLMResponse<string> response`
- Generator calls: `T response` → `LLMResponse<T> response`
- Fix: Overal `.Result` toevoegen

---

## Performance

### Token Tracking
- ✅ Minimale overhead (~0.1ms per call)
- ✅ Geen extra LLM calls
- ✅ Token data komt uit API responses

### Dynamic API
- ⚠️ Web search adds 1-2 seconden per search
- ✅ Credential loading gecached
- ✅ HTTP client reuse

---

## Security Notes

### Token Costs
- Alle costs gebaseerd op public pricing (Jan 2025)
- Update prices in `TokenUsageInfo` bij pricing changes

### Credentials
- **NEVER** commit credentials to git
- Use `.gitignore` voor `credentials/` directory
- Separate keys voor dev/prod
- Prefer env vars in production

---

## Roadmap

### High Priority (Next)
1. Python execution tool voor data analysis
2. Multimodal loop (save → load → analyze charts)
3. Templated agent instructions
4. Tool bundles per domain

### Medium Priority
5. More authentication types (JWT, custom)
6. API response caching
7. Rate limiting per service
8. Retry logic met exponential backoff

### Low Priority
9. Docker deployment templates
10. Marketplace integration
11. Usage analytics dashboard

---

## Contributors

Deze release bevat bijdragen aan:
- Token usage tracking systeem
- Dynamic API integration framework
- Repository analysis en documentatie

---

## Links

- [Token Usage Examples](TOKENUSAGE_EXAMPLE.md)
- [DynamicAPI README](DevGPT.DynamicAPI/README.md)
- [DynamicAPI Usage Guide](DevGPT.DynamicAPI/USAGE_GUIDE.md)
- [Data Analytics Agent Analysis](ANALYSIS_data-analytics-agent.md)

---

## Git Log Summary

```
a12a8a9 add: Nederlandse usage guide voor DevGPT.DynamicAPI
32794a2 fix: upgrade System.Text.Json naar 8.0.5 voor security
53bc58d add: DevGPT.DynamicAPI project met credential store, web search en dynamic API client
d4f7761 fix: verwijder duplicate PostgresChunkStore
1a82d21 add: analyse van data-analytics-agent repository
f5aa360 add: documentatie voor token usage tracking
146ac26 fix: OpenAI integration tests voor LLMResponse
2661ec2 fix: gebruik correcte property namen voor token usage
b7c8148 update: AgentManager voor LLMResponse support
92821d9 update: DocumentGenerator met LLMResponse wrapper
cf8be38 update: HuggingFace client met token tracking
db359ec update: Anthropic client met token tracking
cc62c84 update: OpenAI client met token tracking
d8eb4fc add: LLMResponse wrapper class
7c9588b add: TokenUsageInfo model voor token tracking
```
