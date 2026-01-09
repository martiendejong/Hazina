/// <summary>
/// Contains token usage and cost information for an LLM API call.
/// </summary>
/// <remarks>
/// <para>
/// This class tracks both the number of tokens used and the associated costs.
/// Costs are calculated based on the model's pricing at the time of the call.
/// </para>
/// <para>
/// Multiple <see cref="TokenUsageInfo"/> instances can be aggregated using the + operator
/// to track cumulative usage across a conversation or session.
/// </para>
/// </remarks>
/// <example>
/// Track cumulative costs across multiple calls:
/// <code>
/// var totalUsage = new TokenUsageInfo();
///
/// var response1 = await llm.GetResponse(messages1, ...);
/// totalUsage += response1.TokenUsage;
///
/// var response2 = await llm.GetResponse(messages2, ...);
/// totalUsage += response2.TokenUsage;
///
/// Console.WriteLine($"Total cost: ${totalUsage.TotalCost:F4}");
/// Console.WriteLine($"Total tokens: {totalUsage.TotalTokens}");
/// </code>
/// </example>
public class TokenUsageInfo
{
    /// <summary>
    /// Gets or sets the number of tokens in the input/prompt.
    /// </summary>
    /// <remarks>
    /// Input tokens include the system prompt, conversation history, and user message.
    /// </remarks>
    public int InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens generated in the response.
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Gets the total number of tokens (input + output).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Gets or sets the cost in USD for the input tokens.
    /// </summary>
    /// <remarks>
    /// Calculated based on the model's per-token pricing for input.
    /// </remarks>
    public decimal InputCost { get; set; }

    /// <summary>
    /// Gets or sets the cost in USD for the output tokens.
    /// </summary>
    /// <remarks>
    /// Calculated based on the model's per-token pricing for output.
    /// Output tokens are typically more expensive than input tokens.
    /// </remarks>
    public decimal OutputCost { get; set; }

    /// <summary>
    /// Gets the total cost in USD for this API call.
    /// </summary>
    public decimal TotalCost => InputCost + OutputCost;

    /// <summary>
    /// Gets or sets the name/ID of the model used for this call.
    /// </summary>
    /// <remarks>
    /// Examples: "gpt-4.1", "claude-3-5-sonnet-latest", "gemini-1.5-pro"
    /// </remarks>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new empty token usage info.
    /// </summary>
    public TokenUsageInfo()
    {
    }

    /// <summary>
    /// Creates a new token usage info with the specified values.
    /// </summary>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <param name="inputCost">Cost of input tokens in USD.</param>
    /// <param name="outputCost">Cost of output tokens in USD.</param>
    /// <param name="modelName">Name of the model used.</param>
    public TokenUsageInfo(int inputTokens, int outputTokens, decimal inputCost, decimal outputCost, string modelName = "")
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        InputCost = inputCost;
        OutputCost = outputCost;
        ModelName = modelName;
    }

    /// <summary>
    /// Aggregates two token usage info instances.
    /// </summary>
    /// <param name="a">First token usage info.</param>
    /// <param name="b">Second token usage info.</param>
    /// <returns>A new instance with summed tokens and costs.</returns>
    /// <remarks>
    /// The model name is taken from the first non-empty value.
    /// </remarks>
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
