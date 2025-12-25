using System.Collections.Generic;

namespace Hazina.Tools.Models.BigQuery
{
    public class ConnectedBigQueryAccount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<ConnectedBigQueryTable> Tables { get; set; }
    }
}

