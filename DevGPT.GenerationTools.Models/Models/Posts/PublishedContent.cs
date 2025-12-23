using System;

namespace DevGPTStore.Models
{
    public class PublishedContent
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public DateTime PublishDate { get; set; }
        public string ContentHook { get; set; }
    }
}

