Claude (Anthropic) LLM Client

Summary
- Provides a basic `ILLMClient` implementation backed by Anthropic Claude via the Messages API.
- Supports text responses (non-streaming, with simple chunked streaming emulation).
- Embeddings and image generation are not supported in this client.

Usage
- Create a config and client:

  var cfg = new DevGPT.Anthropic.AnthropicConfig {
      ApiKey = "YOUR_ANTHROPIC_KEY",
      Model = "claude-3-5-sonnet-latest"
  };
  var llm = new ClaudeClientWrapper(cfg);

- Plug into your pipelines similar to `OpenAIClientWrapper`/`HuggingFaceClientWrapper`.

Notes
- For typed JSON responses, the client injects a system formatting instruction like the OpenAI wrapper. Ensure your prompts fit within model limits.
- Anthropic SSE streaming can be added in a future iteration if needed.
