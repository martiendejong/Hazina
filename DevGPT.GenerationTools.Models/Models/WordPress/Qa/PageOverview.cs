using System;
using System.Text.Json.Serialization;

namespace DevGPTStore.Models.WordPress
{
    [Serializable]
    public class PageOverview
    {
        [JsonPropertyName("ID")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("_aio_enabled")]
        public bool aio_enabled { get; set; }
    }
}

