using Hazina.Tools.AI.Agents;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hazina.Tools.Data;
using Hazina.Tools.Models;

namespace Hazina.Tools.AI.Agents
{
    public class LegacyBlogGeneratorAgent : GeneratorAgentBase
    {
        public LegacyBlogGeneratorAgent(Microsoft.Extensions.Configuration.IConfiguration configuration, string basisPrompt, ProjectsRepository projects) : base(configuration, basisPrompt) { }

        public class Blog
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string Body { get; set; }
            public int WpCategoryId { get; set; }
            public DateTime? PublishedDateTime { get; set; }
            public DateTime? ScheduledPublishDateTime { get; set; }
            public string WordpressPostId { get; set; }
        }

        public Task<Blog> GenerateBlogUpdate(Project project, object currentBlog, string feedback, Action<string> streamFn)
            => Task.FromResult(new Blog { Title = "", Description = "", Body = "" });

        public Task<Blog> GenerateBlog(Project project, string request, List<string> categories, DateTime scheduledPublishDate)
            => Task.FromResult(new Blog { Title = "", Description = "", Body = "", WpCategoryId = 0, ScheduledPublishDateTime = scheduledPublishDate });

        public Task<Blog> GenerateBlog(Project project, List<string> categories, DateTime scheduledPublishDate)
            => Task.FromResult(new Blog { Title = "", Description = "", Body = "", WpCategoryId = 0, ScheduledPublishDateTime = scheduledPublishDate });

        public Task<string> GenerateTextReplacement(Project project, object currentBlog, string selectedText, string fullContext, Action<string> streamFn, string instruction)
            => Task.FromResult(selectedText);
    }
}
