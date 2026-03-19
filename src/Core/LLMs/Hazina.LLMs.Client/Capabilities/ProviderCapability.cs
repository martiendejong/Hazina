namespace Hazina.LLMs.Capabilities;

/// <summary>
/// Defines specific capabilities that LLM providers can support.
/// </summary>
[Flags]
public enum ProviderCapability
{
    /// <summary>
    /// No capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// Supports basic chat completions.
    /// </summary>
    Chat = 1 << 0,

    /// <summary>
    /// Supports streaming responses.
    /// </summary>
    Streaming = 1 << 1,

    /// <summary>
    /// Supports tool/function calling.
    /// </summary>
    Tools = 1 << 2,

    /// <summary>
    /// Supports vision/image input.
    /// </summary>
    Vision = 1 << 3,

    /// <summary>
    /// Supports image generation.
    /// </summary>
    ImageGeneration = 1 << 4,

    /// <summary>
    /// Supports embedding generation.
    /// </summary>
    Embeddings = 1 << 5,

    /// <summary>
    /// Supports text-to-speech.
    /// </summary>
    TextToSpeech = 1 << 6,

    /// <summary>
    /// Supports structured JSON output.
    /// </summary>
    JsonMode = 1 << 7,

    /// <summary>
    /// Supports system messages.
    /// </summary>
    SystemMessages = 1 << 8,

    /// <summary>
    /// Supports message streaming with tool calls.
    /// </summary>
    StreamingTools = 1 << 9,

    /// <summary>
    /// All capabilities.
    /// </summary>
    All = Chat | Streaming | Tools | Vision | ImageGeneration | Embeddings | TextToSpeech | JsonMode | SystemMessages | StreamingTools
}
