using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for tracking tasks during a coding session (like Claude Code's TodoWrite)
/// </summary>
public static class TodoWriteTool
{
    private static List<TodoItem> _todos = new();

    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "todo_write",
            description: "Track tasks during your coding session. Use this to plan complex work, show progress to the user, and ensure all requirements are completed.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "todos",
                    Type = "array",
                    Required = true,
                    Description = "Array of todo items. Each item has: content (string, task description), status (string: 'pending', 'in_progress', or 'completed'), activeForm (string, present tense description shown during execution)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("todos", out var todosElement))
                        return "Error: todos parameter is required";

                    _todos.Clear();
                    foreach (var todoJson in todosElement.EnumerateArray())
                    {
                        var content = todoJson.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        var status = todoJson.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending";
                        var activeForm = todoJson.TryGetProperty("activeForm", out var a) ? a.GetString() ?? content : content;

                        _todos.Add(new TodoItem
                        {
                            Content = content,
                            Status = status,
                            ActiveForm = activeForm
                        });
                    }

                    return FormatTodoList();
                }
                catch (Exception ex)
                {
                    return $"Error updating todos: {ex.Message}";
                }
            }
        );
    }

    public static string FormatTodoList()
    {
        if (_todos.Count == 0)
            return "Todo list is empty.";

        var sb = new StringBuilder();
        sb.AppendLine("## Current Tasks");
        sb.AppendLine();

        for (int i = 0; i < _todos.Count; i++)
        {
            var todo = _todos[i];
            var icon = todo.Status switch
            {
                "completed" => "[x]",
                "in_progress" => "[>]",
                _ => "[ ]"
            };
            var statusText = todo.Status switch
            {
                "completed" => "(done)",
                "in_progress" => "(working)",
                _ => ""
            };

            sb.AppendLine($"{i + 1}. {icon} {todo.Content} {statusText}");
        }

        var completed = _todos.Count(t => t.Status == "completed");
        var inProgress = _todos.Count(t => t.Status == "in_progress");
        var pending = _todos.Count(t => t.Status == "pending");

        sb.AppendLine();
        sb.AppendLine($"Progress: {completed}/{_todos.Count} completed, {inProgress} in progress, {pending} pending");

        return sb.ToString();
    }

    public static List<TodoItem> GetTodos() => _todos;

    public class TodoItem
    {
        public string Content { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string ActiveForm { get; set; } = "";
    }
}
