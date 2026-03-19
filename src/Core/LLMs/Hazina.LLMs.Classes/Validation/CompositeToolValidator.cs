namespace Hazina.LLMs.Classes.Validation;

/// <summary>
/// Composite validator that runs multiple validators in sequence
/// </summary>
public class CompositeToolValidator : IToolValidator
{
    private readonly List<IToolValidator> _validators = new();

    public CompositeToolValidator()
    {
    }

    public CompositeToolValidator(params IToolValidator[] validators)
    {
        _validators.AddRange(validators);
    }

    public void AddValidator(IToolValidator validator)
    {
        if (validator == null)
            throw new ArgumentNullException(nameof(validator));

        _validators.Add(validator);
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

            foreach (var kvp in validationResult.Metadata)
            {
                result.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }
}

/// <summary>
/// Validator for parameter requirements
/// </summary>
public class ParameterValidator : IToolValidator
{
    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ToolValidationResult { IsValid = true };

        // Check required parameters
        foreach (var param in tool.Parameters.Where(p => p.Required))
        {
            if (!param.TryGetValue(toolCall, out string value) || string.IsNullOrEmpty(value))
            {
                result.AddError($"Required parameter '{param.Name}' is missing or empty");
            }
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// Validator for permission checks
/// </summary>
public class PermissionValidator : IToolValidator
{
    private readonly Dictionary<string, ToolPermissionLevel> _toolPermissions = new();

    public void SetToolPermission(string toolName, ToolPermissionLevel requiredLevel)
    {
        _toolPermissions[toolName] = requiredLevel;
    }

    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (_toolPermissions.TryGetValue(tool.FunctionName, out var requiredLevel))
        {
            if (context.PermissionLevel < requiredLevel)
            {
                return Task.FromResult(ToolValidationResult.Failure(
                    $"Tool '{tool.FunctionName}' requires {requiredLevel} permission level, but caller has {context.PermissionLevel}"));
            }
        }

        return Task.FromResult(ToolValidationResult.Success());
    }
}

/// <summary>
/// Validator for timeout constraints
/// </summary>
public class TimeoutValidator : IToolValidator
{
    private readonly TimeSpan _maxTimeout;

    public TimeoutValidator(TimeSpan maxTimeout)
    {
        _maxTimeout = maxTimeout;
    }

    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Timeout.HasValue && context.Timeout.Value > _maxTimeout)
        {
            return Task.FromResult(ToolValidationResult.Failure(
                $"Requested timeout {context.Timeout.Value.TotalSeconds}s exceeds maximum allowed {_maxTimeout.TotalSeconds}s"));
        }

        return Task.FromResult(ToolValidationResult.Success());
    }
}

/// <summary>
/// Validator for read-only mode
/// </summary>
public class ReadOnlyValidator : IToolValidator
{
    private readonly HashSet<string> _writeTools = new(StringComparer.OrdinalIgnoreCase);

    public ReadOnlyValidator(params string[] writeToolNames)
    {
        foreach (var name in writeToolNames)
        {
            _writeTools.Add(name);
        }
    }

    public void RegisterWriteTool(string toolName)
    {
        _writeTools.Add(toolName);
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

/// <summary>
/// Validator for rate limiting
/// </summary>
public class RateLimitValidator : IToolValidator
{
    private readonly Dictionary<string, RateLimitState> _states = new();
    private readonly int _maxCallsPerMinute;
    private readonly object _lock = new();

    public RateLimitValidator(int maxCallsPerMinute)
    {
        _maxCallsPerMinute = maxCallsPerMinute;
    }

    public Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var key = $"{context.UserId ?? "anonymous"}:{tool.FunctionName}";

        lock (_lock)
        {
            if (!_states.TryGetValue(key, out var state))
            {
                state = new RateLimitState();
                _states[key] = state;
            }

            var now = DateTime.UtcNow;
            state.Calls.RemoveAll(c => (now - c).TotalMinutes > 1);

            if (state.Calls.Count >= _maxCallsPerMinute)
            {
                return Task.FromResult(ToolValidationResult.Failure(
                    $"Rate limit exceeded for tool '{tool.FunctionName}': {_maxCallsPerMinute} calls per minute"));
            }

            state.Calls.Add(now);
        }

        return Task.FromResult(ToolValidationResult.Success());
    }

    private class RateLimitState
    {
        public List<DateTime> Calls { get; set; } = new();
    }
}
