using OpenAI.Chat;
using System.Collections.Generic;
using HazinaStore.Models;
using Hazina.Tools.Models.WordPress.Blogs;

namespace Hazina.Tools.Models.Social
{
    public class SocialMediaInfo : Serializer<SocialMediaInfo>
    {
        public SocialMediaAddress Address { get; set; }
        public List<CommentInfo> Comments { get; set; } = new List<CommentInfo>();
    }
}
