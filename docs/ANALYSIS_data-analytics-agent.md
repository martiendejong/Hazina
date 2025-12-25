# Analysis: data-analytics-agent Repository

## Repository Overview
**Source**: https://github.com/agency-ai-solutions/data-analytics-agent
**Purpose**: AI-powered data analyst that automates analytics workflows across multiple platforms
**Framework**: agency-swarm (Python-based multi-agent framework)
**Deployment**: Docker containerized, 60-second setup

---

## Architecture Analysis

### 1. Agent Pattern
**Single Autonomous Agent Model**
- One specialized `data_analyst` agent with 4 core tools
- No multi-agent communication flows
- Operates within agency_swarm framework wrapper
- Simple orchestration with minimal configuration

### 2. Core Tools (4-Tool System)
```
1. IPythonInterpreter - Execute Python code for data analysis
2. LocalShellTool - File system operations
3. WebSearchTool - API documentation lookup
4. load_images - Multimodal visual chart analysis
```

### 3. Key Innovation: Dynamic API Integration
**Self-Directed API Discovery**
- NO pre-configured MCP servers or connectors
- Agent reads API documentation on-demand via web search
- Dynamically generates API requests for ANY platform
- Platform-agnostic approach (Google Analytics, Stripe, PostgreSQL, MongoDB, etc.)

### 4. Multimodal Analysis Pattern
**Human-Like Workflow**
```
1. Generate data visualizations (matplotlib, plotly, seaborn)
2. Save charts as images
3. Load images back into context
4. Visually analyze the charts (like a human analyst)
5. Provide insights based on visual interpretation
```

### 5. Instruction Architecture
**Templated Business Context**
- `shared_instructions.md` with template placeholders
- Dynamic personalization: {company_name}, {business_overview}, {business_goals}
- Single codebase → multiple deployments via config
- Agent-specific instructions in separate file

### 6. Workflow Methodology (5-Step Process)
```
1. Clarify questions → identify metrics
2. Search web → find API docs
3. Fetch data → Python analysis
4. Generate visualizations → load images
5. Deliver quantified insights → actionable recommendations
```

---

## Technical Stack

### Dependencies
**Core Framework**
- agency-swarm (with FastAPI and LiteLLM support)
- openai-agents (v0.3.3)

**Data Science**
- pandas, numpy, scipy
- statsmodels, scikit-learn

**Visualization**
- matplotlib, seaborn, plotly

**Database Connectors**
- PostgreSQL, MongoDB, Redis
- SQLAlchemy

**Third-Party API Integrations**
- Google Analytics, BigQuery, Cloud Storage
- Stripe, Mixpanel, Segment, Amplitude
- HubSpot, Salesforce
- Slack, Notion
- Airtable, Google Sheets

### Deployment Strategy
**Docker Configuration**
- Base: Python 3.13-slim
- Unbuffered output for logging
- No .pyc file generation
- Entry point: `python -u main.py`
- Credentials stored in container-local `/credentials/` directory
- Environment-based configuration via .env

---

## What We Can Learn & Implement

### 1. ✅ **Dynamic API Integration Pattern**
**Concept**: Agent discovers and calls APIs without pre-built connectors

**How it works**:
- Agent uses web search to find API documentation
- Parses documentation to understand endpoints
- Generates appropriate API requests dynamically
- Handles authentication from stored credentials

