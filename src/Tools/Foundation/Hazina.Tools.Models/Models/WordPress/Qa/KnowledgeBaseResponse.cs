using System.Collections.Generic;

namespace DevGPTStore.Models.WordPress
{
    public class KnowledgeBaseResponse
    {
        public int PostId { get; set; }
        public List<KnowledgeBaseCategory> Categories { get; set; }
        public string CustomHtml { get; set; }
        public string CustomCss { get; set; }
        public string CustomJs { get; set; }
    }
}

