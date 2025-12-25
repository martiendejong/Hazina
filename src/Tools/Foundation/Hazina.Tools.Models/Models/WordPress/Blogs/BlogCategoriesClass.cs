using System.Collections.Generic;

namespace Hazina.Tools.Models.WordPress.Blogs
{
    public class BlogCategoriesClass : Serializer<BlogCategoriesClass>
    {
        public List<BlogCategory> BlogCategories { get; set; }
    }
}

