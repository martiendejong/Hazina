
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models.Social
{
    public class ConnectedFacebookPost
    {
        public string Id { get; set; }
        public string Message { get; set; } = "";

        [JsonPropertyName("CreatedTime")]
        public string Created { get; set; }

        [JsonPropertyName("PermalinkUrl")]
        public string Url { get; set; }

        [JsonPropertyName("FullPicture")]
        public string FullPicture { get; set; }

        public string PictureUrl { get; set; }

        public int Shares { get; set; }

        [JsonConverter(typeof(FlexibleCommentsConverter))]
        public List<ConnectedFacebookComment> Comments { get; set; }

        [JsonConverter(typeof(FlexibleReactionsConverter))]
        public Dictionary<string, int> Reactions { get; set; }
    }
}

