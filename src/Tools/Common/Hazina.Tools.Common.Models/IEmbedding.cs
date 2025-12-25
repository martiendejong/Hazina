using System.Collections.Generic;

// Keep legacy namespace to minimize breaking changes across the codebase
namespace HazinaStore.Models
{
    public interface IEmbedding
    {
        string Id { get; }
        string Content { get; }
        string ToDescriptiveString();
        List<double> Embedding { get; set; }
    }
}

