using System.Text;

namespace Hazina.Tools.Services.Database.Core;

/// <summary>
/// Compiles StructuredQuery objects into parameterized SQL.
/// All queries use parameters to prevent SQL injection.
/// </summary>
public class SqlCompiler
{
    private readonly DatabaseSchema _schema;
    private readonly SqlDialect _dialect;

    /// <summary>
    /// Allowed comparison operators.
    /// </summary>
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", "<>", ">", ">=", "<", "<=",
        "LIKE", "NOT LIKE",
        "IN", "NOT IN",
        "IS NULL", "IS NOT NULL",
        "BETWEEN"
    };

    /// <summary>
    /// Allowed aggregate functions.
    /// </summary>
    private static readonly HashSet<string> AllowedAggregates = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "SUM", "AVG", "MIN", "MAX"
    };

    /// <summary>
    /// Creates a new SQL compiler.
    /// </summary>
    /// <param name="schema">Database schema for validation.</param>
    /// <param name="dialect">SQL dialect to use (default SQLite).</param>
    public SqlCompiler(DatabaseSchema schema, SqlDialect dialect = SqlDialect.SQLite)
    {
        _schema = schema;
        _dialect = dialect;
    }

    /// <summary>
    /// Compiles a structured query to parameterized SQL.
    /// </summary>
    public CompiledQuery Compile(StructuredQuery query, RestrictionPolicy? restriction = null, RestrictionContext? context = null)
    {
        var result = new CompiledQuery();
        var parameters = new Dictionary<string, object?>();
        var paramIndex = 0;

        // Validate table
        var table = _schema.GetTable(query.Table);
        if (table == null)
        {
            result.Errors.Add($"Table '{query.Table}' not found in schema");
            return result;
        }

        var sql = new StringBuilder();

        // Build SELECT clause
        var selectClause = BuildSelectClause(query, table, result);
        if (!result.IsValid) return result;

        sql.Append("SELECT ");
        sql.Append(selectClause);

        // Build FROM clause (with restriction subquery if applicable)
        sql.Append(" FROM ");

        if (restriction != null && restriction.AppliesToTable(query.Table) && context != null)
        {
            // Wrap in restriction subquery
            var restrictionResult = BuildRestrictionSubquery(query.Table, restriction, context, ref paramIndex, parameters);
            if (!restrictionResult.IsValid)
            {
                result.Errors.AddRange(restrictionResult.Errors);
                return result;
            }
            sql.Append(restrictionResult.SqlFragment);
        }
        else
        {
            sql.Append(QuoteIdentifier(query.Table));
        }

        // Build WHERE clause
        if (query.Where.Length > 0)
        {
            var whereClause = BuildWhereClause(query.Where, table, ref paramIndex, parameters, result);
            if (!result.IsValid) return result;
            sql.Append(" WHERE ");
            sql.Append(whereClause);
        }

        // Build GROUP BY clause
        if (query.GroupBy.Length > 0)
        {
            var groupByClause = BuildGroupByClause(query.GroupBy, table, result);
            if (!result.IsValid) return result;
            sql.Append(" GROUP BY ");
            sql.Append(groupByClause);
        }

        // Build HAVING clause
        if (query.Having.Length > 0)
        {
            var havingClause = BuildWhereClause(query.Having, table, ref paramIndex, parameters, result, isHaving: true);
            if (!result.IsValid) return result;
            sql.Append(" HAVING ");
            sql.Append(havingClause);
        }

        // Build ORDER BY clause
        if (query.OrderBy.Length > 0)
        {
            var orderByClause = BuildOrderByClause(query.OrderBy, table, result);
            if (!result.IsValid) return result;
            sql.Append(" ORDER BY ");
            sql.Append(orderByClause);
        }

        // Build LIMIT/OFFSET
        var limit = Math.Min(query.Limit, 10000); // Hard cap
        if (limit <= 0) limit = 100;
        sql.Append($" LIMIT {limit}");

        if (query.Offset > 0)
        {
            sql.Append($" OFFSET {query.Offset}");
        }

        result.Sql = sql.ToString();
        result.Parameters = parameters;
        return result;
    }

    private string BuildSelectClause(StructuredQuery query, TableSchema table, CompiledQuery result)
    {
        var columns = new List<string>();

        // Handle aggregates first
        foreach (var agg in query.Aggregates)
        {
            if (!AllowedAggregates.Contains(agg.Function))
            {
                result.Errors.Add($"Invalid aggregate function: {agg.Function}");
                continue;
            }

            var col = agg.Column == "*" ? "*" : QuoteIdentifier(agg.Column);
            if (agg.Column != "*" && !table.IsColumnAllowed(agg.Column))
            {
                result.Errors.Add($"Column '{agg.Column}' is not allowed");
                continue;
            }

            var alias = string.IsNullOrEmpty(agg.Alias) ? $"{agg.Function}_{agg.Column}" : agg.Alias;
            columns.Add($"{agg.Function.ToUpperInvariant()}({col}) AS {QuoteIdentifier(alias)}");
        }

        // Handle regular columns
        if (query.Select.Length == 0 || (query.Select.Length == 1 && query.Select[0] == "*"))
        {
            // Select all allowed columns
            columns.AddRange(table.AllowedColumnNames.Select(QuoteIdentifier));
        }
        else
        {
            foreach (var col in query.Select)
            {
                if (!table.IsColumnAllowed(col))
                {
                    result.Errors.Add($"Column '{col}' is not allowed");
                    continue;
                }
                columns.Add(QuoteIdentifier(col));
            }
        }

        if (columns.Count == 0)
        {
            result.Errors.Add("No valid columns to select");
            return "";
        }

        return string.Join(", ", columns);
    }

    private string BuildWhereClause(
        WhereClause[] clauses,
        TableSchema table,
        ref int paramIndex,
        Dictionary<string, object?> parameters,
        CompiledQuery result,
        bool isHaving = false)
    {
        var parts = new List<string>();

        foreach (var clause in clauses)
        {
            if (!table.IsColumnAllowed(clause.Column))
            {
                result.Errors.Add($"Column '{clause.Column}' is not allowed for filtering");
                continue;
            }

            if (!AllowedOperators.Contains(clause.Operator))
            {
                result.Errors.Add($"Invalid operator: {clause.Operator}");
                continue;
            }

            var op = clause.Operator.ToUpperInvariant();
            var colName = QuoteIdentifier(clause.Column);

            string condition;
            if (op == "IS NULL" || op == "IS NOT NULL")
            {
                condition = $"{colName} {op}";
            }
            else if (op == "IN" || op == "NOT IN")
            {
                if (clause.Value is not IEnumerable<object> values)
                {
                    result.Errors.Add($"IN/NOT IN requires an array value for column {clause.Column}");
                    continue;
                }

                var placeholders = new List<string>();
                foreach (var val in values)
                {
                    var paramName = $"@p{paramIndex++}";
                    placeholders.Add(paramName);
                    parameters[paramName] = val;
                }

                if (placeholders.Count == 0)
                {
                    // Empty IN clause - always false for IN, always true for NOT IN
                    condition = op == "IN" ? "1=0" : "1=1";
                }
                else
                {
                    condition = $"{colName} {op} ({string.Join(", ", placeholders)})";
                }
            }
            else if (op == "BETWEEN")
            {
                if (clause.Value is not object[] range || range.Length != 2)
                {
                    result.Errors.Add($"BETWEEN requires an array of two values for column {clause.Column}");
                    continue;
                }

                var paramName1 = $"@p{paramIndex++}";
                var paramName2 = $"@p{paramIndex++}";
                parameters[paramName1] = range[0];
                parameters[paramName2] = range[1];
                condition = $"{colName} BETWEEN {paramName1} AND {paramName2}";
            }
            else
            {
                var paramName = $"@p{paramIndex++}";
                parameters[paramName] = clause.Value;
                condition = $"{colName} {op} {paramName}";
            }

            if (parts.Count > 0)
            {
                var connector = clause.Connector.ToUpperInvariant() == "OR" ? "OR" : "AND";
                parts.Add($"{connector} {condition}");
            }
            else
            {
                parts.Add(condition);
            }
        }

        return string.Join(" ", parts);
    }

    private string BuildGroupByClause(string[] columns, TableSchema table, CompiledQuery result)
    {
        var parts = new List<string>();

        foreach (var col in columns)
        {
            if (!table.IsColumnAllowed(col))
            {
                result.Errors.Add($"Column '{col}' is not allowed for grouping");
                continue;
            }
            parts.Add(QuoteIdentifier(col));
        }

        return string.Join(", ", parts);
    }

    private string BuildOrderByClause(OrderByClause[] clauses, TableSchema table, CompiledQuery result)
    {
        var parts = new List<string>();

        foreach (var clause in clauses)
        {
            if (!table.IsColumnAllowed(clause.Column))
            {
                result.Errors.Add($"Column '{clause.Column}' is not allowed for sorting");
                continue;
            }

            var direction = clause.Descending ? "DESC" : "ASC";
            parts.Add($"{QuoteIdentifier(clause.Column)} {direction}");
        }

        return string.Join(", ", parts);
    }

    private RestrictionResult BuildRestrictionSubquery(
        string tableName,
        RestrictionPolicy restriction,
        RestrictionContext context,
        ref int paramIndex,
        Dictionary<string, object?> parameters)
    {
        var result = new RestrictionResult { IsValid = true };
        var conditions = new List<string>();

        foreach (var predicate in restriction.RequiredPredicates)
        {
            var value = context.ResolveValueSource(predicate.ValueSource);
            if (value == null)
            {
                result.Errors.Add($"Could not resolve restriction value for {predicate.Column}");
                result.IsValid = false;
                continue;
            }

            var paramName = $"@restriction_{paramIndex++}";
            parameters[paramName] = value;

            var op = predicate.Operator.ToUpperInvariant();
            conditions.Add($"{QuoteIdentifier(predicate.Column)} {op} {paramName}");
        }

        if (!result.IsValid)
            return result;

        // Build subquery
        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        result.SqlFragment = $"(SELECT * FROM {QuoteIdentifier(tableName)}{whereClause}) AS {QuoteIdentifier(tableName)}";

        return result;
    }

    private string QuoteIdentifier(string identifier)
    {
        // Validate identifier (alphanumeric, underscore only)
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"Invalid identifier: {identifier}");

        return _dialect switch
        {
            SqlDialect.SQLite => $"\"{identifier}\"",
            SqlDialect.PostgreSQL => $"\"{identifier}\"",
            SqlDialect.MySQL => $"`{identifier}`",
            SqlDialect.SqlServer => $"[{identifier}]",
            _ => $"\"{identifier}\""
        };
    }

    private static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;

        // Allow alphanumeric, underscore, and dot (for schema.table)
        foreach (var c in identifier)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                return false;
        }

        return true;
    }
}

/// <summary>
/// SQL dialect for query compilation.
/// </summary>
public enum SqlDialect
{
    SQLite,
    PostgreSQL,
    MySQL,
    SqlServer
}
