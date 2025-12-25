using OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;

using static System.Net.Mime.MediaTypeNames;

public class DocumentGenerator : IDocumentGenerator
{
    protected IDocumentStore Store { get; set; }
    protected List<IDocumentStore> ReadonlyStores { get; set; }
    //protected TypedOpenAIClient TypedApi { get; set; }
    //public SimpleOpenAIClient SimpleApi { get; set; }
    public List<DevGPTChatMessage> BaseMessages { get; set; }
    protected ILLMClient LLMClient { get; set; }
    public int MaxTokens { get; set; } = 6000;

    public EmbeddingMatcher EmbeddingMatcher = new EmbeddingMatcher();

    public async Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string message, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var response = await LLMClient.GetImage(message, DevGPTChatResponseFormat.Text, toolsContext, images, cancel);
        return response;
    }

    public DocumentGenerator(IDocumentStore store, List<DevGPTChatMessage> baseMessages, ILLMClient client, List<IDocumentStore> readonlyStores)
    {
        Store = store;
        BaseMessages = baseMessages;

        LLMClient = client;
        //TypedApi = new TypedOpenAIClient(OpenAIAPI, openAiApiKey, logger.Log);
        //SimpleApi = TypedApi;
        ReadonlyStores = readonlyStores;
    }

    public DocumentGenerator(IDocumentStore store, List<DevGPTChatMessage> baseMessages, ILLMClient client, string openAiApiKey, string logFilePath, List<IDocumentStore> readonlyStores)
    {
        Store = store;
        ReadonlyStores = readonlyStores;
        BaseMessages = baseMessages;
        LLMClient = client;
        var OpenAIAPI = new OpenAIClient(openAiApiKey);
        //var logger = new Logger(logFilePath);
        //TypedApi = new TypedOpenAIClient(OpenAIAPI, openAiApiKey, logger.Log);
        //SimpleApi = TypedApi;
    }

    public async Task<LLMResponse<string>> GetResponse(string message, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponse(sendMessages.ToList(), DevGPTChatResponseFormat.Text, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<string>> GetResponse(IEnumerable<DevGPTChatMessage> messages, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(messages.ToList(), history?.ToList(), addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponse(sendMessages, DevGPTChatResponseFormat.Text, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<string>> StreamResponse(string message, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponseStream(sendMessages, onChunkReceived, DevGPTChatResponseFormat.Text, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<string>> StreamResponse(IEnumerable<DevGPTChatMessage> messages, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(messages, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponseStream(sendMessages, onChunkReceived, DevGPTChatResponseFormat.Text, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(string message, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponse<ResponseType>(sendMessages, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(IEnumerable<DevGPTChatMessage> messages, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var sendMessages = await PrepareMessages(messages, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponse<ResponseType>(sendMessages, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<ResponseType?>> StreamResponse<ResponseType>(string message, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponseStream<ResponseType>(sendMessages, onChunkReceived, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<ResponseType?>> StreamResponse<ResponseType>(IEnumerable<DevGPTChatMessage> messages, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var sendMessages = await PrepareMessages(messages, history, addRelevantDocuments, addFilesList);
        return await LLMClient.GetResponseStream<ResponseType>(sendMessages, onChunkReceived, toolsContext, images, cancel);
    }

    public async Task<LLMResponse<string>> UpdateStore(string message, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);

        //var info = new DevGPTChatTool("writefile", "Writes content to a file and returns success or an error. Use this to modify files.", new List<ChatToolParameter>() {
        //        new ChatToolParameter { Name = "file", Description = "The relative path to the file being written", Type = "string" },
        //        new ChatToolParameter { Name = "content", Description = "The literal content that will be written to the file", Type = "string" }
        //    },
        //    async (messages, call) => {
        //        using JsonDocument argumentsJson = JsonDocument.Parse(call.FunctionArguments);
        //        var hasFile = argumentsJson.RootElement.TryGetProperty("file", out JsonElement file);
        //        if (!hasFile) return "file parameter not provided";
        //        var hasContent = argumentsJson.RootElement.TryGetProperty("content", out JsonElement content);
        //        if (!hasContent) return "content parameter not provided";
        //        try
        //        {
        //            await Store.Store(file.ToString(), content.ToString(), false);
        //            return "success";
        //        }
        //        catch (Exception ex)
        //        {
        //            return ex.Message;
        //        }
        //    }
        //);
        //toolsContext.Add(info);

        var response = await LLMClient.GetResponse<UpdateStoreResponse>(sendMessages, toolsContext, images, cancel);

        await ModifyDocuments(response.Result);

        return new LLMResponse<string>(response.Result?.ResponseMessage ?? "", response.TokenUsage);
    }

    public async Task<LLMResponse<string>> UpdateStore(IEnumerable<DevGPTChatMessage> messages, CancellationToken cancel, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(messages, history, addRelevantDocuments, addFilesList);
        var response = await LLMClient.GetResponse<UpdateStoreResponse>(sendMessages, toolsContext, images, cancel);
        await ModifyDocuments(response.Result);
        return new LLMResponse<string>(response.Result?.ResponseMessage ?? "", response.TokenUsage);
    }

    public async Task<LLMResponse<string>> StreamUpdateStore(string message, CancellationToken cancel, Action<string> onChunkReceived, IEnumerable<DevGPTChatMessage>? history = null, bool addRelevantDocuments = true, bool addFilesList = true, IToolsContext? toolsContext = null, List<ImageData>? images = null)
    {
        var sendMessages = await PrepareMessages(message, history, addRelevantDocuments, addFilesList);
        var response = await LLMClient.GetResponseStream<UpdateStoreResponse>(sendMessages, onChunkReceived, toolsContext, images, cancel);
        await ModifyDocuments(response.Result);
        return new LLMResponse<string>(response.Result?.ResponseMessage ?? "", response.TokenUsage);
    }

    private async Task ModifyDocuments(UpdateStoreResponse? response)
    {
        if (response.Modifications != null)
            foreach (var modification in response.Modifications)
            {
                await Store.Store(modification.Path, modification.Contents, null, false);
            }
        if (response.Deletions != null)
            foreach (var deletion in response.Deletions)
            {
                await Store.Remove(deletion.Path);
            }
        if (response.Moves != null)
            foreach (var move in response.Moves)
            {
                await Store.Move(move.Path, move.NewPath, false);
            }
    }


    private async Task<List<DevGPTChatMessage>> PrepareMessages(string message, IEnumerable<DevGPTChatMessage>? messages, bool addRelevantDocuments, bool addFilesList)
    {
        return await PrepareMessages([new DevGPTChatMessage { Role = DevGPTMessageRole.User, Text = message }], messages, addRelevantDocuments, addFilesList);
    }

    private async Task<List<DevGPTChatMessage>> PrepareMessages(IEnumerable<DevGPTChatMessage> chatMessages, IEnumerable<DevGPTChatMessage>? history, bool addRelevantDocuments, bool addFilesList)
    {
        var numMessages = 20;
        if(history !=  null)
            if (history.Count() > 20)
                history = history.Reverse().Take(20).Reverse();
            else
                numMessages = history.Count();
        var sendMessages = history == null ? new List<DevGPTChatMessage>() : history.Take(numMessages - 3).ToList();
        if (addRelevantDocuments)
        {
            var relevancyQuery = string.Join("\n\n", sendMessages.Concat(BaseMessages).Concat(chatMessages).Select(m => m.Role + ": " + m.Text));

            var embeddings = await Store.Embeddings(relevancyQuery);
            foreach(var s in ReadonlyStores)
            {
                embeddings.AddRange(await s.Embeddings(relevancyQuery));
            }
            var e = new EmbeddingMatcher();
            var docs = await e.TakeTop(embeddings, MaxTokens);

            var msgs = docs.Select(d => new DevGPTChatMessage { Role = DevGPTMessageRole.Assistant, Text = d });

            sendMessages.AddRange(msgs);
        }
        if (addFilesList)
        {
            var filesList = await Store.List("", true);
            var filesListString = string.Join("\n", filesList);
            sendMessages.Add(new DevGPTChatMessage { Role = DevGPTMessageRole.Assistant, Text = $"A list of all files in the document store:\n{filesListString}" });
        }
        sendMessages.AddRange(BaseMessages);
        if(history != null) 
            sendMessages.AddRange(history.Skip(numMessages - 3).Take(3).ToList());
        if (chatMessages.Any())
        {
            sendMessages.AddRange(chatMessages);
        }
        return sendMessages;
    }
}
