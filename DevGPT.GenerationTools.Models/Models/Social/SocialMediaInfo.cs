using OpenAI.Chat;
using System.Collections.Generic;
using DevGPTStore.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPT.GenerationTools.Models.Social
{
    public class SocialMediaInfo : Serializer<SocialMediaInfo>
    {
        public SocialMediaAddress Address { get; set; }
        public List<CommentInfo> Comments { get; set; } = new List<CommentInfo>();
    }
}
