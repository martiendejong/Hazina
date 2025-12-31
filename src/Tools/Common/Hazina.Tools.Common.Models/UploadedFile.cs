using System.Collections.Generic;
using System.Text.Json.Serialization;

// Keep legacy namespace to minimize breaking changes across the codebase
namespace HazinaStore.Models
{
    public class UploadedFile : IEmbedding
    {
        public string Filename { get; set; }
        public string TextFilename { get; set; }
        public string Label { get; set; }
        public string Extension { get; set; }
        public int TokenCount { get; set; }
        public List<List<double>> Parts { get; set; } = new List<List<double>>();

        public string Id => Filename;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Content { get; set; }

        public List<double> Embedding { get; set; }

        /// <summary>
        /// Tags for categorizing and filtering documents
        /// Examples: "text", "image", "upload", "generated", "website", "webpage", "product"
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        public string ToDescriptiveString() => Content;
    }
}

