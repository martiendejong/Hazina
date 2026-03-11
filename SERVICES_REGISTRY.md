# Hazina Services Registry
Complete overview of all service interfaces in Hazina.
**Total Services:** 36
**Last Updated:** 2026-02-28

---
## Table of Contents
- [Agent Tools](#agent-tools) (1)
- [Chat & Messaging](#chat--messaging) (6)
- [Content Publishing](#content-publishing) (1)
- [Data & Storage](#data--storage) (2)
- [Data Analysis](#data-analysis) (2)
- [Embeddings & Vectors](#embeddings--vectors) (2)
- [Image Processing](#image-processing) (3)
- [Other Services](#other-services) (17)
- [Web & Scraping](#web--scraping) (2)

---
## Quick Reference
| Interface | Implementation | Category | Purpose |
|-----------|----------------|----------|----------|
| `IAgentExecutionService` | `N/A` | Other Services | Interface for agent and flow execution operations. |
| `IAgentExecutionService` | `N/A` | Other Services | (No description) |
| `IAgentIdentityService` | `AgentIdentityService` | Other Services | Service for managing persistent agent identities (CV system)... |
| `IAlertingService` | `N/A` | Other Services | Service for monitoring and alerting on prompt performance |
| `IAnalysisFieldService` | `N/A` | Data Analysis | Service for automatically generating analysis fields from ch... |
| `IAuthService` | `N/A` | Other Services | Service for user authentication and registration |
| `IBigQueryService` | `N/A` | Other Services | Interface for BigQuery operations. |
| `IBlogStorageService` | `N/A` | Data & Storage | Generic blog storage abstraction supporting multiple storage... |
| `IChatCanvasService` | `N/A` | Chat & Messaging | (No description) |
| `IChatImageService` | `N/A` | Chat & Messaging | Generates an image from a prompt and returns the raw bytes. ... |
| `IChatMessageService` | `N/A` | Chat & Messaging | (No description) |
| `IChatMetadataService` | `N/A` | Chat & Messaging | (No description) |
| `IChatService` | `N/A` | Chat & Messaging | (No description) |
| `IChatStreamService` | `N/A` | Chat & Messaging | (No description) |
| `IConversationStarterService` | `N/A` | Other Services | (No description) |
| `IDataGatheringService` | `N/A` | Data Analysis | Service responsible for extracting and storing structured da... |
| `IDatabaseService` | `N/A` | Data & Storage | Service interface for database operations. Provides query co... |
| `IEmailService` | `N/A` | Other Services | Interface for email operations. |
| `IEmbeddingsService` | `N/A` | Embeddings & Vectors | Generate embedding for a single text string |
| `IFireCrawlService` | `N/A` | Web & Scraping | Interface for FireCrawl MCP web scraping operations.     Ena... |
| `IImageAnalysisService` | `N/A` | Image Processing | Analyze image content using AI vision models |
| `IIncrementalEmbeddingService` | `N/A` | Embeddings & Vectors | Service for incremental document embedding. Only re-embeds c... |
| `IInteractionService` | `N/A` | Other Services | (No description) |
| `IJwtService` | `N/A` | Other Services | Service for JWT token generation and validation |
| `ILLMTextService` | `N/A` | Other Services | Simplified text-in/text-out LLM service for graph constructi... |
| `ILayeredImageService` | `N/A` | Image Processing | Main service for generating layered images. Orchestrates lay... |
| `ILearningIntegrationService` | `N/A` | Other Services | (No description) |
| `IOutputCaptureService` | `N/A` | Other Services | (No description) |
| `IPromptTemplateService` | `N/A` | Other Services | Service for managing predefined prompt templates. Provides C... |
| `IPublishingService` | `N/A` | Content Publishing | Generic publishing service abstraction supporting multiple p... |
| `ISmartLayeredImageService` | `N/A` | Image Processing | Smart layered image service that takes a natural language pr... |
| `IStateSyncService` | `N/A` | Other Services | (No description) |
| `ITagScoringService` | `N/A` | Other Services | Service for scoring tags based on query relevance. Can use L... |
| `IToolAgentService` | `N/A` | Agent Tools | Service for executing actions via the tool agent orchestrati... |
| `IToolRegistrationService` | `N/A` | Other Services | Interface for tool registration operations. |
| `IWebSearchService` | `N/A` | Web & Scraping | Interface for web search operations. |

---
## Agent Tools
**Count:** 1

### `IToolAgentService`
**Implementation:** `N/A`

**Description:** Service for executing actions via the tool agent orchestration layer. The tool agent receives high-level actions from the chat agent and orchestrates which specialized tools to call to accomplish the task.

**File:** `src\Tools\Services\Hazina.Tools.Services.ToolAgent\Abstractions\IToolAgentService.cs`

**Key Methods:**
- `ExecuteActionAsync()`
- `GetAvailableActionsAsync()`

---

## Chat & Messaging
**Count:** 6

### `IChatCanvasService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatCanvasService.cs`

**Key Methods:**
- `EditCanvasMessage()`
- `EditCanvasMessage()`

---

### `IChatImageService`
**Implementation:** `N/A`

**Description:** Generates an image from a prompt and returns the raw bytes.         Used by LayeredImageService for generating individual layers.

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatImageService.cs`

**Key Methods:**
- `GenerateImage()`
- `GenerateImage()`
- `GenerateImage()`
- `GenerateImage()`
- `GenerateImageBytesAsync()`
- `GenerateImageBytesWithContextAsync()`

---

### `IChatMessageService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatMessageService.cs`

**Key Methods:**
- `Delete()`

---

### `IChatMetadataService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatMetadataService.cs`

---

### `IChatService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatService.cs`

**Key Methods:**
- `Delete()`
- `Delete()`
- `SendChatMessage()`
- `SendChatMessage()`
- `GenerateImage()`
- `GenerateImage()`
- `GenerateImage()`
- `GenerateImage()`
- _...and 2 more_

---

### `IChatStreamService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IChatStreamService.cs`

**Key Methods:**
- `SendChatMessage()`
- `SendChatMessage()`
- `SendChatMessage()`

---

## Content Publishing
**Count:** 1

### `IPublishingService`
**Implementation:** `N/A`

**Description:** Generic publishing service abstraction supporting multiple platforms Implementations: WordPress, Medium, Dev.to, Hashnode, Ghost, Blogger, etc.

**File:** `src\Tools\Services\Hazina.Tools.Services\Publishing\IPublishingService.cs`

**Key Methods:**
- `PublishAsync()`
- `UpdateAsync()`
- `DeleteAsync()`
- `ValidateConnectionAsync()`
- `GetCategoriesAsync()`

---

## Data & Storage
**Count:** 2

### `IBlogStorageService`
**Implementation:** `N/A`

**Description:** Generic blog storage abstraction supporting multiple storage providers (File, Database, etc.) Configured via appsettings.json: "BlogStorage:Provider"

**File:** `src\Tools\Services\Hazina.Tools.Services\Blog\IBlogStorageService.cs`

**Key Methods:**
- `AddBlogItemAsync()`
- `UpdateBlogItemAsync()`
- `DeleteBlogItemAsync()`
- `GetBlogItemAsync()`
- `GetBlogItemsAsync()`
- `GetDueForPublishingAsync()`
- `MarkAsPublishedAsync()`

---

### `IDatabaseService`
**Implementation:** `N/A`

**Description:** Service interface for database operations. Provides query compilation and execution with schema validation.

**File:** `src\Tools\Services\Hazina.Tools.Services.Database\Abstractions\IDatabaseService.cs`

**Key Methods:**
- `ExecuteQueryAsync()`
- `ExecuteRestrictedQueryAsync()`
- `GetSchemaAsync()`
- `CreateTablesAsync()`

---

## Data Analysis
**Count:** 2

### `IAnalysisFieldService`
**Implementation:** `N/A`

**Description:** Service for automatically generating analysis fields from chat conversations. Runs in parallel with the main chat to populate analysis fields when enough context is available.

**File:** `src\Tools\Services\Hazina.Tools.Services.DataGathering\Abstractions\IAnalysisFieldService.cs`

**Key Methods:**
- `LoadFieldConfigsAsync()`
- `GenerateFromConversationAsync()`
- `GetFieldsAsync()`
- `GetFieldContentAsync()`
- `SaveFieldAsync()`
- `GenerateTypedFieldAsync()`

---

### `IDataGatheringService`
**Implementation:** `N/A`

**Description:** Service responsible for extracting and storing structured data from chat conversations. This is the main orchestration interface for the data gathering feature.

**File:** `src\Tools\Services\Hazina.Tools.Services.DataGathering\Abstractions\IDataGatheringService.cs`

**Key Methods:**
- `GatherDataFromMessageAsync()`
- `GetProjectDataAsync()`
- `GetDataItemAsync()`
- `StoreDataItemAsync()`
- `DeleteDataItemAsync()`

---

## Embeddings & Vectors
**Count:** 2

### `IEmbeddingsService`
**Implementation:** `N/A`

**Description:** Generate embedding for a single text string

**File:** `src\Tools\Services\Hazina.Tools.Services.Embeddings\IEmbeddingsService.cs`

**Key Methods:**
- `RefreshProjectEmbeddings()`
- `RefreshGlobalEmbeddings()`
- `EmbedProjectFile()`
- `EmbedChatUpload()`
- `PromoteChatFileToProject()`
- `DemoteChatFileFromProject()`
- `GenerateEmbeddingAsync()`

---

### `IIncrementalEmbeddingService`
**Implementation:** `N/A`

**Description:** Service for incremental document embedding. Only re-embeds chunks that have changed, reducing API costs.

**File:** `src\Core\Storage\Hazina.Store.DocumentStore\Interfaces\IIncrementalEmbeddingService.cs`

**Key Methods:**
- `ComputeDiffAsync()`
- `EmbedIncrementallyAsync()`
- `GetChunkHashesAsync()`
- `StoreChunkEmbeddingsAsync()`
- `DeleteChunkEmbeddingsAsync()`
- `GetStatisticsAsync()`

---

## Image Processing
**Count:** 3

### `IImageAnalysisService`
**Implementation:** `N/A`

**Description:** Analyze image content using AI vision models

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IImageAnalysisService.cs`

**Key Methods:**
- `AnalyzeImageAsync()`

---

### `ILayeredImageService`
**Implementation:** `N/A`

**Description:** Main service for generating layered images. Orchestrates layer generation, composition, and export.

**File:** `src\Tools\Services\Hazina.Tools.Services.Images\LayeredImage\Abstractions\ILayeredImageService.cs`

**Key Methods:**
- `GenerateAsync()`
- `GenerateFromJsonAsync()`

---

### `ISmartLayeredImageService`
**Implementation:** `N/A`

**Description:** Smart layered image service that takes a natural language prompt and automatically plans, generates, and composites a multi-layer image.

**File:** `src\Tools\Services\Hazina.Tools.Services.Images\LayeredImage\Abstractions\ISmartLayeredImageService.cs`

**Key Methods:**
- `GenerateFromPromptAsync()`

---

## Other Services
**Count:** 17

### `IAgentExecutionService`
**Implementation:** `N/A`

**Description:** Interface for agent and flow execution operations.

**File:** `src\Core\Agents\Hazina.AgentFactory\Services\Execution\IAgentExecutionService.cs`

**Key Methods:**
- `CallAgentAsync()`
- `CallFlowAsync()`
- `CallCoderAgentAsync()`
- `CallAgentWithMetaAsync()`

---

### `IAgentExecutionService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Hazina.Agent.API\Services\IAgentExecutionService.cs`

---

### `IAgentIdentityService`
**Implementation:** `AgentIdentityService`

**Description:** Service for managing persistent agent identities (CV system) Inspired by Overstory's agent identity tracking

**File:** `src\Hazina.AgenticOrchestration\Services\AgentIdentityService.cs`

**Key Methods:**
- `CreateIdentityAsync()`
- `LoadIdentityAsync()`
- `UpdateIdentityAsync()`
- `RecordTaskCompletionAsync()`
- `CreateIdentityAsync()`
- `LoadIdentityAsync()`
- `UpdateIdentityAsync()`
- `RecordTaskCompletionAsync()`
- _...and 1 more_

---

### `IAlertingService`
**Implementation:** `N/A`

**Description:** Service for monitoring and alerting on prompt performance

**File:** `src\Core\AI\Hazina.AI.PromptManagement\Dashboard\IAlertingService.cs`

**Key Methods:**
- `CheckRegressionsAsync()`
- `CheckDriftAsync()`
- `SendAlertAsync()`
- `GetAlertHistoryAsync()`
- `SaveAlertRuleAsync()`
- `GetAlertRulesAsync()`

---

### `IAuthService`
**Implementation:** `N/A`

**Description:** Service for user authentication and registration

**File:** `src\Infrastructure\Auth\Hazina.Auth.Core\Interfaces\IAuthService.cs`

**Key Methods:**
- `RegisterAsync()`
- `LoginAsync()`
- `OAuthLoginAsync()`
- `RefreshTokenAsync()`
- `GetCurrentUserAsync()`
- `RevokeTokenAsync()`

---

### `IBigQueryService`
**Implementation:** `N/A`

**Description:** Interface for BigQuery operations.

**File:** `src\Core\Agents\Hazina.AgentFactory\Services\BigQuery\IBigQueryService.cs`

**Key Methods:**
- `GetDatasetsAsync()`
- `GetTablesAsync()`
- `GetTableFieldsAsync()`
- `ExecuteQueryAsync()`

---

### `IConversationStarterService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Tools\Services\Hazina.Tools.Services.Chat\Interfaces\IConversationStarterService.cs`

**Key Methods:**
- `GenerateConversationStarter()`
- `GetConversationStarter()`
- `OpenConversationStarter()`
- `OpenConversationStarter()`

---

### `IEmailService`
**Implementation:** `N/A`

**Description:** Interface for email operations.

**File:** `src\Core\Agents\Hazina.AgentFactory\Services\Email\IEmailService.cs`

**Key Methods:**
- `SendEmailAsync()`
- `ListInboxEmailsAsync()`
- `ReadEmailAsync()`
- `CreateMailboxFolderAsync()`
- `MoveEmailToFolderAsync()`
- `ListMailboxFoldersAsync()`

---

### `IInteractionService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Hazina.AgenticOrchestration\Services\IInteractionService.cs`

**Key Methods:**
- `NotifyAwaitingInputAsync()`
- `PollForResponseAsync()`
- `RespondToInteractionAsync()`
- `GetPendingInteractionsAsync()`

---

### `IJwtService`
**Implementation:** `N/A`

**Description:** Service for JWT token generation and validation

**File:** `src\Infrastructure\Auth\Hazina.Auth.Core\Interfaces\IJwtService.cs`

---

### `ILLMTextService`
**Implementation:** `N/A`

**Description:** Simplified text-in/text-out LLM service for graph construction.

**File:** `src\Core\AI\Hazina.AI.RAG\Graph\Pipeline\ILLMTextService.cs`

**Key Methods:**
- `GetResponseAsync()`

---

### `ILearningIntegrationService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Hazina.Agent.API\Services\ILearningIntegrationService.cs`

**Key Methods:**
- `IntegrateNewLearningsAsync()`
- `GetConsciousnessStateAsync()`
- `UpdateConsciousnessStateAsync()`

---

### `IOutputCaptureService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Hazina.AgenticOrchestration\Services\IOutputCaptureService.cs`

**Key Methods:**
- `StreamOutputAsync()`
- `GetOutputHistoryAsync()`

---

### `IPromptTemplateService`
**Implementation:** `N/A`

**Description:** Service for managing predefined prompt templates. Provides CRUD operations and persistence for terminal session prompt templates.

**File:** `src\Hazina.AgenticOrchestration\Services\IPromptTemplateService.cs`

**Key Methods:**
- `GetAllTemplatesAsync()`
- `GetTemplateAsync()`
- `GetFavoriteTemplatesAsync()`
- `GetRecentTemplatesAsync()`
- `CreateTemplateAsync()`
- `UpdateTemplateAsync()`
- `DeleteTemplateAsync()`
- `RecordTemplateUsageAsync()`
- _...and 2 more_

---

### `IStateSyncService`
**Implementation:** `N/A`

**Description:** (No description)

**File:** `src\Hazina.Agent.API\Services\IStateSyncService.cs`

**Key Methods:**
- `GetIdentityAsync()`
- `SyncStateAsync()`
- `PublishLearningEventAsync()`
- `GetNewLearningEventsAsync()`
- `HasConflictsAsync()`
- `ResolveConflictsAsync()`

---

### `ITagScoringService`
**Implementation:** `N/A`

**Description:** Service for scoring tags based on query relevance. Can use LLM or rule-based implementations.

**File:** `src\Core\Storage\Hazina.Store.DocumentStore\Interfaces\ITagScoringService.cs`

**Key Methods:**
- `ScoreTagsAsync()`
- `GetOrComputeScoresAsync()`
- `HasScoresForContextAsync()`

---

### `IToolRegistrationService`
**Implementation:** `N/A`

**Description:** Interface for tool registration operations.

**File:** `src\Core\Agents\Hazina.AgentFactory\Services\Tools\IToolRegistrationService.cs`

---

## Web & Scraping
**Count:** 2

### `IFireCrawlService`
**Implementation:** `N/A`

**Description:** Interface for FireCrawl MCP web scraping operations.     Enables autonomous web scraping, branding extraction, site mapping, and structured data extraction.

**File:** `src\Tools\Services\Hazina.Tools.Services.Web\Abstractions\IFireCrawlService.cs`

**Key Methods:**
- `ScrapeAsync()`
- `MapAsync()`
- `CrawlAsync()`
- `ExtractAsync()`
- `ExtractBrandingAsync()`
- `ScreenshotAsync()`
- `SearchAsync()`

---

### `IWebSearchService`
**Implementation:** `N/A`

**Description:** Interface for web search operations.

**File:** `src\Tools\Services\Hazina.Tools.Services.Web\Abstractions\IWebSearchService.cs`

**Key Methods:**
- `SearchAsync()`
- `FetchUrlAsync()`

---

