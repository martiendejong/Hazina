using System.Collections.Generic;
using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;

namespace Hazina.Tools.Models
{
    public class ContentHookFinal : Serializer<ContentHookFinal>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Reason { get; set; }
        public List<string>? Examples { get; set; }
        public bool? Like { get; set; }
    }

    public class ContentHooksFinal : Serializer<ContentHooksFinal>
    {
        public List<ContentHookFinal> ContentHooks { get; set; } = new();
    }
}

