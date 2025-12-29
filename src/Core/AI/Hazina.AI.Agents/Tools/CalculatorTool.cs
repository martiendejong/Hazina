using System.Data;

namespace Hazina.AI.Agents.Tools;

/// <summary>
/// Calculator tool for mathematical operations
/// </summary>
public class CalculatorTool : AgentTool
{
    public CalculatorTool()
    {
        Name = "Calculator";
        Description = "Performs mathematical calculations";
        Parameters = new Dictionary<string, ToolParameter>
        {
            ["expression"] = new ToolParameter
            {
                Type = "string",
                Description = "Mathematical expression to evaluate (e.g., '2 + 2', '10 * 5 - 3')",
                Required = true
            }
        };
    }

    public override Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateArguments(arguments, out var error))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Error = error
            });
        }

        try
        {
            var expression = arguments["expression"].ToString() ?? "";
            var result = EvaluateExpression(expression);

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Output = result.ToString()
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Error = $"Calculation error: {ex.Message}"
            });
        }
    }

    private double EvaluateExpression(string expression)
    {
        // Simple expression evaluation using DataTable.Compute
        var table = new DataTable();
        var result = table.Compute(expression, null);
        return Convert.ToDouble(result);
    }
}
