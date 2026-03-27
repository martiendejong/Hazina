namespace Hazina.Tools.Services.BigQuery
{
    public class BigQueryTableDef
    {
        public List<BigQueryFieldDef> Fields { get; set; } = new();
        public string Name { get; set; } = string.Empty;
    }

    public class BigQueryFieldDef
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }

    public class BigQueries
    {
        public static List<BigQueryTableDef> Tables = new();
        public static string SchemaName = Environment.GetEnvironmentVariable("BIGQUERY_SCHEMA") ?? "your-project-id.your-dataset";

        public static string GetTablesQuery => @$"
SELECT
  table_name,
  STRING_AGG(CONCAT('- ', column_name, ' (', data_type, ')'), '\n') AS fields_list
FROM
  `{SchemaName}.INFORMATION_SCHEMA.COLUMNS`
GROUP BY
  table_name
ORDER BY
  table_name;";

        public static string GetColumnsQuery => @$"
SELECT DISTINCT table_name
FROM `{SchemaName}.INFORMATION_SCHEMA.COLUMNS`
WHERE table_name='{{table}}'
ORDER BY table_name;";

        public static string BigQueryPrompt = @$"
You have access to the following BigQuery tables. Use this schema to write accurate SQL queries.

tables:
{string.Join("\n", Tables.Select(t => t.Name))}

{string.Join("\n\n", Tables.Select(t => $@"table: {t.Name}\n{t.Fields.Select(f => $@"- {f.Name} ({f.DataType})")}"))}
";
    }
}
