namespace Hazina.CodeGeneration.Core.Pipeline;

/// <summary>
/// Result of a code generation operation
/// </summary>
public class CodeGenerationResult
{
    /// <summary>
    /// Whether the generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The generated code
    /// </summary>
    public string GeneratedCode { get; set; } = string.Empty;

    /// <summary>
    /// The confidence score of the generation (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Any errors that occurred during generation
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warnings generated during the process
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Additional metadata about the generation
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// End-to-end pipeline for code generation from natural language
/// </summary>
public interface ICodeGenerationPipeline
{
    /// <summary>
    /// Generate code from a natural language prompt
    /// </summary>
    /// <param name="prompt">The natural language prompt</param>
    /// <param name="context">Additional context for code generation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The code generation result</returns>
    Task<CodeGenerationResult> GenerateFromPromptAsync(
        string prompt,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate code and write it to a file
    /// </summary>
    /// <param name="prompt">The natural language prompt</param>
    /// <param name="outputPath">The file path to write the generated code</param>
    /// <param name="context">Additional context for code generation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The code generation result</returns>
    Task<CodeGenerationResult> GenerateAndSaveAsync(
        string prompt,
        string outputPath,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default);
}
