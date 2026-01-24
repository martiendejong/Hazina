# RAG Chat API Guide

## Setup

### 1. Configure Ollama (Local LLM - No API Key Needed!)

The Zero-Code API is configured to use your local Ollama instance running at:
- **Endpoint:** `http://85.215.217.154:5555`
- **Model:** `phi3:mini`
- **Embedding Model:** `nomic-embed-text`

**No API key needed!** It uses your existing Ollama setup from client-manager.

**Configuration in appsettings.json:**
```json
{
  "Ollama": {
    "Endpoint": "http://85.215.217.154:5555",
    "Model": "phi3:mini",
    "EmbeddingModel": "nomic-embed-text",
    "Password": "Th1s1sSp4rt4!"
  }
}
```

### 2. Add Some Documents

First, create a few documents for the RAG system to search:

```bash
curl -X POST https://localhost:49238/api/Document \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Getting Started with Hazina",
    "content": "Hazina is a framework for building enterprise applications with built-in RAG capabilities. It provides dynamic API generation from YAML configuration.",
    "category": "Tutorial"
  }'

curl -X POST https://localhost:49238/api/Document \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Zero-Code API Features",
    "content": "The Zero-Code API allows you to define entities in YAML without writing any C# code. It automatically generates CRUD endpoints, search functionality, and supports embeddings for RAG.",
    "category": "Documentation"
  }'

curl -X POST https://localhost:49238/api/Document \
  -H "Content-Type: application/json" \
  -d '{
    "title": "RAG Architecture",
    "content": "Retrieval-Augmented Generation combines document search with LLM responses. When you ask a question, the system searches for relevant documents and includes them as context for the AI.",
    "category": "Technical"
  }'
```

## Using the Chat API

### Simple Chat (Single Message)

```bash
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "How does the Zero-Code API work?"
  }'
```

**Response:**
```json
{
  "answer": "Based on the documents, the Zero-Code API allows you to define entities in YAML without writing any C# code. It automatically generates CRUD endpoints, search functionality, and supports embeddings for RAG...",
  "documentsUsed": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "title": "Zero-Code API Features",
      "relevance": 0.0
    }
  ],
  "tokenUsage": {
    "promptTokens": 250,
    "completionTokens": 45,
    "totalTokens": 295
  }
}
```

### Chat with History (Multiple Messages)

```bash
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "What is Hazina?"
      },
      {
        "role": "assistant",
        "content": "Hazina is a framework for building enterprise applications with built-in RAG capabilities."
      },
      {
        "role": "user",
        "content": "Tell me more about the RAG features"
      }
    ]
  }'
```

### Control Number of Context Documents

By default, the API retrieves the top 3 most relevant documents. You can change this:

```bash
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Explain Hazina in detail",
    "topK": 5
  }'
```

## How It Works

1. **Document Ingestion**: When you create documents, they're automatically indexed
2. **Query Processing**: When you send a chat message, the system searches for relevant documents
3. **Context Building**: Top K documents are formatted as context
4. **LLM Call**: Your message + document context is sent to OpenAI
5. **Response**: You get an AI-generated answer based on your documents

## Request Body Options

### Simple Mode
```json
{
  "message": "Your question here",
  "topK": 3
}
```

### Advanced Mode (with history)
```json
{
  "messages": [
    {
      "role": "user",
      "content": "First question"
    },
    {
      "role": "assistant",
      "content": "First answer"
    },
    {
      "role": "user",
      "content": "Follow-up question"
    }
  ],
  "topK": 3
}
```

## Response Structure

```json
{
  "answer": "AI-generated response",
  "documentsUsed": [
    {
      "id": "guid-here",
      "title": "Document Title",
      "relevance": 0.0
    }
  ],
  "tokenUsage": {
    "promptTokens": 100,
    "completionTokens": 50,
    "totalTokens": 150
  }
}
```

## Testing in Swagger

1. Run the API: `dotnet run`
2. Open Swagger UI: `https://localhost:49238`
3. Find the **Chat** section
4. Click **POST /api/chat**
5. Click **Try it out**
6. Enter your message:
   ```json
   {
     "message": "What is Hazina?"
   }
   ```
7. Click **Execute**

## Tips

### For Best Results
- **Add more documents** - More context = better answers
- **Use descriptive titles** - Helps with search relevance
- **Categorize documents** - Makes it easier to find related content
- **Keep content focused** - Each document should cover one topic well

### Troubleshooting

**Error: Connection refused to Ollama**
- Check that Ollama is running at `http://85.215.217.154:5555`
- Verify the endpoint is accessible from your machine
- Check if the password is correct in appsettings.json

**No relevant documents found**
- Make sure you've created documents first
- Try broader search terms
- Check that your documents contain relevant keywords

**Slow responses**
- Normal for first request (model loading)
- Subsequent requests are faster
- Phi3:mini is already a fast, lightweight model

## Example Workflow

```bash
# 1. Create a document
curl -X POST https://localhost:49238/api/Document \
  -H "Content-Type: application/json" \
  -d '{
    "title": "API Authentication",
    "content": "The API uses JWT tokens for authentication. Include the token in the Authorization header as Bearer {token}.",
    "category": "Security"
  }'

# 2. Ask a question
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "How does authentication work?"
  }'

# 3. Follow up
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "How does authentication work?"
      },
      {
        "role": "assistant",
        "content": "The API uses JWT tokens for authentication..."
      },
      {
        "role": "user",
        "content": "Where do I put the token?"
      }
    ]
  }'
```

## Next Steps

- Explore the Swagger UI for interactive testing
- Create more documents to build your knowledge base
- Experiment with different `topK` values
- Try multi-turn conversations with message history

---

**Powered by Hazina Framework + Ollama (Local LLM)** 🚀

**Benefits of using Ollama:**
- ✅ **No API costs** - Run everything locally
- ✅ **Privacy** - Your data never leaves your server
- ✅ **Fast** - Low latency with local inference
- ✅ **Offline** - Works without internet connection
