# Visual Studio Template - Implementation Plan

**Parent Document:** [README.md](./README.md)
**Status:** Planning
**Created:** 2026-01-13

---

## Overview

The Visual Studio Template provides a **"File → New Project"** experience for creating Hazina applications. It allows developers to quickly scaffold a new project with a guided wizard.

### Delivery Mechanisms

1. **Visual Studio Extension (VSIX)** - Full wizard experience in VS
2. **dotnet new Template** - CLI-based project creation
3. **VS Code Extension** - Lightweight wizard for VS Code users

---

## Template Types

### 1. Hazina RAG Application (Full)

Complete production-ready RAG application with all features.

```
Template: hazina-rag
Output: Web API with RAG, ingestion, search, health endpoints
Includes: Docker, tests, comprehensive configuration
```

### 2. Hazina RAG Minimal

Minimal RAG setup for quick prototyping.

```
Template: hazina-rag-minimal
Output: Single-file Web API with basic RAG
Includes: In-memory vector store, basic configuration
```

### 3. Hazina Worker Service

Background processing for document ingestion.

```
Template: hazina-worker
Output: Worker service for async document processing
Includes: Queue integration, pipeline processing
```

### 4. Hazina Console

Command-line RAG application.

```
Template: hazina-console
Output: CLI application for RAG queries
Includes: Interactive mode, file processing
```

---

## dotnet new Template

### Template Structure

```
templates/
└── project-templates/
    └── Hazina.RAG.Template/
        ├── .template.config/
        │   └── template.json
        ├── src/
        │   └── HazinaRagApp/
        │       ├── HazinaRagApp.csproj
        │       ├── Program.cs
        │       ├── Controllers/
        │       │   ├── QueryController.cs
        │       │   └── HealthController.cs
        │       ├── Services/
        │       │   └── RagQueryService.cs
        │       ├── Configuration/
        │       │   └── HazinaConfiguration.cs
        │       └── appsettings.json
        ├── tests/
        │   └── HazinaRagApp.Tests/
        │       └── QueryControllerTests.cs
        ├── Dockerfile
        ├── docker-compose.yml
        ├── .gitignore
        └── README.md
```

