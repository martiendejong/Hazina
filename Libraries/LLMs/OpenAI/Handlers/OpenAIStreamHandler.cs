using OpenAI.Chat;
using SharpToken;
using System.Text;

public class OpenAIStreamHandler
{
    public async Task<string> HandleStream(
    Action<string> onChunkReceived, IAsyncEnumerable<StreamingChatCompletionUpdate> stream, TokenUsageInfo tokenUsage)
    {
        var fullResponse = new StringBuilder();
        StreamingChatCompletionUpdate? lastChunk = null;
        await foreach (var chunk in stream)
        {
            if (chunk != null)
            {
                lastChunk = chunk;
                HandleChunk(chunk, onChunkReceived, fullResponse);
                if (chunk.FinishReason != null)
                    break;
            }
        }
        if (lastChunk?.Usage != null)
        {
            tokenUsage.InputTokens = lastChunk.Usage.InputTokenCount;
            tokenUsage.OutputTokens = lastChunk.Usage.OutputTokenCount;
        }
        return fullResponse.ToString();
    }

    protected void HandleChunk(StreamingChatCompletionUpdate chunk, Action<string> onChunkReceived, StringBuilder fullResponse)
    {
        var text = string.Join(" ", chunk.ContentUpdate.Select(u => u.Text).ToList());
        if (text != null)
        {
            fullResponse.Append(text);
            onChunkReceived?.Invoke(text);
        }
    }
}
