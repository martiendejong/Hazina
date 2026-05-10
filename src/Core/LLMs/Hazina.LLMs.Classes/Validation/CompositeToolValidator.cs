/// <summary>
/// Composite validator that runs multiple validators in sequence
/// </summary>
public class CompositeToolValidator : IToolValidator
{
    private readonly List<IToolValidator> _validators = new();

    public CompositeToolValidator(params IToolValidator[] validators)
    {
        _validators.AddRange(validators);
    }

    public async Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ToolValidationResult { IsValid = true };

        foreach (var validator in _validators)
        {
            var validationResult = await validator.ValidateAsync(tool, toolCall, context, cancellationToken);
            if (!validationResult.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(validationResult.Errors);
            }
            result.Warnings.AddRange(validationResult.Warnings);
        }

        return result;
    }
}

public class ParameterValidator : IToolValidator
{
    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ToolValidationResult { IsValid = true };

        foreach (var param in tool.Parameters.Where(p => p.Required))
        {
            if (!param.TryGetValue(toolCall, out string value) || string.IsNullOrEmpty(value))
            {
                result.IsValid = false;
                result.Errors.Add($"Required parameter '{param.Name}' is missing or empty");
            }
        }

        return Task.FromResult(result);
    }
}

public class ReadOnlyValidator : IToolValidator
{
    private readonly HashSet<string> _writeTools;

    public ReadOnlyValidator(params string[] writeToolNames)
    {
        _writeTools = new HashSet<string>(writeToolNames, StringComparer.OrdinalIgnoreCase);
    }

    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsReadOnly && _writeTools.Contains(tool.FunctionName))
        {
            return Task.FromResult(ToolValidationResult.Failure(
                $"Tool '{tool.FunctionName}' cannot be executed in read-only mode"));
        }

        return Task.FromResult(ToolValidationResult.Success());
    }
}
