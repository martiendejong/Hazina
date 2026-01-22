using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for editing Jupyter notebook (.ipynb) cells
/// </summary>
public static class NotebookEditTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "notebook_edit",
            description: "Edit a specific cell in a Jupyter notebook (.ipynb file). Can replace, insert, or delete cells.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "notebook_path",
                    Type = "string",
                    Required = true,
                    Description = "Path to the Jupyter notebook file (must be .ipynb)"
                },
                new()
                {
                    Name = "cell_index",
                    Type = "number",
                    Required = true,
                    Description = "0-based index of the cell to edit"
                },
                new()
                {
                    Name = "new_source",
                    Type = "string",
                    Required = false,
                    Description = "New source content for the cell (required for replace/insert)"
                },
                new()
                {
                    Name = "cell_type",
                    Type = "string",
                    Required = false,
                    Description = "Cell type: 'code' or 'markdown' (default: current type or 'code' for insert)"
                },
                new()
                {
                    Name = "edit_mode",
                    Type = "string",
                    Required = false,
                    Description = "Edit mode: 'replace' (default), 'insert' (add after index), or 'delete'"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("notebook_path", out var pathElement))
                        return "Error: notebook_path parameter is required";

                    var notebookPath = pathElement.GetString();
                    if (string.IsNullOrWhiteSpace(notebookPath))
                        return "Error: notebook_path cannot be empty";

                    // Resolve path
                    if (!Path.IsPathRooted(notebookPath))
                    {
                        notebookPath = Path.GetFullPath(Path.Combine(workingDirectory, notebookPath));
                    }

                    if (!notebookPath.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase))
                        return "Error: File must be a Jupyter notebook (.ipynb)";

                    if (!File.Exists(notebookPath))
                        return $"Error: Notebook not found: {notebookPath}";

                    if (!root.TryGetProperty("cell_index", out var indexElement))
                        return "Error: cell_index parameter is required";

                    var cellIndex = indexElement.GetInt32();

                    var editMode = "replace";
                    if (root.TryGetProperty("edit_mode", out var modeElement))
                    {
                        editMode = modeElement.GetString()?.ToLower() ?? "replace";
                    }

                    string? newSource = null;
                    if (root.TryGetProperty("new_source", out var sourceElement))
                    {
                        newSource = sourceElement.GetString();
                    }

                    string? cellType = null;
                    if (root.TryGetProperty("cell_type", out var typeElement))
                    {
                        cellType = typeElement.GetString()?.ToLower();
                    }

                    // Read the notebook
                    var notebookJson = await File.ReadAllTextAsync(notebookPath, cancel);
                    var notebook = JsonNode.Parse(notebookJson);

                    if (notebook == null)
                        return "Error: Could not parse notebook JSON";

                    var cells = notebook["cells"]?.AsArray();
                    if (cells == null)
                        return "Error: Notebook has no cells array";

                    // Validate cell index
                    if (editMode != "insert" && (cellIndex < 0 || cellIndex >= cells.Count))
                        return $"Error: Cell index {cellIndex} out of range (notebook has {cells.Count} cells)";

                    if (editMode == "insert" && (cellIndex < -1 || cellIndex >= cells.Count))
                        return $"Error: Insert index {cellIndex} out of range";

                    string result;

                    switch (editMode)
                    {
                        case "delete":
                            var deletedCell = cells[cellIndex];
                            cells.RemoveAt(cellIndex);
                            result = $"Deleted cell {cellIndex}";
                            break;

                        case "insert":
                            if (string.IsNullOrEmpty(newSource))
                                return "Error: new_source is required for insert mode";

                            var insertType = cellType ?? "code";
                            var newCell = CreateCell(insertType, newSource);
                            cells.Insert(cellIndex + 1, newCell);
                            result = $"Inserted new {insertType} cell at index {cellIndex + 1}";
                            break;

                        case "replace":
                        default:
                            if (string.IsNullOrEmpty(newSource))
                                return "Error: new_source is required for replace mode";

                            var existingCell = cells[cellIndex];
                            var existingType = existingCell?["cell_type"]?.GetValue<string>() ?? "code";
                            var finalType = cellType ?? existingType;

                            // Update the cell
                            existingCell!["cell_type"] = finalType;
                            existingCell["source"] = CreateSourceArray(newSource);

                            // Clear outputs for code cells
                            if (finalType == "code")
                            {
                                existingCell["outputs"] = new JsonArray();
                                existingCell["execution_count"] = null;
                            }

                            result = $"Updated cell {cellIndex} (type: {finalType})";
                            break;
                    }

                    // Write back to file
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(notebookPath, notebook.ToJsonString(options), cancel);

                    return $"{result}\nNotebook saved: {notebookPath}\nTotal cells: {cells.Count}";
                }
                catch (Exception ex)
                {
                    return $"Error editing notebook: {ex.Message}";
                }
            }
        );
    }

    private static JsonNode CreateCell(string cellType, string source)
    {
        var cell = new JsonObject
        {
            ["cell_type"] = cellType,
            ["metadata"] = new JsonObject(),
            ["source"] = CreateSourceArray(source)
        };

        if (cellType == "code")
        {
            cell["outputs"] = new JsonArray();
            cell["execution_count"] = null;
        }

        return cell;
    }

    private static JsonArray CreateSourceArray(string source)
    {
        var lines = source.Split('\n');
        var sourceArray = new JsonArray();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Add newline to all but last line
            if (i < lines.Length - 1)
            {
                sourceArray.Add(line + "\n");
            }
            else if (!string.IsNullOrEmpty(line))
            {
                sourceArray.Add(line);
            }
        }

        return sourceArray;
    }
}
