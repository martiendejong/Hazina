using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models
{
    // Generic chat response contract used by generator
    public class TagsList : ChatResponse<TagsList>
    {
        public TagsList()
        {
        }

        public List<string> ToneOfVoiceDescriptors { get; set; } = new List<string>();

        [JsonIgnore]
        public override TagsList _example => new TagsList { ToneOfVoiceDescriptors = new List<string> { "Friendly", "Confident" } };

        [JsonIgnore]
        public override string _signature => "{ ToneOfVoiceDescriptors: string[] }";
    }
}
