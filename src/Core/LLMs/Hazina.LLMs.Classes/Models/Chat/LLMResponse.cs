public class LLMResponse<T>
{
    public T Result { get; set; }
    public TokenUsageInfo TokenUsage { get; set; }

    public LLMResponse(T result, TokenUsageInfo tokenUsage)
    {
        Result = result;
        TokenUsage = tokenUsage;
    }
}
