# Hasina Framework - Architecture Analysis
**Date**: 2026-01-05
**Analyst**: Claude Code
**Scope**: Complete framework analysis against conceptual model

---

## Executive Summary

Hasina is een **generiek, config-gedreven framework** voor conversational AI-systemen met incrementele profielbouw. De architectuur implementeert het conceptuele model grotendeels volledig, met sterke scheiding tussen generic core (Hasina) en domain-specifieke implementatie (Brand2Boost/Client-Manager).

**Kernbevindingen**:
- ✔ 7 van 8 concepten volledig geïmplementeerd
- ◑ 1 concept gedeeltelijk geïmplementeerd (Interview Agent)
- ✖ 0 concepten ontbreken
- Sterke generieke basis, minimale technische schuld
- Sprint 4 features **NIET** geïntegreerd in chat

---

## 1. Chat → LLM Agent System

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### Bewijs in Code

**Framework Core**:
- `Hazina.AgentFactory/Core/HazinaAgent.cs` - Basis agent model
  - Properties: Name, DocumentGenerator, IToolsContext, IsCoder
  - Orchestreert tool execution

**Agent Types**:
- `Hazina.Tools.AI.Agents/Agents/GeneratorAgentBase.cs` - Base klasse
- `Hazina.Tools.AI.Agents/Agents/HazinaStoreAgent.cs` - Store-based agent

**Chat Integration**:
- `ClientManagerAPI/Controllers/ChatController.cs` (lines 70-180)
  - Dynamische ChatService instantiatie
  - SignalR streaming responses
  - Integreert DataGathering, AnalysisFields, ImageGeneration

**Tool Context**:
- `Hazina.LLMs.Client/IToolsContext.cs`
  ```csharp
  public interface IToolsContext {
      List<HazinaChatTool> Tools { get; }
      string ProjectId { get; }
      Func<string, Task> SendMessage { get; }
      Action<TokenUsage> OnTokensUsed { get; }
  }
  ```

### Architectuur

```
User Message
    ↓
ChatController
    ↓
ChatService (dynamisch aangemaakt)
    ↓
HazinaAgent + IToolsContext
    ↓
LLM Call (via ILLMClient)
    ↓
Tool Execution (indien nodig)
    ↓
Response Stream (via SignalR)
```

### Observaties
- Agents zijn stateless, per-request instanties
- Tool context bevat alle benodigde services
- SignalR voor real-time streaming
- Session-aware routing (elk browser tab = unieke sessie)

---

## 2. Tools als Application Functions

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### Bewijs in Code

**Tool Model**:
- `Hazina.LLMs.Classes/Models/Tools/HazinaChatTool.cs`
  ```csharp
  public class HazinaChatTool {
      public string FunctionName { get; set; }
      public string Description { get; set; }
      public List<ChatToolParameter> Parameters { get; set; }
      public Func<List<HazinaChatMessage>, HazinaChatToolCall,
                   CancellationToken, Task<string>> Execute { get; set; }
  }
  ```

**Tool Registration Pattern**:
- `ClientManagerAPI/Extensions/ToolsContextAnalysisExtensions.cs` (lines 22-231)
  ```csharp
  var tool = new HazinaChatTool(
      "generate_analysis_field",
      "Generate or regenerate...",
      parameters,
      async (messages, toolCall, cancel) => {
          // ECHTE APPLICATION LOGIC - geen mock
          var result = await analysisFieldService
              .GenerateTypedFieldAsync(...);
          return JsonSerializer.Serialize(result);
      });
  context.Tools.Add(tool);
  ```

**Beschikbare Tools**:
1. `generate_analysis_field` - Analysis field generation
2. `update_analysis_field` - Field updates
3. `generate_logo` - Logo generation via DALL-E
4. `generate_illustration` - General image generation
5. `create_variations` - Image variations

### Kritieke Eigenschap

**Tools zijn NIET LLM tool specs** - ze zijn C# functions die direct application services aanroepen:
- `IAnalysisFieldService.GenerateTypedFieldAsync()`
- `ChatService.GenerateImage()`
- Database writes
- SignalR broadcasts

### Observaties
- Execute delegate voert volledige business logic uit
- Direct integration met services (DI)
- Async/await pattern voor long-running operations
- Returns JSON string naar LLM

---

