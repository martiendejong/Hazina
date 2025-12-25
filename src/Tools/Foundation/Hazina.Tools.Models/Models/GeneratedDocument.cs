using HazinaStore.Models;
using System.Data.SqlTypes;
using System.Runtime.Serialization;
using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;

namespace HazinaStore.Models
{
    public class GeneratedDocument : Serializer<GeneratedDocument>, IEmbedding
    {
        public string Name { get; set; }
        public List<DocumentRevision>? Revisions { get; set; }
        public string Content => Revisions.Any() ? Revisions.Last().Content : "";

        public string Id => Name;

        public List<double> Embedding { get; set; }

        public string ToDescriptiveString()
        {
            return $"{Name}:\n\n{Content}";
        }
    }
}


public class GeneratedObject<T> : Serializer<GeneratedObject<T>>, IEmbedding where T: Serializer<T>
{
    public string Name { get; set; }
    public List<ObjectRevision<T>>? Revisions { get; set; }

    public T MyObject => Revisions.Any() ? Revisions.Last().Content : null;

    public string Content => Revisions.Any() ? Revisions.Last().Content.Serialize() : "";

    public string Id => Name;

    public List<double> Embedding { get; set; }

    public string ToDescriptiveString()
    {
        return $"{Name}:\n\n{Content}";
    }
}