### template.json Configuration

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Hazina AI",
  "classifications": ["Web", "AI", "RAG", "Hazina"],
  "identity": "Hazina.RAG.Template",
  "name": "Hazina RAG Application",
  "shortName": "hazina-rag",
  "description": "A production-ready RAG application using Hazina AI framework",
  "tags": {
    "language": "C#",
    "type": "project"
  },
  "sourceName": "HazinaRagApp",
  "preferNameDirectory": true,
  "defaultName": "MyRagApp",
  "symbols": {
    "Framework": {
      "type": "parameter",
      "description": "The target framework for the project",
      "datatype": "choice",
      "choices": [
        { "choice": "net9.0", "description": ".NET 9.0" },
        { "choice": "net8.0", "description": ".NET 8.0" }
      ],
      "defaultValue": "net9.0",
      "replaces": "net9.0"
    },
    "LlmProvider": {
      "type": "parameter",
      "description": "Primary LLM provider",
      "datatype": "choice",
      "choices": [
        { "choice": "openai", "description": "OpenAI (GPT-4)" },
        { "choice": "anthropic", "description": "Anthropic (Claude)" },
        { "choice": "ollama", "description": "Ollama (Local)" }
      ],
      "defaultValue": "openai"
    },
    "VectorStore": {
      "type": "parameter",
      "description": "Vector store for embeddings",
      "datatype": "choice",
      "choices": [
        { "choice": "memory", "description": "In-Memory (Development)" },
        { "choice": "supabase", "description": "Supabase (Cloud)" },
        { "choice": "pgvector", "description": "PostgreSQL with pgvector" }
      ],
      "defaultValue": "memory"
    },
    "IncludeDocker": {
      "type": "parameter",
      "description": "Include Docker configuration",
      "datatype": "bool",
      "defaultValue": "true"
    },
    "IncludeTests": {
      "type": "parameter",
      "description": "Include test project",
      "datatype": "bool",
      "defaultValue": "true"
    },
    "IncludeSwagger": {
      "type": "parameter",
      "description": "Include Swagger/OpenAPI documentation",
      "datatype": "bool",
      "defaultValue": "true"
    },
    "UseOpenAI": {
      "type": "computed",
      "value": "(LlmProvider == \"openai\")"
    },
    "UseAnthropic": {
      "type": "computed",
      "value": "(LlmProvider == \"anthropic\")"
    },
    "UseOllama": {
      "type": "computed",
      "value": "(LlmProvider == \"ollama\")"
    },
    "UseMemoryStore": {
      "type": "computed",
      "value": "(VectorStore == \"memory\")"
    },
    "UseSupabase": {
      "type": "computed",
      "value": "(VectorStore == \"supabase\")"
    },
    "UsePgVector": {
      "type": "computed",
      "value": "(VectorStore == \"pgvector\")"
    }
  },
  "sources": [
    {
      "modifiers": [
        {
          "condition": "(!IncludeDocker)",
          "exclude": ["Dockerfile", "docker-compose.yml", ".dockerignore"]
        },
        {
          "condition": "(!IncludeTests)",
          "exclude": ["tests/**/*"]
        }
      ]
    }
  ],
  "postActions": [
    {
      "condition": "(!skipRestore)",
      "description": "Restore NuGet packages",
      "manualInstructions": [{ "text": "Run 'dotnet restore'" }],
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true
    }
  ]
}
```

### Template Files with Conditionals

**Program.cs:**
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
//#if (UseOpenAI)
using Hazina.LLMs.OpenAI;
//#endif
//#if (UseAnthropic)
using Hazina.LLMs.Anthropic;
//#endif
//#if (UseOllama)
using Hazina.LLMs.Ollama;
//#endif
using HazinaRagApp.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Hazina services
builder.Services.AddHazinaServices(builder.Configuration);

//#if (IncludeSwagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//#endif

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

//#if (IncludeSwagger)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//#endif

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

**HazinaConfiguration.cs:**
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.Providers;
//#if (UseOpenAI)
using Hazina.LLMs.OpenAI;
//#endif
//#if (UseAnthropic)
using Hazina.LLMs.Anthropic;
//#endif
//#if (UseOllama)
using Hazina.LLMs.Ollama;
//#endif
//#if (UseMemoryStore)
using Hazina.Store.EmbeddingStore;
//#endif
//#if (UseSupabase)
using Hazina.Store.Supabase;
//#endif
//#if (UsePgVector)
using Hazina.Store.PgVector;
//#endif

namespace HazinaRagApp.Configuration;

public static class HazinaConfiguration
{
    public static IServiceCollection AddHazinaServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure LLM provider
//#if (UseOpenAI)
        var llmClient = new OpenAIClientWrapper(new OpenAIConfig
        {
            ApiKey = configuration["Hazina:OpenAI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
            Model = configuration["Hazina:OpenAI:Model"] ?? "gpt-4o"
        });
//#endif
//#if (UseAnthropic)
        var llmClient = new ClaudeClientWrapper(new AnthropicConfig
        {
            ApiKey = configuration["Hazina:Anthropic:ApiKey"]
                ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!,
            Model = configuration["Hazina:Anthropic:Model"] ?? "claude-3-5-sonnet-20241022"
        });
//#endif
//#if (UseOllama)
        var llmClient = new OllamaClientWrapper(new OllamaConfig
        {
            Model = configuration["Hazina:Ollama:Model"] ?? "llama3.1",
            Endpoint = configuration["Hazina:Ollama:Endpoint"] ?? "http://localhost:11434"
        });
//#endif

        services.AddSingleton<ILLMClient>(llmClient);

        // Configure vector store
//#if (UseMemoryStore)
        services.AddSingleton<IEmbeddingStore, InMemoryVectorStore>();
//#endif
//#if (UseSupabase)
        services.AddSingleton<IEmbeddingStore>(sp =>
            new SupabaseEmbeddingStore(
                configuration["Hazina:Supabase:Url"]!,
                configuration["Hazina:Supabase:Key"]!));
//#endif
//#if (UsePgVector)
        services.AddSingleton<IEmbeddingStore>(sp =>
            new PgVectorStore(configuration.GetConnectionString("Postgres")!));
//#endif

        // Register services
        services.AddScoped<IRagQueryService, RagQueryService>();

        return services;
    }
}
```

