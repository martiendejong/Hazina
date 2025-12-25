using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System;

namespace DevGPT.GenerationTools.Models
{
    public class ContentItem : Serializer<ContentItem>
    {
        public ContentItem() { }
        public ContentItem(string text, string ContentHook)
        {
            Text = text;
            ContentHook = ContentHook;
        }
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; }
        public string ContentHook { get; set; }
        public ContentItemState State { get; set; } = ContentItemState.New;
    }
}


