using System;
using System.Text.Json.Serialization;

namespace HazinaStore.Models.WordPress
{
    // Wrapper used by API controller: { data: { page_id, questions, ... } }
    [Serializable]
    public class StoreAioInformatieRequest
    {
        [JsonPropertyName("data")]
        public AioInformatieResponse Data { get; set; }
    }
}
