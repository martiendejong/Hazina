namespace Hazina.Tools.Services.Database.Core;

/// <summary>
/// Defines the schema for a table, including allowed columns and security settings.
/// </summary>
public class TableSchema
{
    /// <summary>
    /// The table name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description for AI agents to understand the table's purpose.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Columns that agents can SELECT. If empty, all columns are allowed.
    /// </summary>
    public List<ColumnSchema> Columns { get; set; } = new();

    /// <summary>
    /// Column names that are forbidden (never exposed to agents).
    /// </summary>
    public HashSet<string> ForbiddenColumns { get; set; } = new();

    /// <summary>
    /// Whether this table supports full-text search.
    /// </summary>
    public bool SupportsFullTextSearch { get; set; } = false;

    /// <summary>
    /// Column to use for full-text search if supported.
    /// </summary>
    public string? FullTextSearchColumn { get; set; }

    /// <summary>
    /// Gets all allowed column names.
    /// </summary>
    public IEnumerable<string> AllowedColumnNames =>
        Columns.Select(c => c.Name).Except(ForbiddenColumns);

    /// <summary>
    /// Checks if a column is allowed for selection.
    /// </summary>
    public bool IsColumnAllowed(string columnName)
    {
        if (ForbiddenColumns.Contains(columnName))
            return false;

        // If no columns defined, allow all except forbidden
        if (Columns.Count == 0)
            return true;

        return Columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Defines a column's schema.
/// </summary>
public class ColumnSchema
{
    /// <summary>
    /// Column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data type: TEXT, INTEGER, REAL, BLOB, DATETIME
    /// </summary>
    public string DataType { get; set; } = "TEXT";

    /// <summary>
    /// Description for AI agents.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this column can be filtered on.
    /// </summary>
    public bool Filterable { get; set; } = true;

    /// <summary>
    /// Whether this column can be sorted.
    /// </summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// Whether this column can be aggregated.
    /// </summary>
    public bool Aggregatable { get; set; } = true;
}

/// <summary>
/// Defines the schema for a database.
/// </summary>
public class DatabaseSchema
{
    /// <summary>
    /// Database identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description for AI agents.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Table schemas in this database.
    /// </summary>
    public Dictionary<string, TableSchema> Tables { get; set; } = new();

    /// <summary>
    /// Gets a table schema by name.
    /// </summary>
    public TableSchema? GetTable(string name)
    {
        return Tables.TryGetValue(name, out var table) ? table : null;
    }

    /// <summary>
    /// Adds a table schema.
    /// </summary>
    public DatabaseSchema AddTable(TableSchema table)
    {
        Tables[table.Name] = table;
        return this;
    }
}