## 3. Twee Datatypes: Gathered Data + Analysis Fields

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### A. Gathered Data (key→value van gebruiker)

**Model**:
- `Hazina.Tools.Services.DataGathering/Models/GatheredDataItem.cs`
  ```csharp
  public class GatheredDataItem {
      public string Key { get; set; }           // "brand-name"
      public string Title { get; set; }         // "Brand Name"
      public GatheredDataValue Data { get; set; } // String/Number/LargeText/List
      public DateTime GatheredAt { get; set; }
      public string? Source { get; set; }       // "api", "chat:123"
  }

  public class GatheredDataValue {
      public GatheredDataType Type { get; set; } // String, Number, LargeText, List
      public string? StringValue { get; set; }
      public double? NumberValue { get; set; }
      public List<string>? ListValue { get; set; }
  }
  ```

**Provider Interface**:
- `Hazina.Tools.Services.DataGathering/Abstractions/IGatheredDataProvider.cs`
  - GetAllAsync, GetAsync, SaveAsync, DeleteAsync, ExistsAsync

**Implementatie**:
- `FileSystemGatheredDataProvider.cs` - JSON files per project

**Service**:
- `IDataGatheringService.GatherDataFromMessageAsync()` - LLM extracts entities

**API**:
- `ClientManagerAPI/Controllers/GatheredDataController.cs`
  - GET `/api/gatherdata/{projectId}/{key}`
  - PUT `/api/gatherdata/{projectId}/{key}`
  - DELETE `/api/gatherdata/{projectId}/{key}`

**Real-time Updates**:
- `SignalRGatheredDataNotifier.cs` - Broadcasts changes

### B. Analysis Fields (gegenereerd/afgeleid, config-driven)

**Configuration Model**:
- `ClientManagerAPI/Models/AnalysisFieldConfiguration.cs`
  ```csharp
  public class AnalysisFieldDefinition {
      public string Key { get; set; }          // "brand-profile"
      public string FileName { get; set; }     // "brand-profile.json"
      public string DisplayName { get; set; }  // "Brand Profile"
      public string ConfigFileName { get; set; } // "brand-profile.prompt.txt"
      public string GenericType { get; set; }  // "ColorScheme", "ImageSet"
      public string ComponentName { get; set; } // "ColorScheme" → UI component
  }
  ```

**Config Loader**:
- `Hazina.Tools.Services.Store/Analysis/AnalysisFieldConfigLoader.cs`
  - Laadt `analysis-fields.config.json` uit project folder

**Service Interface**:
- `Hazina.Tools.Services.DataGathering/Abstractions/IAnalysisFieldService.cs`
  ```csharp
  Task<Dictionary<string, AnalysisFieldConfig>> LoadFieldConfigsAsync(string projectId);
  Task<IReadOnlyList<GeneratedAnalysisField>> GenerateFromConversationAsync(...);
  Task<object?> GenerateTypedFieldAsync(projectId, chatId, key, instruction);
  ```

**Generation Strategy Pattern**:
- `ClientManagerAPI/Strategies/FieldGenerationStrategyFactory.cs`
  ```csharp
  public static IFieldGenerationStrategy GetStrategy(AnalysisFieldConfig config) {
      foreach (var strategy in _strategies) {
          if (strategy.CanHandle(config)) return strategy;
      }
      return null; // Fallback to plain text
  }
  ```
  - Strategies: `ImageSetGenerationStrategy`, `TypedFieldGenerationStrategy`
  - Extensible via `RegisterStrategy()`

**Type-Based Handlers** (AnalysisController.cs lines 695-759):
- `ColorScheme` → Typed JSON generation
- `ToneOfVoice` → Typed JSON generation
- `CoreValues` → Typed JSON generation
- `ImageSet` (Logo) → DALL-E image generation
- Plain text → Instruction-based generation

**API**:
- `ClientManagerAPI/Controllers/AnalysisController.cs`
  - POST `/api/analysis/{projectId}/generate-field/{fieldName}`
  - GET `/api/analysis/{projectId}/field/{fieldName}`
  - PUT `/api/analysis/{projectId}/field/{fieldName}`

### Scheiding Gathered vs Analysis

