# Handmatige Fix Instructies voor LLM Logging

## Stap 2.1: GeneratorAgentBase.cs Aanpassingen

**Bestand**: `C:\Projects\hazina\src\Tools\Foundation\Hazina.Tools.AI.Agents\Agents\GeneratorAgentBase.cs`

### Wijziging 1: Using Statements Toevoegen (na regel 18)

```csharp
using Hazina.Observability.LLMLogs.Decorators;
using Hazina.Observability.LLMLogs.Storage;
using Hazina.Observability.LLMLogs.Configuration;
using Microsoft.Extensions.Options;
```

### Wijziging 2: Private Fields Toevoegen (na regel 34)

```csharp
private readonly ILLMLogRepository _llmLogRepository;
private readonly IOptions<LLMLoggingOptions> _llmLoggingOptions;
```

### Wijziging 3: Constructor Signature Wijzigen (regel 41)

**Van:**
```csharp
public GeneratorAgentBase(IConfiguration configuration, string basisPrompt)
```

**Naar:**
```csharp
public GeneratorAgentBase(
    IConfiguration configuration,
    string basisPrompt,
    ILLMLogRepository llmLogRepository = null,
    IOptions<LLMLoggingOptions> llmLoggingOptions = null)
```

### Wijziging 4: Constructor Body - Dependencies Opslaan (na regel 53)

**Toevoegen aan het einde van de constructor:**
```csharp
_llmLogRepository = llmLogRepository;
_llmLoggingOptions = llmLoggingOptions;
```

### Wijziging 5: GetGeneratorWithoutPrompt - LLM Client Wrapping (rond regel 197-203)

**Van:**
```csharp
public async Task<DocumentGenerator> GetGeneratorWithoutPrompt(Project project)
{
    var store = await InitStore(project);
    var folder = _fileLocator.GetProjectFolder(project.Id);
    var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);
    var g = new DocumentGenerator(setup.Store, new List<HazinaChatMessage>(), setup.LLMClient, new List<IDocumentStore>());
    return g;
}
```

**Naar:**
```csharp
public async Task<DocumentGenerator> GetGeneratorWithoutPrompt(Project project)
{
    var store = await InitStore(project);
    var folder = _fileLocator.GetProjectFolder(project.Id);
    var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);

    // Wrap LLM client with logging decorator if available
    var llmClient = setup.LLMClient;
    if (_llmLogRepository != null && _llmLoggingOptions != null)
    {
        llmClient = new LLMLoggingClientDecorator(
            setup.LLMClient,
            _llmLogRepository,
            _llmLoggingOptions,
            "OpenAI");
    }

    var g = new DocumentGenerator(setup.Store, new List<HazinaChatMessage>(), llmClient, new List<IDocumentStore>());
    return g;
}
```

### Wijziging 6: GetGenerator - LLM Client Wrapping (rond regel 205-219)

**Van:**
```csharp
public async Task<DocumentGenerator> GetGenerator(Project project, string prompt)
{
    var assistantPrompts = new List<HazinaChatMessage>()
    {
        new HazinaChatMessage(HazinaMessageRole.System, prompt),
    };
    if(!string.IsNullOrWhiteSpace(project.KlantSpecifiekePrompt))
        assistantPrompts.Add(new HazinaChatMessage(HazinaMessageRole.System, project.KlantSpecifiekePrompt));

    var store = await InitStore(project);
    var folder = _fileLocator.GetProjectFolder(project.Id);
    var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);
    var g = new DocumentGenerator(store, assistantPrompts, setup.LLMClient, new List<IDocumentStore>());
    return g;
}
```

**Naar:**
```csharp
public async Task<DocumentGenerator> GetGenerator(Project project, string prompt)
{
    var assistantPrompts = new List<HazinaChatMessage>()
    {
        new HazinaChatMessage(HazinaMessageRole.System, prompt),
    };
    if(!string.IsNullOrWhiteSpace(project.KlantSpecifiekePrompt))
        assistantPrompts.Add(new HazinaChatMessage(HazinaMessageRole.System, project.KlantSpecifiekePrompt));

    var store = await InitStore(project);
    var folder = _fileLocator.GetProjectFolder(project.Id);
    var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);

    // Wrap LLM client with logging decorator if available
    var llmClient = setup.LLMClient;
    if (_llmLogRepository != null && _llmLoggingOptions != null)
    {
        llmClient = new LLMLoggingClientDecorator(
            setup.LLMClient,
            _llmLogRepository,
            _llmLoggingOptions,
            "OpenAI");
    }

    var g = new DocumentGenerator(store, assistantPrompts, llmClient, new List<IDocumentStore>());
    return g;
}
```

## Al Gedaan

✅ AgentWithImageTools.cs is al bijgewerkt
✅ ChatController.cs is al bijgewerkt
✅ ChatService.cs dubbele berichten fix is gedaan
✅ Project reference is toegevoegd aan Hazina.Tools.AI.Agents.csproj

## Om Te Doen

1. Pas bovenstaande wijzigingen toe aan GeneratorAgentBase.cs
2. Build het project: `dotnet build Hazina.sln`
3. Test de applicatie
4. Commit alle changes

## Verificatie

Na de wijzigingen build je met:
```bash
cd /c/Projects/hazina
dotnet build Hazina.sln
```

Er zouden 0 errors moeten zijn.
