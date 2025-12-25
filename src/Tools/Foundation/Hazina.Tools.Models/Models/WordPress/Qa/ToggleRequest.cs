using System;
using System.Text.Json.Serialization;

namespace DevGPTStore.Models.WordPress
{
    [Serializable]
    public class ToggleRequest
    {
        [JsonPropertyName("pageId")]
        public int PageId { get; set; }

        [JsonPropertyName("toggleValue")]
        public bool ToggleValue { get; set; }
    }

    [Serializable]
    public class WordpressToggleRequest
    {
        public WordpressToggleRequest() { }
        public WordpressToggleRequest(ToggleRequest request)
        {
            ToggleValue = request.ToggleValue;
            PageId = request.PageId;
        }

        [JsonPropertyName("page_id")]
        public int PageId { get; set; }

        [JsonPropertyName("enabled")]
        public bool ToggleValue { get; set; }
    }
}

