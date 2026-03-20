using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.Store.EmbeddingStore;
using Hazina.LLMs;

namespace Hazina.Examples.BasicRag;

/// <summary>
/// Demonstrates basic RAG (Retrieval-Augmented Generation) with Hazina.
///
/// This example shows:
/// - Setting up a RAG engine with in-memory storage
/// - Indexing documents with metadata
/// - Querying with automatic context retrieval
/// - Examining retrieved documents and similarity scores
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - OpenAI API key set in OPENAI_API_KEY environment variable
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Basic RAG Example ===\n");

        // 1. Setup: Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");
            Console.WriteLine("\nTo set it:");
            Console.WriteLine("  Windows: set OPENAI_API_KEY=sk-your-key-here");
            Console.WriteLine("  Linux/Mac: export OPENAI_API_KEY=sk-your-key-here");
            return;
        }

        try
        {
            // 2. Create AI client
            var ai = QuickSetup.SetupOpenAI(apiKey);
            Console.WriteLine("✓ AI client initialized (OpenAI GPT-4)\n");

            // 3. Create in-memory vector store (for development)
            // For production, use PgVectorStore with PostgreSQL
            var vectorStore = new InMemoryVectorStore();

            // 4. Create RAG engine
            var rag = new RAGEngine(ai, vectorStore);
            Console.WriteLine("✓ RAG engine initialized\n");

            // 5. Create sample documents about Hazina
            var documents = new List<Document>
            {
                new()
                {
                    Id = "overview",
                    Content = "Hazina is a production-ready AI framework for .NET that provides multi-provider " +
                              "orchestration, RAG capabilities, and agent workflows out of the box. It allows you to " +
                              "switch between OpenAI, Anthropic, Google, and local models without changing your code.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = "overview.md",
                        ["category"] = "getting-started",
                        ["created"] = DateTime.UtcNow
                    }
                },
                new()
                {
                    Id = "architecture",
                    Content = "Hazina follows a modular architecture with 5 layers: Core LLMs (provider abstraction), " +
                              "Storage & Agents (data persistence and orchestration), AI Capabilities (RAG, Memory, Routing), " +
                              "Tools (domain integrations), and Applications (complete solutions). This layered design allows " +
                              "you to use only what you need and swap implementations easily.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = "architecture.md",
                        ["category"] = "architecture",
                        ["created"] = DateTime.UtcNow
                    }
                },
                new()
                {
                    Id = "rag-features",
                    Content = "Hazina's RAG implementation includes automatic chunking, metadata-first search (query without " +
                              "embeddings), similarity scoring, and configurable retrieval (TopK, similarity threshold). " +
                              "It supports both in-memory storage for development and PostgreSQL with pgvector for production.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = "rag-guide.md",
                        ["category"] = "rag",
                        ["created"] = DateTime.UtcNow
                    }
                }
            };

            // 6. Index documents
            Console.WriteLine("Indexing documents...");
            var indexResult = await rag.IndexDocumentsAsync(documents);
            Console.WriteLine($"✓ Indexed {indexResult.IndexedDocuments}/{indexResult.TotalDocuments} documents\n");

            // 7. Ask a question
            var question = "What is Hazina?";
            Console.WriteLine($"Question: {question}\n");

            var response = await rag.QueryAsync(question, new RAGQueryOptions
            {
                TopK = 3,                    // Retrieve top 3 most relevant documents
                MinSimilarity = 0.5,         // Only use documents with ≥50% similarity
                RequireCitation = true       // AI should cite sources
            });

            // 8. Display answer
            Console.WriteLine($"Answer: {response.Answer}\n");

            // 9. Show retrieved documents
            Console.WriteLine($"Retrieved {response.RetrievedDocuments.Count} documents:");
            foreach (var doc in response.RetrievedDocuments)
            {
                var source = doc.Metadata.ContainsKey("source") ? doc.Metadata["source"] : "unknown";
                Console.WriteLine($"  [{doc.Similarity:P0}] {source}");
            }

            // 10. Show token usage and cost
            if (response.TokenUsage != null)
            {
                Console.WriteLine($"\nTokens used: {response.TokenUsage.TotalTokens} " +
                    $"(input: {response.TokenUsage.InputTokens}, output: {response.TokenUsage.OutputTokens})");

                // Approximate cost (GPT-4 pricing: $0.03/1K input tokens, $0.06/1K output tokens)
                var estimatedCost = (response.TokenUsage.InputTokens * 0.03m / 1000m) +
                                    (response.TokenUsage.OutputTokens * 0.06m / 1000m);
                Console.WriteLine($"Estimated cost: ${estimatedCost:F4}");
            }

            Console.WriteLine("\n✓ Success! Your RAG system is working.");

            // 11. Interactive Q&A (optional)
            Console.WriteLine("\n--- Interactive Mode ---");
            Console.WriteLine("Ask questions about the indexed documents (or 'quit' to exit)\n");

            while (true)
            {
                Console.Write("Question: ");
                var userQuestion = Console.ReadLine();

                if (string.IsNullOrEmpty(userQuestion) || userQuestion.ToLower() == "quit")
                    break;

                var interactiveResponse = await rag.QueryAsync(userQuestion);

                Console.WriteLine($"\nAnswer: {interactiveResponse.Answer}");
                Console.WriteLine($"Sources: {interactiveResponse.RetrievedDocuments.Count} documents");

                if (interactiveResponse.TokenUsage != null)
                {
                    var cost = (interactiveResponse.TokenUsage.InputTokens * 0.03m / 1000m) +
                               (interactiveResponse.TokenUsage.OutputTokens * 0.06m / 1000m);
                    Console.WriteLine($"Cost: ${cost:F4}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("\nThank you for using Hazina!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine("\nCommon issues:");
            Console.WriteLine("  - Invalid API key");
            Console.WriteLine("  - Network connection problems");
            Console.WriteLine("  - Rate limit exceeded");
            Console.WriteLine("  - Insufficient API credits");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}