### Installation

```bash
# Pack the template
dotnet pack templates/project-templates/Hazina.RAG.Template

# Install locally
dotnet new install ./templates/project-templates/Hazina.RAG.Template

# Install from NuGet (future)
dotnet new install Hazina.Templates

# Use the template
dotnet new hazina-rag -n MyRagApp --LlmProvider openai --VectorStore supabase
```

---

## Visual Studio VSIX Extension

### Extension Structure

```
Hazina.VisualStudio.Extension/
├── Hazina.VisualStudio.Extension.csproj
├── source.extension.vsixmanifest
├── Resources/
│   ├── Icon.png
│   └── Preview.png
├── ProjectTemplates/
│   ├── HazinaRag/
│   │   ├── HazinaRag.vstemplate
│   │   └── (template files)
│   └── HazinaRagMinimal/
│       └── ...
├── Wizards/
│   ├── HazinaProjectWizard.cs
│   ├── WizardWindow.xaml
│   └── WizardWindow.xaml.cs
└── Commands/
    └── NewHazinaProjectCommand.cs
```

### Project Wizard

The VSIX includes a WPF wizard for guided project creation.

```csharp
// HazinaProjectWizard.cs
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using System.Collections.Generic;
using System.Windows;

namespace Hazina.VisualStudio.Extension.Wizards
{
    public class HazinaProjectWizard : IWizard
    {
        private WizardWindow _wizardWindow;
        private Dictionary<string, string> _replacements;

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams)
        {
            _replacements = replacementsDictionary;

            // Show wizard dialog
            _wizardWindow = new WizardWindow();
            var result = _wizardWindow.ShowDialog();

            if (result != true)
            {
                throw new WizardCancelledException();
            }

            // Apply selections to replacements
            ApplyWizardSelections();
        }

        private void ApplyWizardSelections()
        {
            var model = _wizardWindow.ViewModel;

            _replacements["$LlmProvider$"] = model.SelectedLlmProvider;
            _replacements["$VectorStore$"] = model.SelectedVectorStore;
            _replacements["$IncludeDocker$"] = model.IncludeDocker.ToString().ToLower();
            _replacements["$IncludeTests$"] = model.IncludeTests.ToString().ToLower();
            _replacements["$IncludeSwagger$"] = model.IncludeSwagger.ToString().ToLower();

            // Generate assembly spec if requested
            if (model.GenerateAssemblySpec)
            {
                _replacements["$GenerateSpec$"] = "true";
            }
        }

        public bool ShouldAddProjectItem(string filePath)
        {
            // Conditionally include files based on wizard selections
            if (filePath.Contains("Dockerfile") && !bool.Parse(_replacements["$IncludeDocker$"]))
                return false;

            if (filePath.Contains(".Tests") && !bool.Parse(_replacements["$IncludeTests$"]))
                return false;

            return true;
        }

        // Other IWizard interface methods...
        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(Project project) { }
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
        public void RunFinished() { }
    }
}
```

### Wizard UI (WPF)

