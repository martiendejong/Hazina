/// <summary>
/// Wrapper class for LLM responses that includes the result and token usage information.
/// </summary>
/// <typeparam name="T">The type of the response result.</typeparam>
/// <remarks>
/// <para>
/// All LLM operations return this wrapper to provide transparency into token usage and costs.
/// This enables cost tracking, budget management, and performance optimization.
/// </para>
/// <para>
/// Use the <see cref="TokenUsage"/> property to track API costs and optimize prompts.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancel);
///
/// Console.WriteLine($"Response: {response.Result}");
/// Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
/// Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
/// Console.WriteLine($"Model: {response.TokenUsage.ModelName}");
/// </code>
/// </example>
public class LLMResponse<T>
{
    /// <summary>
    /// Gets or sets the actual response result from the LLM.
    /// </summary>
    /// <remarks>
    /// The type depends on the method called:
    /// <list type="bullet">
    ///   <item><description><c>string</c> for text responses</description></item>
    ///   <item><description>Custom types for structured responses</description></item>
    ///   <item><description><see cref="HazinaGeneratedImage"/> for image generation</description></item>
    /// </list>
    /// </remarks>
    public T Result { get; set; }

    /// <summary>
    /// Gets or sets the token usage and cost information for this request.
    /// </summary>
    /// <remarks>
    /// Use this to track API costs and optimize your prompts. Token counts and costs
    /// are calculated based on the model's pricing.
    /// </remarks>
    public TokenUsageInfo TokenUsage { get; set; }

    /// <summary>
    /// Creates a new LLM response with the specified result and token usage.
    /// </summary>
    /// <param name="result">The response result.</param>
    /// <param name="tokenUsage">The token usage information.</param>
    public LLMResponse(T result, TokenUsageInfo tokenUsage)
    {
        Result = result;
        TokenUsage = tokenUsage;
    }
}
