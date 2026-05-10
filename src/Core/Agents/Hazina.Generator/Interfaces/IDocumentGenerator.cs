using Hazina.LLMs;

public interface IDocumentGenerator
{
    Task<LLMResponse<string>> GetResponse(string query, CancellationToken cancel, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<string>> StreamResponse(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(string query, CancellationToken cancel, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images) where ResponseType : ChatResponse<ResponseType>, new();
    Task<LLMResponse<ResponseType?>> StreamResponse<ResponseType>(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images) where ResponseType : ChatResponse<ResponseType>, new();
    Task<LLMResponse<string>> UpdateStore(string query, CancellationToken cancel, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);
    Task<LLMResponse<string>> StreamUpdateStore(string query, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<HazinaChatMessage>? messages, bool addRelevantDocuments, bool addFilesList, IToolsContext toolsContext, List<ImageData> images);

    /// <summary>
    /// Returns a preview of what UpdateStore WOULD do, without applying any changes.
    /// Use this to show the user a patch summary before committing destructive operations.
    /// </summary>
    Task<LLMResponse<UpdateStorePatchPreview>> DryRunUpdateStore(string query, CancellationToken cancel, IEnumerable<HazinaChatMessage>? messages = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null);
}