```xaml
<!-- WizardWindow.xaml -->
<Window x:Class="Hazina.VisualStudio.Extension.Wizards.WizardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Create Hazina RAG Application"
        Width="600" Height="500"
        WindowStartupLocation="CenterScreen">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,20">
            <TextBlock Text="Hazina RAG Application" FontSize="24" FontWeight="Bold"/>
            <TextBlock Text="Configure your AI-powered application" Foreground="Gray"/>
        </StackPanel>

        <!-- Configuration -->
        <ScrollViewer Grid.Row="1">
            <StackPanel>
                <!-- LLM Provider -->
                <GroupBox Header="LLM Provider" Margin="0,0,0,15">
                    <StackPanel>
                        <RadioButton Content="OpenAI (GPT-4o)" IsChecked="True"
                                     GroupName="LlmProvider" Tag="openai"/>
                        <RadioButton Content="Anthropic (Claude)"
                                     GroupName="LlmProvider" Tag="anthropic"/>
                        <RadioButton Content="Ollama (Local)"
                                     GroupName="LlmProvider" Tag="ollama"/>
                    </StackPanel>
                </GroupBox>

                <!-- Vector Store -->
                <GroupBox Header="Vector Store" Margin="0,0,0,15">
                    <StackPanel>
                        <RadioButton Content="In-Memory (Development)" IsChecked="True"
                                     GroupName="VectorStore" Tag="memory"/>
                        <RadioButton Content="Supabase (Cloud)"
                                     GroupName="VectorStore" Tag="supabase"/>
                        <RadioButton Content="PostgreSQL + pgvector"
                                     GroupName="VectorStore" Tag="pgvector"/>
                    </StackPanel>
                </GroupBox>

                <!-- Options -->
                <GroupBox Header="Additional Options" Margin="0,0,0,15">
                    <StackPanel>
                        <CheckBox Content="Include Docker configuration"
                                  IsChecked="{Binding IncludeDocker}"/>
                        <CheckBox Content="Include test project"
                                  IsChecked="{Binding IncludeTests}"/>
                        <CheckBox Content="Include Swagger/OpenAPI"
                                  IsChecked="{Binding IncludeSwagger}"/>
                        <CheckBox Content="Generate assembly specification file"
                                  IsChecked="{Binding GenerateAssemblySpec}"/>
                    </StackPanel>
                </GroupBox>

                <!-- Advanced -->
                <Expander Header="Advanced Configuration">
                    <StackPanel Margin="10">
                        <Label Content="Embedding Model:"/>
                        <ComboBox SelectedItem="{Binding EmbeddingModel}">
                            <ComboBoxItem Content="text-embedding-3-small"/>
                            <ComboBoxItem Content="text-embedding-3-large"/>
                            <ComboBoxItem Content="text-embedding-ada-002"/>
                        </ComboBox>

                        <Label Content="Chunk Size:" Margin="0,10,0,0"/>
                        <Slider Minimum="256" Maximum="2048" Value="{Binding ChunkSize}"
                                TickFrequency="256" IsSnapToTickEnabled="True"/>
                        <TextBlock Text="{Binding ChunkSize, StringFormat='{}{0} tokens'}"/>
                    </StackPanel>
                </Expander>
            </StackPanel>
        </ScrollViewer>

        <!-- Buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,20,0,0">
            <Button Content="Cancel" Width="100" Margin="0,0,10,0"
                    Click="Cancel_Click"/>
            <Button Content="Create Project" Width="120" IsDefault="True"
                    Click="Create_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

---

## VS Code Extension

Lightweight extension for VS Code users.

### Extension Structure

```
vscode-hazina/
├── package.json
├── tsconfig.json
├── src/
│   ├── extension.ts
│   ├── commands/
│   │   └── newProject.ts
│   └── webview/
│       ├── wizard.html
│       └── wizard.js
└── resources/
    └── icon.png
```

### package.json

```json
{
  "name": "hazina-vscode",
  "displayName": "Hazina AI",
  "description": "Create and manage Hazina AI applications",
  "version": "1.0.0",
  "publisher": "hazina-ai",
  "engines": {
    "vscode": "^1.80.0"
  },
  "categories": ["Other"],
  "activationEvents": [
    "onCommand:hazina.newProject"
  ],
  "main": "./out/extension.js",
  "contributes": {
    "commands": [
      {
        "command": "hazina.newProject",
        "title": "Hazina: New RAG Application"
      }
    ]
  }
}
```

### Extension Implementation

```typescript
// extension.ts
import * as vscode from 'vscode';
import { NewProjectCommand } from './commands/newProject';

