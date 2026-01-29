using Hazina.API.Generic.Dynamic;
using Hazina.LLMs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text;

namespace Hazina.Demo.ZeroCode.Controllers;

/// <summary>
/// RAG-powered chat endpoint that searches documents and uses them as context for LLM responses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly DynamicEntityStore _store;
    private readonly ILLMClient _llmClient;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        DynamicEntityStore store,
        ILLMClient llmClient,
        ILogger<ChatController> logger)
    {
        _store = store;
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/chat
    /// Send a chat message and get an AI response with relevant documents as context
    /// </summary>
    /// <remarks>
    /// **RAG-Powered Chat**
    ///
    /// This endpoint uses Retrieval-Augmented Generation (RAG) to provide contextual responses:
    /// 1. Searches for relevant documents based on your message
    /// 2. Includes the most relevant documents as context
    /// 3. Sends everything to OpenAI for a response
    ///
    /// **Example Request:**
    /// ```json
    /// {
    ///   "message": "How do I get started with Hazina?"
    /// }
    /// ```
    ///
    /// **Example Request (Multiple Messages):**
    /// ```json
    /// {
    ///   "messages": [
    ///     {
    ///       "role": "user",
    ///       "content": "What is Hazina?"
    ///     },
    ///     {
    ///       "role": "assistant",
    ///       "content": "Hazina is a framework..."
    ///     },
    ///     {
    ///       "role": "user",
    ///       "content": "Tell me more about the API features"
    ///     }
    ///   ]
    /// }
    /// ```
    ///
    /// **Response includes:**
    /// - AI-generated answer
    /// - List of documents used as context
    /// - Token usage statistics
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Chat with RAG",
        Description = "Send a message and get an AI response with relevant documents as context",
        Tags = new[] { "Chat" }
    )]
    [SwaggerResponse(200, "Chat response with context", typeof(ChatResponse))]
    [SwaggerResponse(400, "Invalid request")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        try
        {
            // Validate request
            if (request == null || (string.IsNullOrWhiteSpace(request.Message) && (request.Messages == null || request.Messages.Count == 0)))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = "Either 'message' or 'messages' must be provided",
                    Status = 400
                });
            }

            // Get the latest user message for RAG search
            var userMessage = !string.IsNullOrWhiteSpace(request.Message)
                ? request.Message
                : request.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "";

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = "No user message found",
                    Status = 400
                });
            }

            // Search for relevant documents (RAG)
            var relevantDocuments = await SearchRelevantDocuments(userMessage, request.TopK ?? 3);

            _logger.LogInformation("Found {Count} relevant documents for query: {Query}",
                relevantDocuments.Count, userMessage);

            // Build context from documents
            var context = BuildContext(relevantDocuments);

            // Build chat messages
            var chatMessages = BuildChatMessages(request, context);

            // Call OpenAI
            var llmResponse = await _llmClient.GetResponse(
                chatMessages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                HttpContext.RequestAborted);

            // Build response
            var response = new ChatResponse
            {
                Answer = llmResponse.Result ?? "",
                DocumentsUsed = relevantDocuments.Select(d => new DocumentReference
                {
                    Id = d.Id,
                    Title = d.Properties.TryGetValue("title", out var title) ? title?.ToString() : "Untitled",
                    Relevance = 0.0 // Placeholder - could calculate cosine similarity
                }).ToList(),
                TokenUsage = llmResponse.TokenUsage != null ? new TokenUsageInfo
                {
                    PromptTokens = llmResponse.TokenUsage.InputTokens,
                    CompletionTokens = llmResponse.TokenUsage.OutputTokens,
                    TotalTokens = llmResponse.TokenUsage.TotalTokens
                } : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while processing your request",
                Status = 500
            });
        }
    }

    private async Task<List<DynamicEntity>> SearchRelevantDocuments(string query, int topK)
    {
        try
        {
            // Use the built-in search functionality
            var results = await _store.SearchAsync("Document", query, topK);
            return results;
        }
        catch
        {
            // Fallback: return empty list if search fails
            _logger.LogWarning("Document search failed, proceeding without context");
            return new List<DynamicEntity>();
        }
    }

    private string BuildContext(List<DynamicEntity> documents)
    {
        if (documents.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Here are some relevant documents that may help answer the question:");
        sb.AppendLine();

        foreach (var doc in documents)
        {
            var title = doc.Properties.TryGetValue("title", out var t) ? t?.ToString() : "Untitled";
            var content = doc.Properties.TryGetValue("content", out var c) ? c?.ToString() : "";
            var category = doc.Properties.TryGetValue("category", out var cat) ? cat?.ToString() : null;

            sb.AppendLine($"Document: {title}");
            if (!string.IsNullOrWhiteSpace(category))
            {
                sb.AppendLine($"Category: {category}");
            }
            sb.AppendLine($"Content: {content}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<HazinaChatMessage> BuildChatMessages(ChatRequest request, string context)
    {
        var messages = new List<HazinaChatMessage>();

        // System message with context
        var systemPrompt = "You are a helpful assistant that answers questions based on the provided documents.";
        if (!string.IsNullOrWhiteSpace(context))
        {
            systemPrompt += "\n\n" + context + "\n\nUse the documents above to answer the user's question. If the documents don't contain relevant information, say so clearly.";
        }

        messages.Add(new HazinaChatMessage(HazinaMessageRole.System, systemPrompt));

        // Add conversation history or single message
        if (request.Messages != null && request.Messages.Count > 0)
        {
            foreach (var msg in request.Messages)
            {
                var role = msg.Role?.ToLower() switch
                {
                    "assistant" => HazinaMessageRole.Assistant,
                    "system" => HazinaMessageRole.System,
                    _ => HazinaMessageRole.User
                };
                messages.Add(new HazinaChatMessage(role, msg.Content ?? ""));
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Message))
        {
            messages.Add(new HazinaChatMessage(HazinaMessageRole.User, request.Message));
        }

        return messages;
    }
}

/// <summary>
/// Chat request model
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// Single message (simple mode)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Multiple messages with history (advanced mode)
    /// </summary>
    public List<ChatMessage>? Messages { get; set; }

    /// <summary>
    /// Number of relevant documents to retrieve (default: 3)
    /// </summary>
    public int? TopK { get; set; }
}

/// <summary>
/// Individual chat message
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Message role: "user", "assistant", or "system"
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public string? Content { get; set; }
}

/// <summary>
/// Chat response model
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// AI-generated answer
    /// </summary>
    public string Answer { get; set; } = "";

    /// <summary>
    /// Documents used as context
    /// </summary>
    public List<DocumentReference> DocumentsUsed { get; set; } = new();

    /// <summary>
    /// Token usage information
    /// </summary>
    public TokenUsageInfo? TokenUsage { get; set; }
}

/// <summary>
/// Reference to a document used in the response
/// </summary>
public class DocumentReference
{
    /// <summary>
    /// Document ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Document title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Relevance score (0.0 - 1.0)
    /// </summary>
    public double Relevance { get; set; }
}

/// <summary>
/// Token usage statistics
/// </summary>
public class TokenUsageInfo
{
    /// <summary>
    /// Tokens used in the prompt
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Tokens used in the completion
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens used
    /// </summary>
    public int TotalTokens { get; set; }
}
