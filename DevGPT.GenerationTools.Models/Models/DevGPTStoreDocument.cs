using DevGPTStore.Models;

namespace DevGPTStore.Models
{
    public class DevGPTStoreDocument : AEmbedding, IEmbedding
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
