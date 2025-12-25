public interface IDocumentGenerator
{
    Task<LLMResponse<string>> GetResponse(string query, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<string>> StreamResponse(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(string query, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images) where ResponseType : ChatResponse<ResponseType>, new();
    Task<LLMResponse<ResponseType?>> StreamResponse<ResponseType>(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images) where ResponseType : ChatResponse<ResponseType>, new();
    Task<LLMResponse<string>> UpdateStore(string query, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<string>> StreamUpdateStore(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
}
