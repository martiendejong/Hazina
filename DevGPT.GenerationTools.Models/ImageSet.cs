using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models
{
    public class ImageVariant
    {
        public string Url { get; set; }
        public string Prompt { get; set; }
        public string Id { get; set; }
    }

    public class ImageSet : ChatResponse<ImageSet>
    {
        public List<ImageVariant> Images { get; set; } = new List<ImageVariant>();
        public int? SelectedIndex { get; set; }
        public string Title { get; set; }
        public string Feedback { get; set; }
        public string Key { get; set; }

        [JsonIgnore]
        public override ImageSet _example => new ImageSet
        {
            Images = new List<ImageVariant>
            {
                new ImageVariant { Id = "primary", Prompt = "Primary logo", Url = "https://example.com/logo1.png" },
                new ImageVariant { Id = "alt", Prompt = "Alternate logo", Url = "https://example.com/logo2.png" },
            },
            SelectedIndex = 0
        };

        [JsonIgnore]
        public override string _signature => "{ Images: [{ Url: string, Prompt?: string, Id?: string }], SelectedIndex?: number }";
    }
}
