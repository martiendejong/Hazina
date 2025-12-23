using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DevGPTStore.Models.WordPress
{
    [Serializable]
    public class AioInformatieResponse
    {
        [JsonPropertyName("page_id")]
        public int PageId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("questions")]
        public List<AioInformationQuestion> Questions { get; set; } = new();

        [JsonPropertyName("custom_html")]
        public string CustomHTML { get; set; }

        [JsonPropertyName("custom_css")]
        public string CustomCSS { get; set; }

        [JsonPropertyName("custom_js")]
        public string CustomJS { get; set; }
    }
}

