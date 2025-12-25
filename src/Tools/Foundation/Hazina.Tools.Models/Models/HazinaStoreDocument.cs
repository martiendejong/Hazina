using HazinaStore.Models;

namespace HazinaStore.Models
{
    public class HazinaStoreDocument : AEmbedding, IEmbedding
    {
        public string Id { get => Label; set => Label = value; }
        public string Label { get; set; }
        public string Content { get; set; }
        public string ToDescriptiveString()
        {
            return $"{Label}:\n{Content}";
        }
    }
}
