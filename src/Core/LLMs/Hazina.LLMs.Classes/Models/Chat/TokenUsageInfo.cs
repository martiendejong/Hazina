public class TokenUsageInfo
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public decimal InputCost { get; set; }
    public decimal OutputCost { get; set; }
    public decimal TotalCost => InputCost + OutputCost;
    public string ModelName { get; set; } = string.Empty;

    public TokenUsageInfo()
    {
    }

    public TokenUsageInfo(int inputTokens, int outputTokens, decimal inputCost, decimal outputCost, string modelName = "")
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        InputCost = inputCost;
        OutputCost = outputCost;
        ModelName = modelName;
    }

    public static TokenUsageInfo operator +(TokenUsageInfo a, TokenUsageInfo b)
    {
        return new TokenUsageInfo
        {
            InputTokens = a.InputTokens + b.InputTokens,
            OutputTokens = a.OutputTokens + b.OutputTokens,
            InputCost = a.InputCost + b.InputCost,
            OutputCost = a.OutputCost + b.OutputCost,
            ModelName = string.IsNullOrEmpty(a.ModelName) ? b.ModelName : a.ModelName
        };
    }
}
