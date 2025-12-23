using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models
{
    public class ItemsListItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }

    // Generic chat response contract used by generator for core values
    public class ItemsList : ChatResponse<ItemsList>
    {
        public ItemsList()
        {
        }

        public List<ItemsListItem> Values { get; set; } = new List<ItemsListItem>();

        [JsonIgnore]
        public override ItemsList _example => new ItemsList
        {
            Values = new List<ItemsListItem>
            {
                new ItemsListItem { Title = "Integrity", Description = "We act with honesty and strong moral principles" },
                new ItemsListItem { Title = "Innovation", Description = "We constantly seek new and creative solutions" }
            }
        };

        [JsonIgnore]
        public override string _signature => "{ Values: [{ Title: string, Description: string }] }";
    }
}