**Implementation for Hazina**:
```csharp
// New Tool: WebSearchTool
public class WebSearchTool : HazinaChatTool
{
    public WebSearchTool() : base(
        "web_search",
        "Search the web for API documentation, code examples, or technical information",
        new List<ChatToolParameter> {
            new ChatToolParameter { Name = "query", Type = "string", Description = "Search query" }
        },
        async (messages, call, cancel) => {
            // Use Bing/Google Search API
            // Parse and return relevant documentation
        }
    ) {}
}

// New Tool: DynamicAPITool
public class DynamicAPITool : HazinaChatTool
{
    public DynamicAPITool() : base(
        "call_api",
        "Make HTTP requests to any API endpoint based on discovered documentation",
        new List<ChatToolParameter> {
            new ChatToolParameter { Name = "url", Type = "string" },
            new ChatToolParameter { Name = "method", Type = "string" },
            new ChatToolParameter { Name = "headers", Type = "object" },
            new ChatToolParameter { Name = "body", Type = "object" }
        },
        async (messages, call, cancel) => {
            // Generic HTTP client
            // Load credentials from store
            // Execute request
        }
    ) {}
}
```

### 2. ✅ **Multimodal Visual Analysis**
**Concept**: Generate charts → Save → Load → Analyze visually

**Current Hazina State**:
- We support image input via `List<ImageData>` parameter
- LLMClient already handles multimodal requests

**Enhancement Needed**:
```csharp
// New Tool: SaveVisualization
public class SaveVisualizationTool : HazinaChatTool
{
    public SaveVisualizationTool(string outputPath) : base(
        "save_chart",
        "Save generated visualization to file for later visual analysis",
        new List<ChatToolParameter> {
            new ChatToolParameter { Name = "chart_data", Type = "string", Description = "Base64 encoded image or plot data" },
            new ChatToolParameter { Name = "filename", Type = "string" }
        },
        async (messages, call, cancel) => {
            // Save to outputPath
            // Return file path
        }
    ) {}
}

// New Tool: LoadImageForAnalysis
public class LoadImageTool : HazinaChatTool
{
    public LoadImageTool() : base(
        "load_image",
        "Load previously saved image/chart for visual analysis",
        new List<ChatToolParameter> {
            new ChatToolParameter { Name = "file_path", Type = "string" }
        },
        async (messages, call, cancel) => {
            // Load image file
            // Convert to ImageData
            // Trigger new LLM call with image in context
        }
    ) {}
}
```

### 3. ✅ **Templated Agent Instructions**
**Concept**: Single codebase with dynamic business context

**Current Hazina State**:
- Agents use BaseMessages for instructions
- Already supports per-agent configuration

**Enhancement**:
```csharp
public class AgentInstructionTemplate
{
    public string Template { get; set; }
    public Dictionary<string, string> Variables { get; set; }

    public string Render()
    {
        var result = Template;
        foreach (var kvp in Variables)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
        }
        return result;
    }
}

// Usage in AgentConfig
public class AgentConfig
{
    public string Name { get; set; }
    public string InstructionTemplate { get; set; } // Path to .md file
    public Dictionary<string, string> TemplateVariables { get; set; }
}
```

### 4. ✅ **Credential Management System**
**Concept**: Secure, containerized credential storage

**Implementation Recommendation**:
```csharp
public class CredentialStore
{
    private readonly string _credentialsPath;
    private readonly Dictionary<string, string> _cache = new();

    public CredentialStore(string credentialsPath)
    {
        _credentialsPath = credentialsPath;
    }

    public async Task<string> GetCredential(string serviceName, string keyName)
    {
        var cacheKey = $"{serviceName}:{keyName}";
        if (_cache.ContainsKey(cacheKey))
            return _cache[cacheKey];

        // Load from file: ./credentials/{serviceName}.json
        var filePath = Path.Combine(_credentialsPath, $"{serviceName}.json");
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath);
            var creds = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (creds?.ContainsKey(keyName) == true)
            {
                _cache[cacheKey] = creds[keyName];
                return creds[keyName];
            }
        }

        // Fallback to environment variables
        var envVar = $"{serviceName.ToUpper()}_{keyName.ToUpper()}";
        var value = Environment.GetEnvironmentVariable(envVar);
        if (value != null)
        {
            _cache[cacheKey] = value;
            return value;
        }

        throw new Exception($"Credential not found: {serviceName}/{keyName}");
    }
}

// Tool integration
public class ApiCallTool : HazinaChatTool
{
    private readonly CredentialStore _credStore;

    public ApiCallTool(CredentialStore credStore) : base(...)
    {
        _credStore = credStore;
    }

    private async Task<string> ExecuteCall(...)
    {
        // Auto-inject credentials
        var apiKey = await _credStore.GetCredential("stripe", "api_key");
        // Use in HTTP request
    }
}
```

