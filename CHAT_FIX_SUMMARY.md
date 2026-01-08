# Chat LLM Configuration Fix - Summary

## Problem
Chat functionality was failing with error: `System.ArgumentException: Value cannot be an empty string. (Parameter 'model')`

## Root Cause
Multiple code paths were using the legacy `StoreProvider.GetStoreSetup(folder, apiKey)` overload which creates an `OpenAIConfig` with only the API key, leaving the Model property empty.

## Solution Overview
The fix requires three main changes:

### 1. HazinaStoreConfigLoader.cs
**Location**: `src/Tools/Services/Hazina.Tools.Services.FileOps/Helpers/HazinaStoreConfigLoader.cs`

**Change needed**:
```csharp
public static HazinaStoreConfig LoadHazinaStoreConfig()
{
    // ... existing config builder code ...

    var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>();
    var projectSettings = configuration.GetSection("ProjectSettings").Get<ProjectSettings>();
    var googleOAuthSettings = configuration.GetSection("GoogleOAuth").Get<GoogleOAuthSettings>();

    // ADD THIS:
    var openAIConfig = Hazina.LLMs.OpenAI.OpenAIConfig.FromConfiguration(configuration);

    // Resolve "configuration:" references in OpenAI config
    if (!string.IsNullOrEmpty(openAIConfig.ApiKey) && openAIConfig.ApiKey.StartsWith("configuration:"))
    {
        var configPath = openAIConfig.ApiKey.Substring("configuration:".Length);
        var resolvedValue = configuration[configPath];
        if (!string.IsNullOrEmpty(resolvedValue))
        {
            openAIConfig.ApiKey = resolvedValue;
        }
    }

    var config = new HazinaStoreConfig
    {
        ProjectSettings = projectSettings,
        ApiSettings = apiSettings,
        GoogleOAuthSettings = googleOAuthSettings,
        OpenAI = openAIConfig  // ADD THIS
    };
    return config;
}
```

### 2. HazinaStoreConfig.cs
**Location**: `src/Tools/Foundation/Hazina.Tools.Core/Config/HazinaStoreConfig.cs`

**Add property**:
```csharp
public OpenAIConfig OpenAI;
```

### 3. StoreProvider.cs
**Location**: `src/Tools/Foundation/Hazina.Tools.Data/StoreProvider.cs`

**Add new overload**:
```csharp
/// <summary>
/// Gets store setup using file-based storage with full OpenAI configuration
/// </summary>
public static StoreSetup GetStoreSetup(string folder, OpenAIConfig openAIConfig)
{
    var embeddingsFolder = Path.Combine(folder, "embeddings");
    var embeddingsPath = Path.Combine(embeddingsFolder, "embeddings.json");
    var partsPath = Path.Combine(folder, "parts");

    // ... migration code ...

    var llmClient = new OpenAIClientWrapper(openAIConfig);
    var fileStore = new EmbeddingFileStore(embeddingsPath, llmClient);
    var textStore = new TextFileStore(folder);
    var partStore = new DocumentPartFileStore(partsPath);
    var chunkIndexPath = Path.Combine(partsPath, "chunks.json");
    var chunkStore = new ChunkFileStore(chunkIndexPath);

    var metadataPath = Path.Combine(folder, "metadata");
    var metadataStore = new QueryableMetadataFileStore(metadataPath);

    var store = new DocumentStore(fileStore, textStore, chunkStore, metadataStore, llmClient);

    var tagRelevancePath = Path.Combine(folder, "tag_relevance");
    var tagRelevanceStore = new TagRelevanceFileStore(tagRelevancePath);
    var tagScoringService = new LLMTagScoringService(llmClient, tagRelevanceStore);
    var compositeScorer = new DefaultCompositeScorer();

    var setup = new StoreSetup()
    {
        LLMClient = llmClient,
        DocumentPartStore = partStore,
        TextStore = textStore,
        TextEmbeddingStore = fileStore,
        Store = store,
        QueryableMetadataStore = metadataStore,
        TagRelevanceStore = tagRelevanceStore,
        TagScoringService = tagScoringService,
        CompositeScorer = compositeScorer
    };

    return setup;
}
```

### 4. GeneratorAgentBase.cs
**Location**: `src/Tools/Foundation/Hazina.Tools.AI.Agents/Agents/GeneratorAgentBase.cs`

**Replace all calls** (lines 88, 96, 201, 228):
```csharp
// OLD:
var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);

// NEW:
var setup = StoreProvider.GetStoreSetup(folder, Config.OpenAI);
```

### 5. EmbeddingsService.cs
**Location**: `src/Tools/Services/Hazina.Tools.Services.Embeddings/EmbeddingsService.cs`

**Replace all calls** (lines 50, 84, 134, 143, 154, 155, 171):
```csharp
// OLD:
var setup = StoreProvider.GetStoreSetup(folder, _config.ApiSettings.OpenApiKey);

// NEW:
var setup = StoreProvider.GetStoreSetup(folder, _config.OpenAI);
```

### 6. BigQueryService.cs
**Location**: `src/Tools/Services/Hazina.Tools.Services.BigQuery/BigQueryService.cs`

**Replace call** (line 59):
```csharp
// OLD:
var bigQueryStoreSetup = StoreProvider.GetStoreSetup(folder, _apiKey);

// NEW:
// Need to load full config or pass OpenAIConfig instance
```

## Testing
After applying all changes:
1. Build hazina: `dotnet build Hazina.Tools.sln`
2. Build client-manager: `dotnet build ClientManagerAPI.local.csproj`
3. Run API and test chat endpoint
4. Verify no "model is empty" errors

## Status
- **Branch created**: `fix/chat-llm-config-loading`
- **Commits**: 2 commits with partial fixes
- **Remaining work**: Apply all changes listed above and test

## Notes
- The linter may have reverted some changes - verify all files before committing
- The appsettings.json uses `"configuration:ApiSettings:OpenApiKey"` pattern that needs manual resolution
- OpenAIConfig.FromConfiguration() calls ApplyDefaults() which sets Model to "gpt-4o-mini" if not specified
