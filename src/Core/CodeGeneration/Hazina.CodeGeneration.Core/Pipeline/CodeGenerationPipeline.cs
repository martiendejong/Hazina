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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeGenerationResult>> GenerateAndSaveManyAsync(
        IEnumerable<(string Prompt, string OutputPath)> requests,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests?.ToList()
            ?? throw new ArgumentNullException(nameof(requests));

        _logger.LogInformation(
            "[CODE GENERATION PIPELINE] Generating {Count} file(s) atomically.",
            requestList.Count);

        // Phase 1: Generate all code in memory (no disk writes yet).
        var results = new List<CodeGenerationResult>(requestList.Count);
        foreach (var (prompt, outputPath) in requestList)
        {
            var result = await GenerateFromPromptAsync(prompt, context, cancellationToken);
            result.Metadata["output_path"] = outputPath;
            results.Add(result);
        }

        // If any generation failed, return without touching disk.
        var failed = results.Where(r => !r.Success).ToList();
        if (failed.Count > 0)
        {
            _logger.LogWarning(
                "[CODE GENERATION PIPELINE] {Count} generation(s) failed — aborting multi-file write.",
                failed.Count);
            return results.AsReadOnly();
        }

        // Phase 2: Atomically write all files using the temp-rename pattern.
        //   For each target: back up original (if exists) → write to .tmp → on success rename; on failure restore.
        var staged = new List<(string TargetPath, string TmpPath, string? BackupPath)>();

        try
        {
            for (int i = 0; i < requestList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = requestList[i].OutputPath;
                var content = results[i].GeneratedCode;

                var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Backup existing file so we can restore it on failure.
                string? backupPath = null;
                if (File.Exists(targetPath))
                {
                    backupPath = targetPath + $".{Guid.NewGuid():N}.bak";
                    File.Copy(targetPath, backupPath, overwrite: false);
                }

                // Write to a temp file first.
                var tmpPath = targetPath + $".{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(tmpPath, content, cancellationToken);

                staged.Add((targetPath, tmpPath, backupPath));
                _logger.LogDebug(
                    "[CODE GENERATION PIPELINE] Staged '{Tmp}' for target '{Target}'", tmpPath, targetPath);
            }

            // Phase 3: Commit — rename all temps to targets.
            foreach (var (targetPath, tmpPath, _) in staged)
            {
                File.Move(tmpPath, targetPath, overwrite: true);
                _logger.LogDebug("[CODE GENERATION PIPELINE] Committed '{Target}'", targetPath);
            }

            // Phase 4: Clean up backups.
            foreach (var (_, _, backupPath) in staged)
            {
                if (backupPath is not null && File.Exists(backupPath))
                    TryDelete(backupPath);
            }

            // Mark all results as written.
            foreach (var result in results)
                result.Metadata["file_written"] = "true";

            _logger.LogInformation(
                "[CODE GENERATION PIPELINE] Successfully committed {Count} file(s).",
                results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CODE GENERATION PIPELINE] Multi-file write failed — rolling back {Count} staged file(s).",
                staged.Count);

            // Rollback: delete temps and restore backups.
            foreach (var (targetPath, tmpPath, backupPath) in staged)
            {
                TryDelete(tmpPath);

                if (backupPath is not null && File.Exists(backupPath))
                {
                    try { File.Move(backupPath, targetPath, overwrite: true); }
                    catch (Exception restoreEx)
                    {
                        _logger.LogError(restoreEx,
                            "[CODE GENERATION PIPELINE] CRITICAL: Could not restore '{Target}' from '{Backup}'.",
                            targetPath, backupPath);
                    }
                }
            }

            // Mark all as failed.
            foreach (var result in results)
            {
                result.Success = false;
                result.Errors.Add($"Atomic write failed and was rolled back: {ex.Message}");
            }
        }

        return results.AsReadOnly();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CODE GENERATION PIPELINE] Could not delete temp/backup file '{Path}'.", path);
        }
    }
}