export function activate(context: vscode.ExtensionContext) {
    const newProjectCmd = vscode.commands.registerCommand(
        'hazina.newProject',
        () => new NewProjectCommand(context).execute()
    );

    context.subscriptions.push(newProjectCmd);
}

// commands/newProject.ts
import * as vscode from 'vscode';
import * as child_process from 'child_process';
import * as path from 'path';

export class NewProjectCommand {
    constructor(private context: vscode.ExtensionContext) {}

    async execute() {
        // Get project name
        const projectName = await vscode.window.showInputBox({
            prompt: 'Enter project name',
            placeHolder: 'MyRagApp'
        });

        if (!projectName) return;

        // Select LLM provider
        const llmProvider = await vscode.window.showQuickPick(
            ['openai', 'anthropic', 'ollama'],
            { placeHolder: 'Select LLM Provider' }
        );

        // Select vector store
        const vectorStore = await vscode.window.showQuickPick(
            ['memory', 'supabase', 'pgvector'],
            { placeHolder: 'Select Vector Store' }
        );

        // Get output folder
        const folderUri = await vscode.window.showOpenDialog({
            canSelectFiles: false,
            canSelectFolders: true,
            canSelectMany: false,
            title: 'Select Output Folder'
        });

        if (!folderUri || folderUri.length === 0) return;

        const outputPath = path.join(folderUri[0].fsPath, projectName);

        // Run dotnet new command
        const command = `dotnet new hazina-rag -n ${projectName} ` +
            `--LlmProvider ${llmProvider} --VectorStore ${vectorStore} ` +
            `-o "${outputPath}"`;

        vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'Creating Hazina project...',
            cancellable: false
        }, async () => {
            return new Promise((resolve, reject) => {
                child_process.exec(command, (error, stdout, stderr) => {
                    if (error) {
                        vscode.window.showErrorMessage(`Failed: ${stderr}`);
                        reject(error);
                    } else {
                        vscode.window.showInformationMessage(
                            'Hazina project created successfully!'
                        );

                        // Open the new project
                        vscode.commands.executeCommand(
                            'vscode.openFolder',
                            vscode.Uri.file(outputPath)
                        );

                        resolve(undefined);
                    }
                });
            });
        });
    }
}
```

---

## Implementation Tasks

### Week 1: dotnet new Template
- [ ] Create template directory structure
- [ ] Write template.json configuration
- [ ] Create template files with conditionals
- [ ] Test template locally
- [ ] Create NuGet package

### Week 2: Visual Studio Extension
- [ ] Create VSIX project
- [ ] Implement project wizard UI
- [ ] Connect wizard to template engine
- [ ] Test in Visual Studio
- [ ] Create VSIX installer

### Week 3: VS Code Extension
- [ ] Create VS Code extension project
- [ ] Implement new project command
- [ ] Create webview wizard (optional)
- [ ] Test extension
- [ ] Publish to marketplace

### Week 4: Polish
- [ ] Documentation
- [ ] Screenshots and demo videos
- [ ] Marketplace listings
- [ ] Integration tests

---

## Distribution

### NuGet (dotnet new templates)
```
Package: Hazina.Templates
Contains: hazina-rag, hazina-rag-minimal, hazina-worker, hazina-console
```

### Visual Studio Marketplace
```
Extension: Hazina AI
Contains: Project templates, wizard, snippets
```

### VS Code Marketplace
```
Extension: Hazina AI
Contains: New project command, snippets
```

---

## Success Criteria

- [ ] `dotnet new hazina-rag` creates working project
- [ ] Visual Studio wizard provides guided experience
- [ ] VS Code extension works across platforms
- [ ] All templates produce compiling code
- [ ] Documentation includes screenshots

---

**Next Document:** [05-AI_BUILD_ORCHESTRATOR.md](./05-AI_BUILD_ORCHESTRATOR.md)