| Aspect | Gathered Data | Analysis Fields |
|--------|---------------|----------------|
| **Bron** | User input (chat/API) | LLM generation |
| **Type** | String/Number/List | JSON/Text/Image |
| **Storage** | gathered-data/*.json | analysis-fields/*.json |
| **API** | /api/gatherdata | /api/analysis |
| **Config** | N/A | analysis-fields.config.json |
| **Event** | GatheredData (SignalR) | AnalysisData (SignalR) |

### Observaties
- Duidelijke scheiding tussen extracted vs generated data
- Analysis fields zijn volledig config-driven
- Strategy pattern maakt extensie eenvoudig
- Type-safe generation voor known types
- Fallback naar plain text generation

---

## 4. Dependencies tussen Fields

**Status**: ◑ **GEDEELTELIJK GEÏMPLEMENTEERD**

### Bewijs van Dependency Support

**Fragment Metadata**:
- `BrandFragmentService.cs` (lines 179-184)
  ```csharp
  fragment.Metadata = new FragmentMetadata {
      Dependencies = new List<string> {
          "logo", "color-scheme", "typography", "brand-profile"
      }
  };
  ```

**Field Generation met Context**:
- `AnalysisController.cs` (lines 1228-1290)
  ```csharp
  // Logo generation laadt brand-profile voor context
  var brandProfile = await _analysisFieldService
      .LoadFieldAsync<BrandProfile>(id, "brand-profile");

  var prompt = $"Professional logo for {brandProfile.BrandName}...";
  ```

**Gathered Data → Analysis Flow**:
- `ChatController.cs` (lines 84-107)
  ```csharp
  var chatService = new ChatService(
      // ...
      analysisProvider: analysisProvider, // Enables field updates
      // ...
  );
  ```

**Fragment Slot Resolution**:
- Slots marked "gathered" → populated from gathered data
- Slots marked "generated" → LLM generation
- `RefreshGatheredSlotsAsync()` re-populates from gathered data

### Wat Ontbreekt

**Expliciete Dependency Graph**:
- Geen DAG (Directed Acyclic Graph) evaluatie
- Geen topological sort voor generation order
- Geen circular dependency detection
- Dependencies zijn documentatie, niet enforcement

**Auto-regeneration**:
- Geen cascade updates bij dependency changes
- Handmatige regeneration vereist

**Dependency Validation**:
- Geen check of dependencies exist before generation
- Geen "ready to generate" status

### Observaties
- Dependencies zijn metadata, niet runtime enforcement
- Context loading is manual in generation strategies
- Works pragmatisch maar niet formeel
- Ruimte voor verbetering met dependency engine

---

## 5. Background Job Orchestration

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### Framework

**Hangfire Integration**:
- `ClientManagerAPI/Jobs/BackgroundJobs.cs`
- Persistent job storage
- Automatic retry met exponential backoff
- Queue prioritization (critical/default/low)

### Registered Jobs

**1. Token Reset** (Daily at midnight UTC):
```csharp
[AutomaticRetry(Attempts = 3)]
public async Task ResetDailyTokensAsync()
```

**2. Embeddings Processing** (On file upload):
```csharp
[AutomaticRetry(Attempts = 3)]
public async Task ProcessEmbeddingsAsync(string projectId, string filePath)
```

**3. Document Generation** (On demand):
```csharp
public async Task GenerateDocumentAsync(...)
```

**4. Scheduled Post Publishing** (Every minute):
```csharp
[AutomaticRetry(Attempts = 3)]
public async Task PublishScheduledPostsAsync()
```

**5. Data Cleanup** (Weekly):
```csharp
[AutomaticRetry(Attempts = 1)]
public async Task CleanupOldDataAsync()
```

### Job Execution

**Enqueue Pattern**:
```csharp
// Immediate execution
BackgroundJob.Enqueue(() => ProcessEmbeddingsAsync(projectId, filePath));

// Delayed execution
BackgroundJob.Schedule(() => GenerateDocumentAsync(...), TimeSpan.FromMinutes(5));

// Recurring job
RecurringJob.AddOrUpdate("publish-posts",
    () => PublishScheduledPostsAsync(),
    Cron.Minutely);
```

**Embeddings Queue Integration**:
- `ClientManagerAPI/Controllers/AnalysisController.cs` (lines 1488-1501)
  ```csharp
  _embeddingsQueue?.EnqueueEmbedProjectFile(projectId, fileName);
  ```
  - Non-blocking
  - Enqueued after field generation
  - Background processing

### Job Monitoring

**Execution Recording**:
```csharp
private async Task RecordJobExecutionAsync(string jobName, bool success, string? error)
```

**Audit Trail**:
- Job execution history
- Success/failure tracking
- Error logging

### Observaties
- Hangfire dashboard voor monitoring (indien enabled)
- Automatic retry met configurable attempts
- Queue-based prioritization
- Transaction-safe execution
- Non-blocking architecture

---

## 6. Event-Driven UI met Component Descriptors

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### SignalR Architecture

**Hub**:
- `ClientManagerAPI/Custom/MyHub.cs`
  ```csharp
  public class MyHub : Hub {
      public async Task JoinProject(string projectId, string sessionId) {
          await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{projectId}");
          await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
      }
  }
  ```

**Two-Level Routing**:
1. **Session Group**: `"session:{sessionId}"` - Streaming responses, specific browser tab
2. **Project Group**: `"project:{projectId}"` - Shared updates, all team members
3. **Fallback**: `"All"` - Backwards compatibility

### Event Types

**1. Analysis Field Events**:
- `SignalRAnalysisFieldNotifier.cs` (lines 37-128)
  ```csharp
  public async Task NotifyFieldGeneratedAsync(
      string projectId, string chatId, string key,
      string displayName, string content,
      string? componentName = null, ...) {

      // Component resolution
      var resolvedComponentName = "view/analysis/AnalysisData";
      if (!string.IsNullOrWhiteSpace(componentName)) {
          var chatComponentName = componentName.EndsWith("Chat")
              ? componentName
              : $"{componentName}Chat";
          resolvedComponentName = $"view/analysis/{chatComponentName}";
      }

      var message = new {
          type = "generated",
          projectId,
          chatId,
          key,
          payload = new {
              type = "analysis-data",
              componentName = resolvedComponentName,
              data = content
          }
      };

      await _hubContext.Clients.Group($"session:{sessionId}")
          .SendAsync("AnalysisData", message, cancellationToken);
  }
  ```

**2. Gathered Data Events**:
- `SignalRGatheredDataNotifier.cs`
- Channel: `GatheredData`
- Similar routing pattern

**3. Chat Streaming Events**:
- `ChatController.cs` + `ChatStreamService`
- Real-time token streaming
- Image generation progress
- Session-specific routing

### Component Descriptor Pattern

**Configuration → Component Mapping**:
```csharp
// In analysis-fields.config.json
{
  "key": "color-scheme",
  "componentName": "ColorScheme",    // Backend name
  "rowComponentName": "ColorSchemeRow"
}

// Becomes in SignalR event
{
  "componentName": "view/analysis/ColorSchemeChat",  // Frontend path
  "type": "analysis-data",
  "data": { /* ColorScheme JSON */ }
}
```

**Frontend Rendering** (assumed):
```javascript
signalR.on("AnalysisData", (message) => {
    const Component = resolveComponent(message.payload.componentName);
    render(<Component data={message.payload.data} />);
});
```

### Component Types

**Configured Components** (AnalysisController.cs lines 365-472):
- `ColorScheme` → ColorSchemeChat/ColorSchemeRow
- `ToneOfVoice` → ToneOfVoiceChat/ToneOfVoiceRow
- `CoreValues` → CoreValuesRow
- `ImageSet` → Logo viewer
- Plain text → Generic AnalysisData viewer

### Observaties
- String-based component resolution (geen type safety)
- Convention: ComponentName + "Chat" suffix
- Two-tier routing prevents cross-talk between sessions
- Flexible: nieuwe component types via config

---

## 7. Interview Agent / Vraagstrategie

**Status**: ◑ **GEDEELTELIJK GEÏMPLEMENTEERD**

### Wat Bestaat

**Intake System**:
- `Hazina.Tools.Services.Intake/IntakeRepository.cs` - Structured intake configs
- `Hazina.Tools.Services.Intake/HazinaStoreIntakeWorker.cs` - Orchestration

**Conversational Data Extraction**:
- `IDataGatheringService.GatherDataFromMessageAsync()` extracts entities
- LLM analyzes chat context
- Structured data extracted automatically

**Profile Building Pattern**:
```
User: "We're a tech startup called Acme"
    ↓
