using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for running bash commands in the background
/// </summary>
public static class BackgroundBashTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "bash_background",
            description: "Run a command in the background. Returns immediately with a task ID. Use task_output to check progress.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "command",
                    Type = "string",
                    Required = true,
                    Description = "The command to execute in the background"
                },
                new()
                {
                    Name = "description",
                    Type = "string",
                    Required = false,
                    Description = "Short description of what this task does"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("command", out var commandElement))
                        return "Error: command parameter is required";

                    var command = commandElement.GetString();
                    if (string.IsNullOrWhiteSpace(command))
                        return "Error: command cannot be empty";

                    var description = "Background task";
                    if (root.TryGetProperty("description", out var descElement))
                    {
                        description = descElement.GetString() ?? description;
                    }

                    var taskId = BackgroundTaskManager.StartTask(command, workingDirectory, description);

                    return $"Background task started.\nTask ID: {taskId}\nCommand: {command}\n\nUse task_output with this ID to check progress or get results.";
                }
                catch (Exception ex)
                {
                    return $"Error starting background task: {ex.Message}";
                }
            }
        );
    }
}
