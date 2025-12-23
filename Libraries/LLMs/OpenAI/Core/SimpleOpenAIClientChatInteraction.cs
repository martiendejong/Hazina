using System.Runtime.CompilerServices;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using OpenAI;
using OpenAI.Chat;
using SharpToken;
using System.Text;
using System.Linq;
using OpenAI.Images;
using System.Threading;

public partial class SimpleOpenAIClientChatInteraction
{
    public OpenAIClient API { get; set; }
    public ChatClient Client { get; set; }
    public ImageClient ImageClient { get; set; }
    public ChatCompletionOptions Options { get; set; }
    public List<ChatMessage> Messages { get; set; }
    public List<ImageData>? Images { get; set; } = new List<ImageData>();

    public OpenAIClientWrapper OpenAIClientWrapper { get; set; }

    public IToolsContext? ToolsContext { get; set; }

    public string LogPath { get; set; }

    public SimpleOpenAIClientChatInteraction(IToolsContext? context, OpenAIClient api, OpenAIClientWrapper wrapper, string apiKey, string model, string logPath, ChatClient chatClient, ImageClient imageClient, List<ChatMessage> messages, List<ImageData>? images, ChatResponseFormat responseFormat, bool useWebSerach, bool useReasoning)
    {
        ToolsContext = context;
        API = api;
        OpenAIClientWrapper = wrapper;
        Client = chatClient;
        ImageClient = imageClient;
        Options = GetOptions(responseFormat, useWebSerach, useReasoning);
        Messages = messages;
        Images = images;
        LogPath = logPath;
        if (Images != null)
            Messages.AddRange(Images.Select(image =>
            {
                var contentPart = ChatMessageContentPart.CreateImagePart(image.BinaryData, image.MimeType);
                return new UserChatMessage($"Image file attached: {image.Name}", contentPart);
            }));
    }

