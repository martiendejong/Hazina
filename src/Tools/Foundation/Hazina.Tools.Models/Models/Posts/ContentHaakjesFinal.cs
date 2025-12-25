using System.Collections.Generic;
using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;

namespace Hazina.Tools.Models
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

