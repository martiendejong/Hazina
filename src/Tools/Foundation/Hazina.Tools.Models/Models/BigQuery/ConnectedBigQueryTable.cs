using System.Collections.Generic;

namespace DevGPT.GenerationTools.Models.BigQuery
{
    public class ConnectedBigQueryTable
    {
        public string Name { get; set; }
        public List<string>? ColumnNames { get; set; }
        public List<string>? RecordsAsCsv { get; set; }
        public int NumRecords { get; set; }
    }
}