### 5. ✅ **Data-First Analysis Principle**
**Concept**: "Never answer without analyzing data first"

**Implementation as Agent Instruction**:
```markdown
# Data Analyst Agent Instructions

## Core Principle
**NEVER provide answers or recommendations without first analyzing actual data.**

## Required Workflow
1. ✓ Identify data sources needed to answer the question
2. ✓ Fetch and validate data (check for nulls, date ranges, completeness)
3. ✓ Perform statistical analysis
4. ✓ Generate visualizations if trends are involved
5. ✓ Quantify findings with specific numbers
6. ✓ State data period analyzed
7. ✓ Cite data sources
8. ✓ Provide actionable recommendations

## Forbidden Actions
- ✗ Generic advice without data backing
- ✗ Assumptions about data without validation
- ✗ Answers based on "typical" patterns instead of actual data
- ✗ Visualizations without context or interpretation
```

### 6. ✅ **Python Execution Tool**
**Concept**: IPythonInterpreter for data analysis

**Implementation Path**:
```csharp
public class PythonExecutionTool : HazinaChatTool
{
    private readonly string _workingDir;

    public PythonExecutionTool(string workingDir) : base(
        "execute_python",
        "Execute Python code for data analysis, visualization, or computation. Supports pandas, numpy, matplotlib, etc.",
        new List<ChatToolParameter> {
            new ChatToolParameter { Name = "code", Type = "string", Description = "Python code to execute" },
            new ChatToolParameter { Name = "install_packages", Type = "array", Description = "Optional packages to pip install first" }
        },
        async (messages, call, cancel) => {
            // 1. Parse code from call
            // 2. Install packages if needed (pip install)
            // 3. Execute in sandboxed Python subprocess
            // 4. Capture stdout, stderr, and any saved files
            // 5. Return results + file paths
        }
    ) {}
}
```

**Alternative**: Use existing C# libraries
- Math.NET for scientific computing
- ScottPlot for visualization
- But Python has better ecosystem for data science

### 7. ⚠️ **Simplified Tool Architecture**
**Their Approach**: 4 tools only
**Our Current State**: Multiple specialized tools

**Recommendation**: Create "tool bundles" for specific domains
```csharp
public class DataAnalystToolBundle
{
    public static List<HazinaChatTool> GetTools(CredentialStore credStore, string outputPath)
    {
        return new List<HazinaChatTool>
        {
            new PythonExecutionTool(outputPath),
            new WebSearchTool(),
            new DynamicAPITool(credStore),
            new LoadImageTool(),
            new SaveVisualizationTool(outputPath)
        };
    }
}

// Usage in AgentConfig
var agent = new HazinaAgent(
    "data_analyst",
    generator,
    new ToolsContext {
        Tools = DataAnalystToolBundle.GetTools(credStore, "./outputs")
    }
);
```

---

## Comparison: Hazina vs data-analytics-agent

