public interface ILLMClient
{
    public Task<Embedding> GenerateEmbedding(string data);
    Task<LLMResponse<HazinaGeneratedImage>> GetImage(string prompt, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel);
    Task<LLMResponse<string>> GetResponse(List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel);
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<HazinaChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new();
    Task<LLMResponse<string>> GetResponseStream(List<HazinaChatMessage> messages, Action<string> onChunkReceived, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel);
    Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<HazinaChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new();

    // Voice streaming (Text-To-Speech) — streams audio bytes as they arrive
    Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel);
}
