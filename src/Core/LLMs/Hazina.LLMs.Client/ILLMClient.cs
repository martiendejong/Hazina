using Hazina.LLMs.Capabilities;

namespace Hazina.LLMs;

/// <summary>
/// Core interface for interacting with Large Language Model (LLM) providers.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the communication with various LLM providers (OpenAI, Anthropic, Gemini, etc.),
/// providing a unified API for chat completions, embeddings, image generation, and text-to-speech.
/// </para>
/// <para>
/// Implementations include:
/// <list type="bullet">
///   <item><description><c>OpenAIClientWrapper</c> - OpenAI GPT models</description></item>
///   <item><description><c>ClaudeClientWrapper</c> - Anthropic Claude models</description></item>
///   <item><description><c>GeminiClientWrapper</c> - Google Gemini models</description></item>
///   <item><description><c>OllamaClientWrapper</c> - Local Ollama models</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// var llm = new OpenAIClientWrapper(new OpenAIConfig(apiKey, model));
/// var messages = new List&lt;HazinaChatMessage&gt;
/// {
///     new() { Role = HazinaMessageRole.User, Text = "Hello!" }
/// };
/// var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
/// Console.WriteLine(response.Result);
/// </code>
/// </example>
public interface ILLMClient : ICapabilityProvider
{
    /// <summary>
    /// Generates a vector embedding for the given text data.
    /// </summary>
    /// <param name="data">The text to embed.</param>
    /// <returns>An <see cref="Embedding"/> containing the vector representation of the input text.</returns>
    /// <remarks>
    /// Embeddings are used for semantic search, document similarity, and RAG (Retrieval-Augmented Generation).
    /// The embedding dimension depends on the model (e.g., 1536 for OpenAI text-embedding-ada-002).
    /// </remarks>
    /// <example>
    /// <code>
    /// var embedding = await llm.GenerateEmbedding("How does authentication work?");
    /// // Use embedding.Vector for similarity calculations
    /// </code>
    /// </example>
    Task<Embedding> GenerateEmbedding(string data);

    /// <summary>
    /// Generates an image based on the given prompt.
    /// </summary>
    /// <param name="prompt">The text description of the image to generate.</param>
    /// <param name="responseFormat">The desired response format.</param>
    /// <param name="toolsContext">Optional tools context for tool calling during generation.</param>
    /// <param name="images">Optional reference images for image-to-image generation.</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="LLMResponse{T}"/> containing the generated image and token usage information.</returns>
    /// <exception cref="NotSupportedException">Thrown when the LLM provider does not support image generation.</exception>
    Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    /// <summary>
    /// Sends a chat conversation to the LLM and returns the response as a string.
    /// </summary>
    /// <param name="messages">The conversation history as a list of messages.</param>
    /// <param name="responseFormat">The desired response format (Text or JSON).</param>
    /// <param name="toolsContext">Optional tools context enabling function/tool calling.</param>
    /// <param name="images">Optional images to include in the conversation (for multimodal models).</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="LLMResponse{T}"/> containing the response text and token usage information.</returns>
    /// <example>
    /// <code>
    /// var messages = new List&lt;HazinaChatMessage&gt;
    /// {
    ///     new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant." },
    ///     new() { Role = HazinaMessageRole.User, Text = "What is the capital of France?" }
    /// };
    /// var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
    /// Console.WriteLine($"Answer: {response.Result}");
    /// Console.WriteLine($"Tokens used: {response.TokenUsage?.TotalTokens}");
    /// </code>
    /// </example>
    Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    /// <summary>
    /// Sends a chat conversation to the LLM and returns a strongly-typed response.
    /// </summary>
    /// <typeparam name="ResponseType">The type to deserialize the response into. Must inherit from <see cref="ChatResponse{T}"/>.</typeparam>
    /// <param name="messages">The conversation history as a list of messages.</param>
    /// <param name="toolsContext">Optional tools context enabling function/tool calling.</param>
    /// <param name="images">Optional images to include in the conversation (for multimodal models).</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="LLMResponse{T}"/> containing the typed response and token usage information.</returns>
    /// <remarks>
    /// This method is useful for structured outputs where you expect the LLM to respond in a specific format.
    /// The LLM will be instructed to return JSON matching the schema of <typeparamref name="ResponseType"/>.
    /// </remarks>
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new();

    /// <summary>
    /// Sends a chat conversation to the LLM with streaming response.
    /// </summary>
    /// <param name="messages">The conversation history as a list of messages.</param>
    /// <param name="onChunkReceived">Callback invoked for each text chunk received from the stream.</param>
    /// <param name="responseFormat">The desired response format.</param>
    /// <param name="toolsContext">Optional tools context enabling function/tool calling.</param>
    /// <param name="images">Optional images to include in the conversation (for multimodal models).</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="LLMResponse{T}"/> containing the complete response text and token usage information.</returns>
    /// <remarks>
    /// Use this method for real-time UI updates where you want to display text as it's generated.
    /// The <paramref name="onChunkReceived"/> callback is called for each token/chunk received.
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await llm.GetResponseStream(
    ///     messages,
    ///     chunk => Console.Write(chunk),  // Display each chunk as it arrives
    ///     HazinaChatResponseFormat.Text,
    ///     null, null,
    ///     CancellationToken.None);
    /// </code>
    /// </example>
    Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    /// <summary>
    /// Sends a chat conversation to the LLM with streaming and returns a strongly-typed response.
    /// </summary>
    /// <typeparam name="ResponseType">The type to deserialize the response into. Must inherit from <see cref="ChatResponse{T}"/>.</typeparam>
    /// <param name="messages">The conversation history as a list of messages.</param>
    /// <param name="onChunkReceived">Callback invoked for each text chunk received from the stream.</param>
    /// <param name="toolsContext">Optional tools context enabling function/tool calling.</param>
    /// <param name="images">Optional images to include in the conversation (for multimodal models).</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="LLMResponse{T}"/> containing the typed response and token usage information.</returns>
    Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new();

    /// <summary>
    /// Converts text to speech and streams the audio bytes.
    /// </summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <param name="voice">The voice identifier to use (provider-specific, e.g., "alloy", "echo", "nova" for OpenAI).</param>
    /// <param name="onAudioChunk">Callback invoked for each audio chunk received.</param>
    /// <param name="mimeType">The desired audio MIME type (e.g., "audio/mpeg", "audio/opus").</param>
    /// <param name="cancel">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when all audio has been streamed.</returns>
    /// <exception cref="NotSupportedException">Thrown when the LLM provider does not support text-to-speech.</exception>
    /// <example>
    /// <code>
    /// using var audioStream = new MemoryStream();
    /// await llm.SpeakStream(
    ///     "Hello, world!",
    ///     "nova",
    ///     chunk => audioStream.Write(chunk),
    ///     "audio/mpeg",
    ///     CancellationToken.None);
    /// // audioStream now contains the complete audio data
    /// </code>
    /// </example>
    Task SpeakStream(
        string text,
        string voice,
        Action<byte[]> onAudioChunk,
        string mimeType,
        CancellationToken cancel);
}