| Feature | data-analytics-agent | Hazina | Action |
|---------|---------------------|--------|--------|
| Multi-agent support | ❌ Single agent | ✅ Multiple agents + flows | Keep our advantage |
| Dynamic API calls | ✅ Via web search + generic HTTP | ❌ Pre-configured tools | **IMPLEMENT** |
| Multimodal analysis | ✅ Generate → Save → Load → Analyze | ⚠️ Input only, no loop | **ENHANCE** |
| Credential management | ✅ File + env based | ⚠️ Per-tool hardcoded | **CENTRALIZE** |
| Python execution | ✅ IPython interpreter | ❌ No sandboxed execution | **ADD TOOL** |
| Visualization | ✅ matplotlib, plotly, seaborn | ❌ No native support | **ADD VIA PYTHON** |
| Instruction templates | ✅ Dynamic with variables | ⚠️ Static per agent | **ENHANCE** |
| Token tracking | ❌ None visible | ✅ Full LLMResponse tracking | Our advantage ✓ |
| Document store | ❌ File-based only | ✅ Multiple backends | Our advantage ✓ |
| Streaming responses | ❌ Not highlighted | ✅ Full support | Our advantage ✓ |

---

## Implementation Priority

### 🔴 HIGH Priority (Immediate Value)
1. **Credential Store** - Centralized, secure credential management
2. **Dynamic API Tool** - Call any API without pre-configuration
3. **Web Search Tool** - Find documentation/info dynamically
4. **Templated Instructions** - Reusable agent configs with variables

### 🟡 MEDIUM Priority (Next Quarter)
5. **Python Execution Tool** - Data analysis & visualization
6. **Multimodal Loop** - Save → Load → Analyze pattern
7. **Tool Bundles** - Pre-configured tool sets for domains

### 🟢 LOW Priority (Future Consideration)
8. **Docker Deployment Template** - Like their 60-second setup
9. **Marketplace Integration** - Deploy agents as services
10. **Agency Swarm Evaluation** - Consider framework migration?

---

## Code Examples Ready for Integration

### Example 1: Credential Store
```csharp
// In AgentFactory or StoresAndAgentsLoader
var credStore = new CredentialStore("./credentials");

// Tools can now access credentials uniformly
var stripeKey = await credStore.GetCredential("stripe", "api_key");
var googleServiceAccount = await credStore.GetCredential("google_analytics", "service_account_json");
```

### Example 2: Dynamic API Tool Integration
```csharp
var tools = new ToolsContext();
tools.Add(new WebSearchTool());
tools.Add(new DynamicAPITool(credStore));

var agent = new HazinaAgent("api_researcher", generator, tools);

// Agent can now discover and call ANY API
var response = await agent.Generator.GetResponse(
    "Fetch my Stripe revenue for last month",
    cancel
);
// Agent will:
// 1. Search web for Stripe API docs
// 2. Find /v1/balance_transactions endpoint
// 3. Load API key from credStore
// 4. Make authenticated request
// 5. Return structured data
```

### Example 3: Templated Agent Config
```json
{
  "name": "sales_analyst",
  "instruction_template": "./templates/analyst_instructions.md",
  "template_variables": {
    "company_name": "Acme Corp",
    "business_overview": "B2B SaaS platform for project management",
    "business_goals": "Increase MRR by 20%, reduce churn to <5%",
    "data_sources": "Stripe, Google Analytics, Salesforce"
  },
  "tools": ["python", "web_search", "api_call", "load_image"]
}
```

---

## Conclusion

The data-analytics-agent repository demonstrates several powerful patterns:

**Most Valuable Learnings**:
1. **Dynamic API integration** without pre-built connectors
2. **Multimodal analysis loop** (generate → save → load → analyze)
3. **Credential abstraction** for security and portability
4. **Templated instructions** for deployment flexibility
5. **Data-first methodology** as core agent principle

**Recommended Next Steps**:
1. Implement CredentialStore class
2. Add WebSearchTool and DynamicAPITool
3. Enhance agent instruction system with templates
4. Evaluate Python execution sandbox for data analysis
5. Create multimodal analysis workflow for chart interpretation

**Our Competitive Advantages to Maintain**:
- Multi-agent orchestration with flows
- Token usage tracking and cost transparency
- Document store abstraction (multiple backends)
- Streaming response support
- Established tool ecosystem

By selectively adopting their patterns while keeping our strengths, we can create a more powerful and flexible agent platform.
