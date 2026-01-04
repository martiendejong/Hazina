using Hazina.CodeGeneration.Core.Parsing;
using Hazina.CodeGeneration.Core.Templates;
using Microsoft.Extensions.Logging;

namespace Hazina.CodeGeneration.Core.Pipeline;

/// <summary>
/// End-to-end code generation pipeline
/// </summary>
public class CodeGenerationPipeline : ICodeGenerationPipeline
{
    private readonly IIntentParser _intentParser;
    private readonly ITemplateEngine _templateEngine;
    private readonly ILogger<CodeGenerationPipeline> _logger;

    public CodeGenerationPipeline(
        IIntentParser intentParser,
        ITemplateEngine templateEngine,
        ILogger<CodeGenerationPipeline> logger)
    {
        _intentParser = intentParser;
        _templateEngine = templateEngine;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CodeGenerationResult> GenerateFromPromptAsync(
        string prompt,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CodeGenerationResult();

        try
        {
            _logger.LogInformation("[CODE GENERATION PIPELINE] Starting code generation for prompt: {Prompt}", prompt);

            // Step 1: Parse the intent
            var intent = await _intentParser.ParseAsync(prompt, context, cancellationToken);

            _logger.LogInformation("[CODE GENERATION PIPELINE] Intent parsed. Type: {IntentType}, Confidence: {Confidence}",
                intent.Type, intent.Confidence);

            result.Confidence = intent.Confidence;
            result.Metadata["intent_type"] = intent.Type.ToString();
            result.Metadata["prompt"] = prompt;

            // Step 2: Validate the intent
            if (!_intentParser.ValidateIntent(intent))
            {
                result.Success = false;
                result.Errors.Add("Intent validation failed. The parsed intent is missing required information.");
                result.Warnings.Add($"Intent confidence: {intent.Confidence:P}");
                _logger.LogWarning("[CODE GENERATION PIPELINE] Intent validation failed");
                return result;
            }

            // Step 3: Generate code from intent
            var generatedCode = await _templateEngine.GenerateCodeAsync(intent, cancellationToken);

            _logger.LogInformation("[CODE GENERATION PIPELINE] Code generated successfully. Length: {Length} characters",
                generatedCode.Length);

            result.Success = true;
            result.GeneratedCode = generatedCode;
            result.Metadata["code_length"] = generatedCode.Length.ToString();

            // Add warnings for low confidence
            if (intent.Confidence < 0.7)
            {
                result.Warnings.Add($"Low confidence intent parsing ({intent.Confidence:P}). Please review the generated code carefully.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CODE GENERATION PIPELINE] Code generation failed");

            result.Success = false;
            result.Errors.Add($"Code generation failed: {ex.Message}");

            return result;
        }
    }

    /// <inheritdoc/>
    public async Task<CodeGenerationResult> GenerateAndSaveAsync(
        string prompt,
        string outputPath,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CODE GENERATION PIPELINE] Generating and saving code to: {OutputPath}", outputPath);

        // Generate code
        var result = await GenerateFromPromptAsync(prompt, context, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("[CODE GENERATION PIPELINE] Code generation failed. Skipping file write.");
            return result;
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("[CODE GENERATION PIPELINE] Created directory: {Directory}", directory);
            }

            // Write code to file
            await File.WriteAllTextAsync(outputPath, result.GeneratedCode, cancellationToken);

            _logger.LogInformation("[CODE GENERATION PIPELINE] Code written to file: {OutputPath}", outputPath);

            result.Metadata["output_path"] = outputPath;
            result.Metadata["file_written"] = "true";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CODE GENERATION PIPELINE] Failed to write code to file: {OutputPath}", outputPath);

            result.Success = false;
            result.Errors.Add($"Failed to write code to file: {ex.Message}");

            return result;
        }
    }
}
