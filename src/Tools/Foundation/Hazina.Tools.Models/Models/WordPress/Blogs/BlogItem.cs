using System;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPTStore.Models
{
    public class BlogItem : Serializer<BlogItem>
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Body { get; set; }
        public DateTime ScheduledPublishDateTime { get; set; }
        public DateTime? PublishedDateTime { get; set; }
        public string WordpressPostId { get; set; }
        public int WordpressCategoryId { get; set; }
    }
}

