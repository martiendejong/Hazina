using System.Collections.Generic;

namespace DevGPT.GenerationTools.Models.WordPress.Blogs
{
    public class BlogCategoriesClass : Serializer<BlogCategoriesClass>
    {
        public List<BlogCategory> BlogCategories { get; set; }
    }
}

