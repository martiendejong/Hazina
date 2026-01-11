using Hazina.Tools.AI.Agents;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Data;
using Hazina.Tools.Models;

namespace Hazina.Tools.AI.Agents
{
    public class ContentHookGeneratorAgent : GeneratorAgentBase
    {
        public ContentHookGeneratorAgent(Microsoft.Extensions.Configuration.IConfiguration configuration, string basisPrompt) : base(configuration, basisPrompt) { }

        public static bool ContentHookWhere(ContentItem item, string contentHookId)
            => item != null && (item.ContentHook == contentHookId || string.IsNullOrEmpty(contentHookId));

        public Task RegenerateContentHook(Project project, string file, ContentHook[] contentHooks, string contentHook, Action<string> streamFn, CancellationToken cancel)
            => Task.CompletedTask;

        public Task RegenerateAllContentHooks(Project project, string file, ContentHook[] contentHooks, Action<string> streamFn, CancellationToken cancel)
            => Task.CompletedTask;
    }
}