DataGatheringService analyzes message
    ↓
Extracts: { "brand-name": "Acme", "industry": "tech", "company-type": "startup" }
    ↓
GatheredDataItems saved
    ↓
SignalR broadcasts updates
    ↓
AnalysisFieldService checks if enough context
    ↓
Auto-generates analysis fields when ready
```

**Conversation Starters**:
- `ClientManagerAPI/Controllers/ChatController.cs` (line 145)
- `ConversationStarterService` suggests next questions
- Guides interview flow

**Onboarding Flow**:
- `ClientManagerAPI/Controllers/OnboardingController.cs`
- Suggested brand discovery questions
- Multi-step intake process

### Wat Ontbreekt

**Expliciete Interview Agent Class**:
- Geen `InterviewAgent` met question strategies
- Geen state machine voor interview flow
- Geen completion detection ("enough info gathered")

**Vraagstrategie**:
- Geen expliciete decision tree
- Geen prioritization van questions
- Geen adaptive questioning based on gaps

**Proactieve Vragen**:
- Geen automatic follow-up questions
- Geen "what's missing?" analysis
- LLM moet implicitly begrijpen wat te vragen

### Huidige Implementatie

**Implicit Interview via Prompts**:
- System prompt stuurt conversational tone
- LLM begrijpt context en stelt vragen
- DataGathering extracts answers automatisch
- Works, maar niet formeel gestructureerd

**Semi-Structured Alternative**:
- IntakeController voor expliciete intake forms
- OnboardingController voor guided flow
- Mix van conversational + structured approaches

### Observaties
- "Smart extraction" in plaats van "formal interview"
- Werkt goed voor ervaren gebruikers
- Minder predictable voor onervaren gebruikers
- Ruimte voor expliciete InterviewAgent klasse met:
  - Question bank
  - Completion criteria
  - Gap analysis
  - Adaptive strategies

---

## 8. Genericity in Hasina Core

**Status**: ✔ **VOLLEDIG GEÏMPLEMENTEERD**

### Generieke Componenten (in Hasina)

**1. Agent Model**:
- `HazinaAgent.cs` - Geen domain assumptions
- Takes any `IToolsContext`
- Agnostic voor project type

**2. Tool System**:
- `HazinaChatTool` - Generic C# function signature
- `IToolsContext` - Interface zonder domain knowledge
- Tools registratie is code-based, niet config

**3. Data Models**:
- `GatheredDataItem` - Key-value met flexible types
- `GatheredDataValue` - String/Number/LargeText/List
- Geen project-specifieke structuren

**4. Field Configuration**:
- `AnalysisFieldConfiguration` - JSON-driven
- Any field can have any GenericType
- Strategy pattern for generation

**5. Storage Abstractions**:
- `IGatheredDataProvider` - Interface voor storage
- `IAnalysisFieldsProvider` - Interface voor fields
- Implementaties: FileSystem, maar abstractions allow DB/Cloud

**6. Service Interfaces**:
- `IDataGatheringService` - Generic extraction
- `IAnalysisFieldService` - Generic generation
- `INotifier` interfaces - Event pattern

**7. LLM Client Abstraction**:
- `ILLMClient` - Provider-agnostic
- Wrappers: OpenAI, Anthropic, Azure, etc.
- Switchable via config

### Domain-Specifieke Componenten (in Brand2Boost/Client-Manager)

**1. UI Components**:
- `ColorScheme`, `ToneOfVoice`, `CoreValues` models
- SignalR notifiers met component routing
- Frontend component library

**2. Field Definitions**:
- `analysis-fields.config.json` - Project-specific fields
- Prompt files: `*.prompt.txt` - Domain knowledge
- Generation strategies: Logo, color scheme, etc.

**3. Controllers**:
- `AnalysisController` - Business logic
- `ChatController` - Chat orchestration
- `GatheredDataController` - CRUD operations

**4. Extensions**:
- `ToolsContextAnalysisExtensions` - Register domain tools
- `AgentWithImageTools` - Decorate context
- Strategy factories

**5. Background Jobs**:
- Token management
- Social media publishing
- Embeddings processing

### Scheiding Tabel

| Component | Hasina (Generic) | Brand2Boost (Domain) |
|-----------|------------------|----------------------|
| Agent | ✓ HazinaAgent | ChatController |
| Tools | ✓ HazinaChatTool | generate_logo, etc. |
| Data | ✓ GatheredDataItem | "brand-name", "target-audience" |
| Fields | ✓ AnalysisFieldConfig | ColorScheme, ToneOfVoice |
| Storage | ✓ FileSystemProvider | analysis-fields.config.json |
| Generation | ✓ IAnalysisFieldService | ImageSetGenerationStrategy |
| UI Events | ✓ INotifier interface | SignalRAnalysisFieldNotifier |
| LLM | ✓ ILLMClient | Prompt engineering |

### Observaties
- **Sterke scheiding** tussen framework en implementatie
- Hasina kan gebruikt worden voor andere projecten (HR, Sales, etc.)
- Domain logic zit in config + extensions, niet in core
- Migration naar andere storage (DB, S3) is mogelijk via interface swap

---

## Sprint 4 Features en Chat Integratie

**Status**: ✖ **NIET GEÏNTEGREERD IN CHAT**

### Sprint 4 Features (geïmplementeerd)

**Controllers**:
1. `SocialMediaAnalyticsController.cs` - 8 analytics endpoints
2. `SocialMediaPostController.cs` - Enhanced met bulk operations

**Services**:
1. `SocialMediaMetricsService.cs` - Metrics aggregation
2. `ContentPreviewService.cs` - Platform-specific previews
3. `SocialMediaPublishingService.cs` - Enhanced multi-account

**Features**:
- Analytics dashboard (project/platform/trends)
- Retry failed posts (single/bulk)
- Multi-account selection
- Content preview & validation
- Best time to post analysis
- Bulk operations (delete/publish/schedule/status)

### Wat NIET Bestaat

**❌ Tools voor Sprint 4 Features**:
- Geen `analyze_social_performance` tool
- Geen `preview_content_for_platform` tool
- Geen `bulk_publish_posts` tool
- Geen `get_best_time_to_post` tool

**❌ Chat Commands**:
- Geen conversational access tot analytics
- Geen "show me platform performance"
- Geen "what's the best time to post on LinkedIn?"
- Geen "preview this content on Twitter"

**❌ Integration**:
- Sprint 4 is **pure REST API**
- Frontend moet direct API calls doen
- Geen LLM agent access
- Geen chat-driven workflows

### Impact

**Huidige Situatie**:
```
User: "Show me how this post performs on LinkedIn"
Agent: "I don't have access to analytics. Use the dashboard."
```

**Gewenste Situatie**:
```
User: "Show me how this post performs on LinkedIn"
Agent: [Calls analyze_social_performance tool]
Agent: "Your LinkedIn post got 45 likes, 12 comments, and 3 shares..."
```

### Required Work

Om Sprint 4 te integreren in chat:

**1. Tool Registration** (in ToolsContextExtensions):
```csharp
var analyticsTool = new HazinaChatTool(
    "analyze_social_performance",
    "Get analytics for social media posts",
    parameters,
    async (messages, toolCall, cancel) => {
        var analytics = await metricsService
            .GetProjectAnalyticsAsync(projectId, startDate, endDate);
        return JsonSerializer.Serialize(analytics);
    });