    public void Log(string? data)
    {
        const int maxRetries = 10;
        const int delayMs = 100;
        string message = $"{DateTime.Now:yy-MM-dd HH:mm:ss}\n{data ?? ""}";

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (FileStream stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine(message);
                    return;
                }
            }
            catch (IOException)
            {
                // File is likely locked by another writer
                Thread.Sleep(delayMs);
            }
        }

        throw new IOException("Could not write to log file after multiple attempts due to it being locked.");
    }

    private ChatCompletionOptions GetOptions(ChatResponseFormat responseFormat, bool useWebSerach, bool useReasoning)
    {
        var options = new ChatCompletionOptions
        {
            ResponseFormat = responseFormat,
        };

        if (ToolsContext != null && ToolsContext.Tools != null)
        {
            foreach (var tool in ToolsContext.Tools)
            {
                options.Tools.Add(tool.OpenAI());
            }
        }

        return options;
    }

    public async Task<ChatCompletion> Run_SurrogateTools(CancellationToken cancellationToken)
    {
        bool requiresAction;
        ChatCompletion completion;
        var i = 0;
        var maxToolCalls = 50;

        var noToolsOptions = new ChatCompletionOptions
        {
            ResponseFormat = Options.ResponseFormat,
        };

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            requiresAction = false;
            try
            {
                Log(Messages?.LastOrDefault()?.Content?.FirstOrDefault()?.ToString());
                // todo add code to demand json in a format that shows the function calls
                completion = await Client.CompleteChatAsync(Messages, noToolsOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }

            var finishMessage = new AssistantChatMessage(completion);
            var toolCalls = completion.ToolCalls;
            var finishReason = completion.FinishReason;
            ++i;

            requiresAction = await HandleFinishReason(requiresAction, finishMessage, toolCalls, finishReason, cancellationToken);
        } while (requiresAction && i < maxToolCalls);

        return completion;
    }

    public async Task<ChatCompletion> Run(CancellationToken cancellationToken)
    {
        bool requiresAction;
        ChatCompletion completion;
        var i = 0;
        var maxToolCalls = 50;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            requiresAction = false;
            try
            {
                Log(Messages?.LastOrDefault()?.Content?.FirstOrDefault()?.ToString());
                completion = await Client.CompleteChatAsync(Messages, Options, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }

            var finishMessage = new AssistantChatMessage(completion);
            var toolCalls = completion.ToolCalls;
            var finishReason = completion.FinishReason;
            ++i;

            requiresAction = await HandleFinishReason(requiresAction, finishMessage, toolCalls, finishReason, cancellationToken);
        } while (requiresAction && i < maxToolCalls);

        // Track token usage
        if (ToolsContext?.OnTokensUsed != null && ToolsContext?.ProjectId != null && completion?.Usage != null)
        {
            try
            {
                ToolsContext.OnTokensUsed(
                    ToolsContext.ProjectId,
                    completion.Usage.InputTokenCount,
                    completion.Usage.OutputTokenCount,
                    "gpt-4" // Model name - could be passed through config if needed
                );
            }
            catch
            {
                // Silently fail token tracking to not disrupt main flow
            }
        }

        return completion;
    }

    public async Task<GeneratedImage> RunImage(string prompt, CancellationToken cancellationToken)
    {
        //var options = new ImageGenerationOptions
        //{
        //    Size = GeneratedImageSize.W1024xH1024,
        //    Style = GeneratedImageStyle.Natural,
        //    Quality = GeneratedImageQuality.Standard
        //};

        var response = await ImageClient.GenerateImageAsync(prompt, null, cancellationToken);
        return response;
    }


    public async IAsyncEnumerable<StreamingChatCompletionUpdate> Stream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool requiresAction;

        var i = 0;
        var maxToolCalls = 50;

        string content = "";
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            requiresAction = false;

            Log(Messages?.LastOrDefault()?.Content?.FirstOrDefault()?.ToString());
            var completionResult = Client.CompleteChatStreaming(Messages, Options, cancellationToken);

            var toolCallData = new List<ToolCallData>();
            ChatFinishReason? finishReason = null;
            var toolupdates = 0;
            // BEGIN PATCH: Fix .WithCancellation usage
            //await foreach (StreamingChatCompletionUpdate completionUpdate in completionResult.WithCancellation(cancellationToken))
            foreach (StreamingChatCompletionUpdate completionUpdate in completionResult)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // END PATCH
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    content += completionUpdate.ContentUpdate[0].Text;
                }

                if (completionUpdate.ToolCallUpdates.Count > 0)
                {
                    foreach(var toolUpdate in completionUpdate.ToolCallUpdates)
                    {
                        if (!toolCallData.Any())
                        {
                            toolCallData.Add(new ToolCallData { BinaryData = new List<BinaryData>() });
                        }

                        var id = completionUpdate.ToolCallUpdates[0].ToolCallId;
                        if (id != null)
                        {
                            if(toolCallData[toolupdates].BinaryData.Any())
                            {
                                toolCallData.Add(new ToolCallData { BinaryData = new List<BinaryData>() });
                                ++toolupdates;
                            }
                            toolCallData[toolupdates].ToolCallId += id;
                        }
                        var fn = completionUpdate.ToolCallUpdates[0].FunctionName;
                        if (fn != null)
                            toolCallData[toolupdates].FunctionName += fn;
                        var binary = completionUpdate.ToolCallUpdates[0].FunctionArgumentsUpdate;
                        if(binary != null)
                            toolCallData[toolupdates].BinaryData.Add(binary);
                    }
                }

                finishReason = completionUpdate.FinishReason;
                if (finishReason != null)
                    break;

                yield return completionUpdate;
            }
            var toolCalls = toolCallData.Select(d => ChatToolCall.CreateFunctionToolCall(d.ToolCallId, d.FunctionName, ConcatenateArguments(d.BinaryData)));

            var finishMessage = toolCalls.Any() ? new AssistantChatMessage(toolCalls) : null;

            ++i;
            requiresAction = await HandleFinishReason(false, finishMessage, toolCalls, finishReason, cancellationToken);
        } while (requiresAction && i < maxToolCalls);
    }

    private static BinaryData ConcatenateArguments(List<BinaryData> arguments)
    {
        using (var memoryStream = new MemoryStream())
        {
            foreach (var arg in arguments)
            {
                var data = arg.ToArray();
                memoryStream.Write(data, 0, data.Length);
            }
            return BinaryData.FromBytes(memoryStream.ToArray());
        }
    }

    private async Task<bool> HandleFinishReason(bool requiresAction, AssistantChatMessage? finishMessage, IEnumerable<ChatToolCall> toolCalls, ChatFinishReason? finishReason, CancellationToken cancellationToken)
    {
        Log(finishMessage?.Content?.FirstOrDefault()?.ToString());

        cancellationToken.ThrowIfCancellationRequested();
        switch (finishReason)
        {
            case ChatFinishReason.Stop:
                {
                    break;
                }

            case ChatFinishReason.ToolCalls:
                {
                    await HandleToolCalls(Messages, toolCalls, finishMessage, cancellationToken);
                    requiresAction = true;
                    break;
                }

            case ChatFinishReason.Length:
                throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

            case ChatFinishReason.ContentFilter:
                throw new NotImplementedException("Omitted content due to a content filter flag.");

            case ChatFinishReason.FunctionCall:
                throw new NotImplementedException("Deprecated in favor of tool calls.");

            default:
                throw new NotImplementedException(finishReason.ToString());
        }

        return requiresAction;
    }

    private async Task<List<ChatMessage>> HandleToolCalls(List<ChatMessage> messages, IEnumerable<ChatToolCall> toolCalls, AssistantChatMessage? toolCallsMessage, CancellationToken cancellationToken)
    {
        if (ToolsContext == null) throw new Exception("Tools Context not defined");

        var toolResults = new List<ChatMessage>() { };
        if(toolCallsMessage != null)
            toolResults.Add(toolCallsMessage);
        // Then, add a new tool message for each tool call that is resolved.
        foreach (ChatToolCall toolCall in toolCalls)
        {
            foreach(var tool in ToolsContext.Tools)
            {
                if (toolCall.FunctionName == tool.FunctionName)
                {
                    var id = Guid.NewGuid().ToString();

                    Console.WriteLine($"Calling {tool.FunctionName}");
                    Console.WriteLine($"Arguments:\n{toolCall.FunctionArguments.ToString()}");
                    if(ToolsContext?.SendMessage != null)
                    {
                        var message = $"Calling {tool.FunctionName}\n{toolCall.FunctionArguments.ToString()}";
                        ToolsContext.SendMessage(id, tool.FunctionName, message);
                    }
                    // BEGIN PATCH: Make Execute call signature match the delegate (no cancellationToken)
                    // string result = await tool.Execute(messages.DevGPT(), toolCall.DevGPT(), cancellationToken);
                    string result = await tool.Execute(messages.DevGPT(), toolCall.DevGPT(), cancellationToken);
                    if (ToolsContext?.SendMessage != null)
                    {
                        ToolsContext.SendMessage(id, tool.FunctionName, result);
                    }

                    // END PATCH
                    //if (!(tool.FunctionName.Contains("_read") || tool.FunctionName.Contains("_write") || tool.FunctionName.Contains("_list") || tool.FunctionName.Contains("_relevancy") || tool.FunctionName == "build" || tool.FunctionName == "git"))
                    //{
                    //    Console.WriteLine($"Result:\n{result}\n");
                    //    if (ToolsContext.SendMessage != null)
                    //    {
                    //        ToolsContext.SendMessage($"{result}\n");
                    //    }
                    //}
                    toolResults.Add(new ToolChatMessage(toolCall.Id, result));
                }
            }
        }
        messages.AddRange(toolResults);
        return messages;
    }
}
