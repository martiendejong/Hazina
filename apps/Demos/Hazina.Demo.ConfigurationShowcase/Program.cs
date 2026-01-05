using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.FluentAPI.Core;
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Hazina.AI.RAG.Core;
using Hazina.Neurochain.Core;
using Hazina.Neurochain.Core.Layers;
using HazinaStore.Models;
using Hazina.Tools.Data;
using Hazina.LLMs; // All LLM providers are in this namespace
using Hazina.Production.Monitoring.Metrics;
using Microsoft.Extensions.Configuration;

namespace Hazina.Demo.ConfigurationShowcase;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   HAZINA CONFIGURATION SHOWCASE                                ║");
        Console.WriteLine("║   Demonstrating all configurable components                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load configuration from environment variables
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        while (true)
        {
            Console.WriteLine("\nSelect a configuration scenario:");
            Console.WriteLine("1. Storage Backends (File-based, Supabase, PostgreSQL, Hybrid)");
            Console.WriteLine("2. LLM Provider Strategies (Priority, Cost, Speed, Failover)");
            Console.WriteLine("3. AI Components Integration (RAG, Neurochain, Agents)");
            Console.WriteLine("4. Production Monitoring (Metrics, Health, Cost Tracking)");
            Console.WriteLine("5. Complete End-to-End Example");
            Console.WriteLine("6. Configuration Best Practices");
            Console.WriteLine("0. Exit");
            Console.Write("\nChoice: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await DemoStorageBackends(configuration);
                        break;
                    case "2":
                        await DemoProviderStrategies(configuration);
                        break;
                    case "3":
                        await DemoAIComponentsIntegration(configuration);
                        break;
                    case "4":
                        await DemoProductionMonitoring(configuration);
                        break;
                    case "5":
                        await DemoEndToEnd(configuration);
                        break;
                    case "6":
                        DisplayBestPractices();
                        break;
                    case "0":
                        Console.WriteLine("\nGoodbye!");
                        return;
                    default:
                        Console.WriteLine("\nInvalid choice. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine("\nMake sure required environment variables are set.");
            }
        }
    }

    static async Task DemoStorageBackends(IConfiguration config)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   STORAGE BACKEND CONFIGURATIONS                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("Select storage backend:");
        Console.WriteLine("1. File-based (Default - No database needed)");
        Console.WriteLine("2. Supabase (Cloud PostgreSQL with pgvector)");
        Console.WriteLine("3. PostgreSQL (Self-hosted with pgvector)");
        Console.WriteLine("4. Hybrid (Local files + Cloud embeddings)");
        Console.Write("\nChoice: ");

        var choice = Console.ReadLine();

        var baseConfig = new HazinaStoreConfig
        {
            ApiSettings = new ApiSettings
            {
                OpenApiKey = config["OPENAI_API_KEY"] ?? throw new Exception("OPENAI_API_KEY not set")
            }
        };

        switch (choice)
        {
            case "1":
                Console.WriteLine("\n📁 FILE-BASED STORAGE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━");

                baseConfig.ProjectSettings = new ProjectSettings
                {
                    ProjectsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hazina-data")
                };

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Storage: File-based");
                Console.WriteLine($"  - Location: {baseConfig.ProjectSettings.ProjectsFolder}");
                Console.WriteLine($"  - Database: None required");
                Console.WriteLine($"  - Use case: Local development, prototyping");

                var fileStoreSetup = StoreProvider.GetStoreSetup(baseConfig, "demo-project", 1536);

                Console.WriteLine($"\n✓ Store setup created:");
                Console.WriteLine($"  - Vector Store: {fileStoreSetup.TextEmbeddingStore?.GetType().Name}");
                Console.WriteLine($"  - Document Store: {fileStoreSetup.Store?.GetType().Name}");
                Console.WriteLine($"  - Metadata Store: {fileStoreSetup.QueryableMetadataStore?.GetType().Name}");
                break;

            case "2":
                Console.WriteLine("\n☁️  SUPABASE STORAGE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━");

                baseConfig.SupabaseSettings = new SupabaseSettings
                {
                    Enabled = true,
                    Url = config["SUPABASE_URL"] ?? throw new Exception("SUPABASE_URL not set"),
                    AnonKey = config["SUPABASE_ANON_KEY"] ?? throw new Exception("SUPABASE_ANON_KEY not set"),
                    ConnectionString = config["SUPABASE_CONNECTION_STRING"] ?? throw new Exception("SUPABASE_CONNECTION_STRING not set")
                };

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Storage: Supabase (Cloud PostgreSQL)");
                Console.WriteLine($"  - URL: {baseConfig.SupabaseSettings.Url}");
                Console.WriteLine($"  - Features: pgvector, JSONB metadata, cloud scalability");
                Console.WriteLine($"  - Use case: Production, multi-user, scalable");

                // Test connection
                Console.WriteLine("\n⏳ Testing Supabase connection...");
                var connectionOk = await SupabaseStoreProvider.TestConnectionAsync(baseConfig.SupabaseSettings.ConnectionString);

                if (connectionOk)
                {
                    Console.WriteLine("✓ Connection successful!");

                    // Initialize schema
                    Console.WriteLine("\n⏳ Initializing database schema...");
                    await SupabaseStoreProvider.InitializeSupabaseSchemaAsync(baseConfig.SupabaseSettings.ConnectionString, 1536);
                    Console.WriteLine("✓ Schema initialized!");

                    var supabaseSetup = SupabaseStoreProvider.GetSupabaseStoreSetup(
                        baseConfig.SupabaseSettings,
                        baseConfig.ApiSettings.OpenApiKey,
                        1536
                    );

                    Console.WriteLine($"\n✓ Store setup created:");
                    Console.WriteLine($"  - Vector Store: {supabaseSetup.TextEmbeddingStore?.GetType().Name}");
                    Console.WriteLine($"  - Document Store: {supabaseSetup.Store?.GetType().Name}");
                    Console.WriteLine($"  - Metadata Store: {supabaseSetup.QueryableMetadataStore?.GetType().Name}");
                }
                else
                {
                    Console.WriteLine("❌ Connection failed! Check your credentials.");
                }
                break;

            case "3":
                Console.WriteLine("\n🐘 POSTGRESQL STORAGE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━");

                var pgConnectionString = config["POSTGRES_CONNECTION_STRING"] ??
                    throw new Exception("POSTGRES_CONNECTION_STRING not set");

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Storage: PostgreSQL (Self-hosted)");
                Console.WriteLine($"  - Features: pgvector, JSONB metadata, full control");
                Console.WriteLine($"  - Use case: Enterprise, on-premise, data sovereignty");

                baseConfig.SupabaseSettings = new SupabaseSettings
                {
                    Enabled = true,
                    ConnectionString = pgConnectionString
                };

                Console.WriteLine("\n⏳ Testing PostgreSQL connection...");
                var pgConnectionOk = await SupabaseStoreProvider.TestConnectionAsync(pgConnectionString);

                if (pgConnectionOk)
                {
                    Console.WriteLine("✓ Connection successful!");

                    var pgSetup = SupabaseStoreProvider.GetSupabaseStoreSetup(
                        baseConfig.SupabaseSettings,
                        baseConfig.ApiSettings.OpenApiKey,
                        1536
                    );

                    Console.WriteLine($"\n✓ Store setup created:");
                    Console.WriteLine($"  - Vector Store: {pgSetup.TextEmbeddingStore?.GetType().Name}");
                    Console.WriteLine($"  - Using pgvector extension");
                }
                else
                {
                    Console.WriteLine("❌ Connection failed! Check your credentials.");
                }
                break;

            case "4":
                Console.WriteLine("\n🔀 HYBRID STORAGE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━");

                baseConfig.ProjectSettings = new ProjectSettings
                {
                    ProjectsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hazina-data")
                };

                baseConfig.SupabaseSettings = new SupabaseSettings
                {
                    Enabled = true,
                    Url = config["SUPABASE_URL"] ?? throw new Exception("SUPABASE_URL not set"),
                    AnonKey = config["SUPABASE_ANON_KEY"] ?? throw new Exception("SUPABASE_ANON_KEY not set"),
                    ConnectionString = config["SUPABASE_CONNECTION_STRING"] ?? throw new Exception("SUPABASE_CONNECTION_STRING not set")
                };

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Storage: Hybrid mode");
                Console.WriteLine($"  - Files: Local ({baseConfig.ProjectSettings.ProjectsFolder})");
                Console.WriteLine($"  - Embeddings: Supabase (Cloud)");
                Console.WriteLine($"  - Use case: Fast file access + Cloud semantic search");

                var hybridSetup = SupabaseStoreProvider.GetHybridStoreSetup(
                    Path.Combine(baseConfig.ProjectSettings.ProjectsFolder, "demo-project"),
                    baseConfig.SupabaseSettings,
                    baseConfig.ApiSettings.OpenApiKey,
                    1536
                );

                Console.WriteLine($"\n✓ Store setup created:");
                Console.WriteLine($"  - Vector Store: {hybridSetup.TextEmbeddingStore?.GetType().Name} (Cloud)");
                Console.WriteLine($"  - Document Store: {hybridSetup.Store?.GetType().Name} (Local)");
                Console.WriteLine($"  - Best of both worlds!");
                break;

            default:
                Console.WriteLine("\nInvalid choice.");
                break;
        }
    }

    static async Task DemoProviderStrategies(IConfiguration config)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   LLM PROVIDER STRATEGIES                                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("Select provider strategy:");
        Console.WriteLine("1. Single Provider (OpenAI only)");
        Console.WriteLine("2. Priority-based (OpenAI → Anthropic → Ollama)");
        Console.WriteLine("3. Cost-optimized (Always cheapest)");
        Console.WriteLine("4. Speed-optimized (Always fastest)");
        Console.WriteLine("5. Failover (Automatic fallback)");
        Console.WriteLine("6. Round-robin (Load distribution)");
        Console.Write("\nChoice: ");

        var choice = Console.ReadLine();

        var openAIKey = config["OPENAI_API_KEY"];
        var anthropicKey = config["ANTHROPIC_API_KEY"];

        switch (choice)
        {
            case "1":
                Console.WriteLine("\n🔷 SINGLE PROVIDER");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━");

                if (string.IsNullOrEmpty(openAIKey))
                {
                    Console.WriteLine("❌ OPENAI_API_KEY not set");
                    return;
                }

                var singleOrchestrator = QuickSetup.SetupOpenAI(openAIKey, "gpt-4o-mini");

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Provider: OpenAI");
                Console.WriteLine($"  - Model: gpt-4o-mini");
                Console.WriteLine($"  - Fallback: None");
                Console.WriteLine($"  - Use case: Simple, predictable");

                Console.WriteLine("\n⏳ Testing...");
                var result1 = await singleOrchestrator.GetResponse("Say 'Hello from OpenAI!'", "gpt-4o-mini", 0.7);
                Console.WriteLine($"✓ Response: {result1.Text}");
                break;

            case "2":
                Console.WriteLine("\n📊 PRIORITY-BASED STRATEGY");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━");

                var orchestrator = new ProviderOrchestrator();
                orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

                if (!string.IsNullOrEmpty(openAIKey))
                {
                    var openaiConfig = new OpenAIConfig { ApiKey = openAIKey, Model = "gpt-4o-mini" };
                    var openaiClient = new OpenAIClientWrapper(openaiConfig);
                    orchestrator.RegisterProvider("openai", openaiClient, new ProviderMetadata
                    {
                        Name = "openai",
                        Priority = 1,
                        Capabilities = new ProviderCapabilities { SupportsChat = true }
                    });
                    Console.WriteLine("✓ Registered OpenAI (Priority: 1 - Highest)");
                }

                if (!string.IsNullOrEmpty(anthropicKey))
                {
                    var anthropicConfig = new AnthropicConfig { ApiKey = anthropicKey, Model = "claude-3-5-sonnet-20241022" };
                    var anthropicClient = new ClaudeClientWrapper(anthropicConfig);
                    orchestrator.RegisterProvider("anthropic", anthropicClient, new ProviderMetadata
                    {
                        Name = "anthropic",
                        Priority = 2,
                        Capabilities = new ProviderCapabilities { SupportsChat = true }
                    });
                    Console.WriteLine("✓ Registered Anthropic (Priority: 2)");
                }

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Strategy: Priority-based");
                Console.WriteLine($"  - Order: OpenAI → Anthropic");
                Console.WriteLine($"  - Use case: Reliability with fallback chain");

                Console.WriteLine("\n⏳ Testing...");
                var result2 = await orchestrator.GetResponse("What is 2+2?", "gpt-4o-mini", 0.7);
                Console.WriteLine($"✓ Response: {result2.Text}");
                break;

            case "3":
                Console.WriteLine("\n💰 COST-OPTIMIZED STRATEGY");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━");

                if (string.IsNullOrEmpty(openAIKey))
                {
                    Console.WriteLine("❌ OPENAI_API_KEY not set");
                    return;
                }

                var costOrchestrator = QuickSetup.SetupCostOptimized(openAIKey, anthropicKey);

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Strategy: Least cost");
                Console.WriteLine($"  - Tracks: All costs in real-time");
                Console.WriteLine($"  - Use case: Cost-sensitive applications");

                Console.WriteLine("\n⏳ Testing multiple requests...");
                for (int i = 0; i < 3; i++)
                {
                    await costOrchestrator.GetResponse($"Count to {i + 1}", "gpt-4o-mini", 0.7);
                    Console.WriteLine($"  Request {i + 1}: Completed");
                }
                break;

            case "4":
                Console.WriteLine("\n⚡ SPEED-OPTIMIZED STRATEGY");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━");

                var speedOrchestrator = new ProviderOrchestrator();
                speedOrchestrator.SetDefaultStrategy(SelectionStrategy.FastestResponse);

                if (!string.IsNullOrEmpty(openAIKey))
                {
                    var openaiConfig = new OpenAIConfig { ApiKey = openAIKey, Model = "gpt-4o-mini" };
                    speedOrchestrator.RegisterProvider("openai", new OpenAIClientWrapper(openaiConfig), new ProviderMetadata
                    {
                        Name = "openai",
                        Capabilities = new ProviderCapabilities { SupportsChat = true }
                    });
                }

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Strategy: Fastest response");
                Console.WriteLine($"  - Tracks: Response times per provider");
                Console.WriteLine($"  - Use case: Latency-sensitive applications");

                Console.WriteLine("\n⏳ Testing...");
                var startTime = DateTime.UtcNow;
                var result4 = await speedOrchestrator.GetResponse("Quick: what is 5+5?", "gpt-4o-mini", 0.7);
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Console.WriteLine($"✓ Response in {elapsed:F0}ms: {result4.Text}");
                break;

            case "5":
                Console.WriteLine("\n🔄 FAILOVER STRATEGY");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━");

                if (string.IsNullOrEmpty(openAIKey) || string.IsNullOrEmpty(anthropicKey))
                {
                    Console.WriteLine("❌ Both OPENAI_API_KEY and ANTHROPIC_API_KEY required");
                    return;
                }

                var failoverOrchestrator = QuickSetup.SetupWithFailover(openAIKey, anthropicKey);
                Hazina.ConfigureDefaultOrchestrator(failoverOrchestrator);

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Primary: OpenAI");
                Console.WriteLine($"  - Fallback: Anthropic");
                Console.WriteLine($"  - Circuit breaker: Enabled");
                Console.WriteLine($"  - Use case: High availability required");

                Console.WriteLine("\n⏳ Testing...");
                var result5 = await Hazina.AskAsync("What is the capital of France?");
                Console.WriteLine($"✓ Response: {result5}");
                break;

            case "6":
                Console.WriteLine("\n🔁 ROUND-ROBIN STRATEGY");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━");

                var rrOrchestrator = new ProviderOrchestrator();
                rrOrchestrator.SetDefaultStrategy(SelectionStrategy.RoundRobin);

                if (!string.IsNullOrEmpty(openAIKey))
                {
                    var openaiConfig = new OpenAIConfig { ApiKey = openAIKey, Model = "gpt-4o-mini" };
                    rrOrchestrator.RegisterProvider("openai", new OpenAIClientWrapper(openaiConfig), new ProviderMetadata
                    {
                        Name = "openai",
                        Capabilities = new ProviderCapabilities { SupportsChat = true }
                    });
                }
                if (!string.IsNullOrEmpty(anthropicKey))
                {
                    var anthropicConfig = new AnthropicConfig { ApiKey = anthropicKey, Model = "claude-3-5-sonnet-20241022" };
                    rrOrchestrator.RegisterProvider("anthropic", new ClaudeClientWrapper(anthropicConfig), new ProviderMetadata
                    {
                        Name = "anthropic",
                        Capabilities = new ProviderCapabilities { SupportsChat = true }
                    });
                }

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Strategy: Round-robin");
                Console.WriteLine($"  - Distributes: Load evenly across providers");
                Console.WriteLine($"  - Use case: Load balancing, testing");

                Console.WriteLine("\n⏳ Testing 3 requests...");
                for (int i = 0; i < 3; i++)
                {
                    await rrOrchestrator.GetResponse($"Request {i + 1}", "gpt-4o-mini", 0.7);
                    Console.WriteLine($"  Request {i + 1} completed");
                }
                break;

            default:
                Console.WriteLine("\nInvalid choice.");
                break;
        }
    }

    static async Task DemoAIComponentsIntegration(IConfiguration config)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   AI COMPONENTS INTEGRATION                                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        var openAIKey = config["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAIKey))
        {
            Console.WriteLine("❌ OPENAI_API_KEY not set");
            return;
        }

        Console.WriteLine("Select component:");
        Console.WriteLine("1. RAG (Retrieval-Augmented Generation)");
        Console.WriteLine("2. Neurochain (Multi-layer reasoning)");
        Console.WriteLine("3. Combined (RAG + Neurochain)");
        Console.Write("\nChoice: ");

        var choice = Console.ReadLine();

        var orchestrator = QuickSetup.SetupOpenAI(openAIKey, "gpt-4o-mini");

        switch (choice)
        {
            case "1":
                Console.WriteLine("\n📚 RAG ENGINE");
                Console.WriteLine("━━━━━━━━━━━━━");

                // Setup file-based storage for demo
                var config1 = new HazinaStoreConfig
                {
                    ApiSettings = new ApiSettings { OpenApiKey = openAIKey },
                    ProjectSettings = new ProjectSettings
                    {
                        ProjectsFolder = Path.Combine(Path.GetTempPath(), "hazina-rag-demo")
                    }
                };

                var storeSetup = StoreProvider.GetStoreSetup(config1, "rag-demo", 1536);
                var ragEngine = new RAGEngine(orchestrator, storeSetup.TextEmbeddingStore!);

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Storage: File-based");
                Console.WriteLine($"  - Features: Semantic search, context building");

                // Index sample documents
                Console.WriteLine("\n⏳ Indexing sample documents...");
                await ragEngine.IndexDocumentAsync("doc1", "Paris is the capital of France. It is known for the Eiffel Tower.");
                await ragEngine.IndexDocumentAsync("doc2", "London is the capital of the United Kingdom. It has Big Ben.");
                await ragEngine.IndexDocumentAsync("doc3", "Berlin is the capital of Germany. It has the Brandenburg Gate.");
                Console.WriteLine("✓ Indexed 3 documents");

                // Query
                Console.WriteLine("\n⏳ Querying: 'What is the capital of France?'");
                var ragResponse = await ragEngine.QueryAsync("What is the capital of France?");
                Console.WriteLine($"\n✓ Response: {ragResponse.Answer}");
                Console.WriteLine($"✓ Confidence: {ragResponse.Confidence:P0}");
                Console.WriteLine($"✓ Sources: {ragResponse.RetrievedChunks.Count} documents");
                break;

            case "2":
                Console.WriteLine("\n🧠 NEUROCHAIN (Multi-layer Reasoning)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                var neurochain = new NeuroChainOrchestrator();

                neurochain.AddLayer(new FastReasoningLayer(orchestrator));
                neurochain.AddLayer(new DeepReasoningLayer(orchestrator));

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - Layers: 2 (Fast, Deep)");
                Console.WriteLine($"  - Features: Multi-perspective reasoning");

                Console.WriteLine("\n⏳ Reasoning: 'What is the square root of 256?'");
                var neurochainResult = await neurochain.ReasonAsync(
                    "What is the square root of 256?",
                    new ReasoningContext { MinConfidence = 0.9 }
                );

                Console.WriteLine($"\n✓ Final Answer: {neurochainResult.FinalAnswer}");
                Console.WriteLine($"✓ Confidence: {neurochainResult.FinalConfidence:P0}");
                Console.WriteLine($"✓ Layers used: {neurochainResult.LayerResults.Count}");
                Console.WriteLine($"✓ Total cost: ${neurochainResult.TotalCost:F6}");
                Console.WriteLine($"✓ Total time: {neurochainResult.TotalDurationMs}ms");
                break;

            case "3":
                Console.WriteLine("\n🔗 COMBINED (RAG + Neurochain)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // Setup RAG
                var config3 = new HazinaStoreConfig
                {
                    ApiSettings = new ApiSettings { OpenApiKey = openAIKey },
                    ProjectSettings = new ProjectSettings
                    {
                        ProjectsFolder = Path.Combine(Path.GetTempPath(), "hazina-combined-demo")
                    }
                };

                var storeSetup3 = StoreProvider.GetStoreSetup(config3, "combined-demo", 1536);
                var ragEngine3 = new RAGEngine(orchestrator, storeSetup3.TextEmbeddingStore!);

                // Setup Neurochain
                var neurochain3 = new NeuroChainOrchestrator();
                neurochain3.AddLayer(new FastReasoningLayer(orchestrator));
                neurochain3.AddLayer(new DeepReasoningLayer(orchestrator));

                Console.WriteLine($"\n✓ Configuration:");
                Console.WriteLine($"  - RAG: Retrieves context from knowledge base");
                Console.WriteLine($"  - Neurochain: Multi-layer reasoning on context");
                Console.WriteLine($"  - Result: High-confidence, context-aware answers");

                // Index sample documents
                Console.WriteLine("\n⏳ Indexing sample documents...");
                await ragEngine3.IndexDocumentAsync("climate1", "Global warming is causing sea levels to rise at an accelerating rate.");
                await ragEngine3.IndexDocumentAsync("climate2", "Renewable energy sources like solar and wind are becoming more efficient.");
                Console.WriteLine("✓ Indexed 2 documents");

                // Query with RAG
                Console.WriteLine("\n⏳ Step 1: RAG retrieval");
                var ragResult = await ragEngine3.QueryAsync("What is happening to sea levels?");
                Console.WriteLine($"✓ Retrieved {ragResult.RetrievedChunks.Count} relevant documents");

                // Reason with Neurochain using RAG context
                Console.WriteLine("\n⏳ Step 2: Multi-layer reasoning");
                var combinedResult = await neurochain3.ReasonAsync(
                    $"Based on this context: {ragResult.RetrievedChunks.FirstOrDefault()?.Text}\n\nQuestion: What is happening to sea levels and why?",
                    new ReasoningContext { MinConfidence = 0.85 }
                );

                Console.WriteLine($"\n✓ Final Answer: {combinedResult.FinalAnswer}");
                Console.WriteLine($"✓ Confidence: {combinedResult.FinalConfidence:P0}");
                Console.WriteLine($"✓ Context-aware: Yes");
                Console.WriteLine($"✓ Multi-validated: Yes");
                break;

            default:
                Console.WriteLine("\nInvalid choice.");
                break;
        }
    }

    static async Task DemoProductionMonitoring(IConfiguration config)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   PRODUCTION MONITORING                                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        var openAIKey = config["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAIKey))
        {
            Console.WriteLine("❌ OPENAI_API_KEY not set");
            return;
        }

        Console.WriteLine("🔍 METRICS & HEALTH MONITORING");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // Setup orchestrator
        var orchestrator = QuickSetup.SetupOpenAI(openAIKey);

        // Setup metrics collector
        var metrics = new MetricsCollector();

        Console.WriteLine($"\n✓ Configuration:");
        Console.WriteLine($"  - Health monitoring: Enabled");
        Console.WriteLine($"  - Cost tracking: Enabled");
        Console.WriteLine($"  - Metrics: Custom collector");

        Console.WriteLine("\n⏳ Simulating production workload...");

        // Simulate multiple requests with metrics
        for (int i = 0; i < 5; i++)
        {
            using (metrics.TimeOperation("llm_request", new Dictionary<string, string>
            {
                ["provider"] = "openai",
                ["model"] = "gpt-4o-mini"
            }))
            {
                try
                {
                    await orchestrator.GetResponse($"Quick test {i + 1}", "gpt-4o-mini", 0.7);

                    // Track success
                    metrics.IncrementCounter("llm_requests_total", new Dictionary<string, string>
                    {
                        ["status"] = "success",
                        ["provider"] = "openai"
                    });

                    Console.WriteLine($"  ✓ Request {i + 1}: Success");
                }
                catch (Exception ex)
                {
                    metrics.IncrementCounter("llm_requests_total", new Dictionary<string, string>
                    {
                        ["status"] = "error",
                        ["provider"] = "openai"
                    });
                    Console.WriteLine($"  ❌ Request {i + 1}: {ex.Message}");
                }
            }

            await Task.Delay(100); // Simulate realistic spacing
        }

        // Display metrics
        Console.WriteLine("\n📈 METRICS:");
        Console.WriteLine($"  Total requests: {metrics.GetCounterValue("llm_requests_total")}");

        var timing = metrics.GetOperationStats("llm_request");
        if (timing != null)
        {
            Console.WriteLine($"  Average response time: {timing.Mean:F0}ms");
            Console.WriteLine($"  P95 response time: {timing.P95:F0}ms");
            Console.WriteLine($"  Success rate: {timing.SuccessRate:P0}");
        }

        Console.WriteLine("\n✅ Monitoring complete!");
    }

    static async Task DemoEndToEnd(IConfiguration config)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   COMPLETE END-TO-END EXAMPLE                                  ║");
        Console.WriteLine("║   Production-ready configuration with all features             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        var openAIKey = config["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAIKey))
        {
            Console.WriteLine("❌ OPENAI_API_KEY required");
            return;
        }

        Console.WriteLine("🚀 PRODUCTION SETUP");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━");

        // Setup orchestrator
        var orchestrator = QuickSetup.SetupOpenAI(openAIKey);
        Console.WriteLine("✓ Provider: OpenAI configured");

        // Setup storage
        var storeConfig = new HazinaStoreConfig
        {
            ApiSettings = new ApiSettings { OpenApiKey = openAIKey },
            ProjectSettings = new ProjectSettings
            {
                ProjectsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hazina-production")
            }
        };

        var storeSetup = StoreProvider.GetStoreSetup(storeConfig, "production", 1536);
        Console.WriteLine("✓ Storage: File-based configured");

        // Setup AI components
        var ragEngine = new RAGEngine(orchestrator, storeSetup.TextEmbeddingStore!);
        var neurochain = new NeuroChainOrchestrator();
        neurochain.AddLayer(new FastReasoningLayer(orchestrator));
        neurochain.AddLayer(new DeepReasoningLayer(orchestrator));

        Console.WriteLine("✓ RAG Engine: Configured");
        Console.WriteLine("✓ Neurochain: 2 layers");

        // Setup monitoring
        var metrics = new MetricsCollector();
        Console.WriteLine("✓ Monitoring: Metrics collector active");

        Console.WriteLine("\n✅ Production system ready!");

        // Example workflow
        Console.WriteLine("\n⏳ Running example workflow...");

        // Index knowledge
        Console.WriteLine("\n1. Indexing knowledge base...");
        await ragEngine.IndexDocumentAsync("product1", "Our product supports multi-cloud deployment on AWS, Azure, and GCP.");
        Console.WriteLine("   ✓ Indexed 1 document");

        // Query with RAG
        Console.WriteLine("\n2. RAG query: 'What clouds do you support?'");
        var ragResult = await ragEngine.QueryAsync("What clouds do you support?");
        Console.WriteLine($"   ✓ Answer: {ragResult.Answer}");

        // Multi-layer reasoning
        Console.WriteLine("\n3. Neurochain reasoning: 'What is 144 / 12?'");
        var reasoningResult = await neurochain.ReasonAsync(
            "What is 144 / 12?",
            new ReasoningContext { MinConfidence = 0.9 }
        );
        Console.WriteLine($"   ✓ Answer: {reasoningResult.FinalAnswer}");

        Console.WriteLine("\n✅ Workflow complete!");
    }

    static void DisplayBestPractices()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   CONFIGURATION BEST PRACTICES                                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("📋 STORAGE BACKEND SELECTION");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("File-based:");
        Console.WriteLine("  ✓ Use for: Development, prototyping, single-user");
        Console.WriteLine("  ✓ Pros: No database needed, simple setup");
        Console.WriteLine("  ✓ Cons: Limited scalability, no concurrent access");
        Console.WriteLine();
        Console.WriteLine("Supabase:");
        Console.WriteLine("  ✓ Use for: Production, multi-user, cloud deployment");
        Console.WriteLine("  ✓ Pros: Managed, scalable, built-in auth");
        Console.WriteLine("  ✓ Cons: Requires cloud account, network dependency");
        Console.WriteLine();
        Console.WriteLine("PostgreSQL:");
        Console.WriteLine("  ✓ Use for: Enterprise, on-premise, data sovereignty");
        Console.WriteLine("  ✓ Pros: Full control, no vendor lock-in");
        Console.WriteLine("  ✓ Cons: Self-managed, requires infrastructure");
        Console.WriteLine();
        Console.WriteLine("Hybrid:");
        Console.WriteLine("  ✓ Use for: Fast local access + Cloud search");
        Console.WriteLine("  ✓ Pros: Best performance, flexible");
        Console.WriteLine("  ✓ Cons: More complex configuration");

        Console.WriteLine("\n\n📋 ENVIRONMENT VARIABLES");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  OPENAI_API_KEY=sk-...           # OpenAI API key");
        Console.WriteLine();
        Console.WriteLine("Optional (for multi-provider):");
        Console.WriteLine("  ANTHROPIC_API_KEY=sk-ant-...    # Anthropic API key");
        Console.WriteLine();
        Console.WriteLine("Optional (for Supabase):");
        Console.WriteLine("  SUPABASE_URL=https://...        # Supabase project URL");
        Console.WriteLine("  SUPABASE_ANON_KEY=eyJ...        # Public key");
        Console.WriteLine("  SUPABASE_CONNECTION_STRING=Host=... # Database connection");

        Console.WriteLine();
    }
}