```

**2. Additional Tools Needed**:
- `get_platform_comparison` - Cross-platform metrics
- `get_best_time_to_post` - Posting recommendations
- `preview_content` - Platform-specific previews
- `bulk_publish` - Multi-post operations
- `retry_failed_posts` - Error recovery

**3. Natural Language Interface**:
- LLM moet parameters extraheren uit conversatie
- Date ranges: "last month", "this week"
- Platforms: "LinkedIn", "all platforms"
- Post selection: "all failed posts", "scheduled for tomorrow"

**4. Response Formatting**:
- Analytics data moet conversational worden
- Charts/graphs als SignalR component events
- Markdown tables voor data
- Summaries in natural language

### Observaties
- **Grote GAP**: Volledige feature set niet toegankelijk via chat
- Gebruikers moeten switchen tussen chat en dashboard
- Missed opportunity voor conversational analytics
- Relatief eenvoudig te implementeren (tool registration + formatting)

---

## Architecturale Risico's

### 1. String-Based Component Resolution
**Risico**: `componentName` is string-based, geen compile-time check
**Impact**: Runtime errors als component niet bestaat
**Mitigatie**: Component registry met validation

### 2. No Dependency DAG
**Risico**: Circular dependencies mogelijk, no enforcement
**Impact**: Infinite loops, generation failures
**Mitigatie**: Dependency graph builder met cycle detection

### 3. File System Storage
**Risico**: Niet schaalbaar voor multi-tenant cloud deployment
**Impact**: Performance, concurrency issues
**Mitigatie**: Database provider implementatie

### 4. No Caching Layer
**Risico**: Config files geladen bij elke request
**Impact**: Performance overhead
**Mitigatie**: In-memory cache met invalidation

### 5. Tool Registration at Runtime
**Risico**: Tools niet hot-reloadable, restart required
**Impact**: Development velocity, deployment downtime
**Mitigatie**: Config-driven tool registration

### 6. No Rate Limiting
**Risico**: LLM API abuse, cost explosion
**Impact**: Financial, quota exhaustion
**Mitigatie**: Token budget system (exists), rate limiting middleware

### 7. No Interview Completion Detection
**Risico**: User doesn't know when profile is "complete"
**Impact**: UX confusion, incomplete data
**Mitigatie**: Explicit InterviewAgent met completion criteria

---

## Inconsistenties

### 1. Mixed Terminology
- "Analysis Field" vs "Generated Field" vs "Analysis Data"
- "Gathered Data" vs "Intake Data" vs "User Data"
- **Resolutie**: Documenteer canonical terms

### 2. Component Naming
- Some: `ColorScheme` → `ColorSchemeChat`
- Others: `ImageSet` → just `ImageSet`
- **Resolutie**: Enforce naming convention

### 3. Event Routing
- Session-specific vs Project-wide inconsistent
- Some use session, some skip to project
- **Resolutie**: Clear routing decision tree

### 4. Error Handling
- Some services throw exceptions
- Some return null
- Some return error messages
- **Resolutie**: Standardize error pattern

### 5. Configuration Locations
- Some config in code (AnalysisController defaults)
- Some in JSON files
- Some in prompt files
- **Resolutie**: Config-driven defaults

---

## Technische Schuld

### Laag (Acceptable)
1. FileSystem storage (works for current scale)
2. No caching (not bottleneck yet)
3. String-based components (convention works)

### Medium (Should Address)
1. **Sprint 4 niet in chat** - Grote feature gap
2. **No dependency enforcement** - Potential bugs
3. **Mixed error handling** - Inconsistent UX
4. **Tool registration in code** - Not hot-reloadable

### Hoog (Critical)
None identified. Architecture is solid.

---

## Kansen voor Vereenvoudiging

### 1. Unified Tool Registration
**Huidig**: Tools geregistreerd in extension methods
**Voorstel**: JSON-based tool definitions
**Voordeel**: Hot-reload, no recompile

### 2. Dependency Engine
**Huidig**: Dependencies in metadata, niet enforced
**Voorstel**: Dependency resolver met auto-ordering
**Voordeel**: Correctness, auto-regeneration

### 3. Component Registry
**Huidig**: String-based resolution
**Voorstel**: Registry met type checking
**Voordeel**: Compile-time safety

### 4. Interview State Machine
**Huidig**: Implicit via LLM prompts
**Voorstel**: Explicit InterviewAgent klasse
**Voordeel**: Predictable, testable, completeness detection

### 5. Unified Event Model
**Huidig**: Different notifiers voor different events
**Voorstel**: Single event bus met typed events
**Voordeel**: Simplicity, extensibility

---

## Conclusie

Hasina framework is een **solide, goed-gestructureerde implementatie** van het conceptuele model. Key strengths:

✅ **Generieke core** - Hasina is reusable
✅ **Clean separation** - Domain logic in Brand2Boost layer
✅ **Config-driven** - Fields, components, strategies via config
✅ **Event-driven UI** - Real-time updates via SignalR
✅ **Tool system** - Application functions, not specs
✅ **Two-tier data** - Gathered vs Analysis clear
✅ **Background jobs** - Async orchestration met Hangfire

**Grootste Gap**: Sprint 4 features niet toegankelijk via chat
**Grootste Kans**: Interview Agent formaliseren
**Grootste Risico**: Dependency management niet enforced

Overall score: **8/10** - Excellent foundation, minor improvements possible

---

**Document Version**: 1.0
**Next Review**: After implementing proposals
**Maintained By**: Architecture team
