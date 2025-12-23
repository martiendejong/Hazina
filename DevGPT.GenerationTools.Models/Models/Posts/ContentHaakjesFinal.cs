using System.Collections.Generic;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPT.GenerationTools.Models
{
    public class ContentHookFinal : Serializer<ContentHookFinal>
    {
        public string Id { get; set; }
        public string Naam { get; set; }
        public string Omschrijving { get; set; }
        public string Waarom { get; set; }
        public List<string>? Voorbeelden { get; set; }
        public bool? Like { get; set; }
    }

    public class ContentHooksFinal : Serializer<ContentHooksFinal>
    {
        public List<ContentHookFinal> ContentHooks { get; set; } = new();
    }
}

