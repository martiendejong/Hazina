using System.Text.Json.Serialization;

namespace DevGPTStore.Models.WordPress
{
    public class AioInformationQuestion
    {
        [JsonPropertyName("question")]
        public string Question { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; }
    }
}

