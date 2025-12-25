using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HazinaStore.Models.WordPress
{
    public class KnowledgeBaseRequest
    {
        [JsonPropertyName("categories")]
        public List<KnowledgeBaseCategory> Categories { get; set; }

        [JsonPropertyName("custom_html")]
        public string CustomHtml { get; set; } = string.Empty;

        [JsonPropertyName("custom_css")]
        public string CustomCss { get; set; } = string.Empty;

        [JsonPropertyName("custom_js")]
        public string CustomJs { get; set; } = string.Empty;
    }

    public class KnowledgeBaseCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("qas")]
        public List<KnowledgeBaseQa> Qas { get; set; }
    }

    public class KnowledgeBaseQa
    {
        [JsonPropertyName("question")]
        public string Question { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; }
    }
}

