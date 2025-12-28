using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Validation;

/// <summary>
/// Validates tool definitions and arguments
/// </summary>
public class ToolValidator
{
    private readonly ILogger? _logger;

    public ToolValidator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate a tool definition
    /// </summary>
    public ValidationResult ValidateToolDefinition(HazinaChatTool tool)
    {
        var result = new ValidationResult();

        // Validate name
        if (string.IsNullOrWhiteSpace(tool.FunctionName))
        {
            result.AddError("Tool name cannot be empty");
        }
        else if (!IsValidToolName(tool.FunctionName))
        {
            result.AddError($"Tool name '{tool.FunctionName}' is invalid. Must match pattern: ^[a-zA-Z0-9_-]+$");
        }

        // Validate description
        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            result.AddWarning("Tool description is empty");
        }
        else if (tool.Description.Length < 10)
        {
            result.AddWarning("Tool description is very short (< 10 characters)");
        }

        // Validate parameters
        if (tool.Parameters == null)
        {
            result.AddWarning("Tool has no parameters defined");
        }
        else
        {
            ValidateParameters(tool.Parameters, result);
        }

        // Validate execute function
        if (tool.Execute == null)
        {
            result.AddError("Tool execute function is null");
        }

        _logger?.LogDebug("Validated tool '{ToolName}': {IsValid}", tool.FunctionName, result.IsValid);

        return result;
    }

    /// <summary>
    /// Validate tool parameters
    /// </summary>
    private void ValidateParameters(List<ChatToolParameter> parameters, ValidationResult result)
    {
        var paramNames = new HashSet<string>();

        foreach (var param in parameters)
        {
            // Check for duplicate names
            if (paramNames.Contains(param.Name))
            {
                result.AddError($"Duplicate parameter name: {param.Name}");
            }
            else
            {
                paramNames.Add(param.Name);
            }

            // Validate parameter name
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                result.AddError("Parameter name cannot be empty");
            }
            else if (!IsValidParameterName(param.Name))
            {
                result.AddError($"Parameter name '{param.Name}' is invalid");
            }

            // Validate parameter type
            if (string.IsNullOrWhiteSpace(param.Type))
            {
                result.AddWarning($"Parameter '{param.Name}' has no type specified");
            }
            else if (!IsValidParameterType(param.Type))
            {
                result.AddWarning($"Parameter '{param.Name}' has unusual type: {param.Type}");
            }

            // Validate description
            if (string.IsNullOrWhiteSpace(param.Description))
            {
                result.AddWarning($"Parameter '{param.Name}' has no description");
            }
        }
    }

    /// <summary>
    /// Validate tool arguments against parameter definitions
    /// </summary>
    public ValidationResult ValidateArguments(
        List<ChatToolParameter> parameters,
        Dictionary<string, object> arguments)
    {
        var result = new ValidationResult();

        // Check required parameters
        var requiredParams = parameters.Where(p => p.Required).ToList();
        foreach (var param in requiredParams)
        {
            if (!arguments.ContainsKey(param.Name))
            {
                result.AddError($"Required parameter '{param.Name}' is missing");
            }
        }

        // Validate argument types
        foreach (var arg in arguments)
        {
            var param = parameters.FirstOrDefault(p => p.Name == arg.Key);
            if (param == null)
            {
                result.AddWarning($"Unknown parameter: {arg.Key}");
                continue;
            }

            if (!ValidateArgumentType(arg.Value, param.Type))
            {
                result.AddError($"Parameter '{arg.Key}' has invalid type. Expected: {param.Type}, Got: {arg.Value?.GetType().Name}");
            }
        }

        return result;
    }

    /// <summary>
    /// Validate an argument value matches the expected type
    /// </summary>
    private bool ValidateArgumentType(object? value, string? expectedType)
    {
        if (value == null)
        {
            return true; // Null is generally acceptable
        }

        if (string.IsNullOrEmpty(expectedType))
        {
            return true; // No type constraint
        }

        return expectedType.ToLowerInvariant() switch
        {
            "string" => value is string,
            "number" or "integer" or "int" => value is int or long or double or float or decimal,
            "boolean" or "bool" => value is bool,
            "array" => value is System.Collections.IEnumerable and not string,
            "object" => value is Dictionary<string, object> or JsonElement { ValueKind: JsonValueKind.Object },
            _ => true // Unknown type, don't validate
        };
    }

    /// <summary>
    /// Check if tool name is valid
    /// </summary>
    private bool IsValidToolName(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$");
    }

    /// <summary>
    /// Check if parameter name is valid
    /// </summary>
    private bool IsValidParameterName(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    /// <summary>
    /// Check if parameter type is a known type
    /// </summary>
    private bool IsValidParameterType(string type)
    {
        var knownTypes = new[] { "string", "number", "integer", "int", "boolean", "bool", "array", "object" };
        return knownTypes.Contains(type.ToLowerInvariant());
    }
}

/// <summary>
/// Result of validation
/// </summary>
public class ValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

    public void AddError(string error)
    {
        _errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        _warnings.Add(warning);
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (_errors.Any())
        {
            parts.Add($"Errors ({_errors.Count}): " + string.Join("; ", _errors));
        }

        if (_warnings.Any())
        {
            parts.Add($"Warnings ({_warnings.Count}): " + string.Join("; ", _warnings));
        }

        return parts.Any() ? string.Join(" | ", parts) : "Valid";
    }
}
