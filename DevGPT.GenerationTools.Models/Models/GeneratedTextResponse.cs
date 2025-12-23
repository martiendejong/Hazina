using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models
{
    // Minimal response type used by generators to capture plain text
    public class GeneratedTextResponse : ChatResponse<GeneratedTextResponse>
    {
        public GeneratedTextResponse()
        {
        }

        public string GeneratedText { get; set; }

        [JsonIgnore]
        public override GeneratedTextResponse _example => new GeneratedTextResponse { GeneratedText = "Example text" };

        [JsonIgnore]
        public override string _signature => "{ GeneratedText: string }";
    }
}

