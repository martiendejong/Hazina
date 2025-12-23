using DevGPT.GenerationTools.Models.WordPress.Blogs;
using DevGPT.GenerationTools.AI.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;

namespace DevGPT.GenerationTools.AI.Agents
{
    public class ContentGeneratorAgent : GeneratorAgentBase
    {
        private readonly ProjectFileLocator _fileLocator;

        public ContentGeneratorAgent(Microsoft.Extensions.Configuration.IConfiguration configuration, string basisPrompt) : base(configuration, basisPrompt)
        {
            _fileLocator = new ProjectFileLocator(Projects.ProjectsFolder);
        }

        public class ContentHookExampleItem { public string ContentHook { get; set; } }
        public class ContentHookExamples { public List<ContentHookExampleItem> ContentHook { get; set; } = new(); }

        public ContentHookExamples LoadContentHooks(string id, ContentHook[] contentHook)
        {
            var result = new ContentHookExamples();
            if (contentHook != null)
                result.ContentHook = contentHook.Select(c => new ContentHookExampleItem { ContentHook = c.Name ?? c.Id }).ToList();
            return result;
        }

        public SerializableList<ContentItem> LoadVisibleContentItems(string id, string contentHookId, string file, int count)
        {
            try { var items = Serializer<ContentItem[]>.Load(_fileLocator.GetPath(id, file)); return new SerializableList<ContentItem>(items); } catch { return new SerializableList<ContentItem>(); }
        }

        public IEnumerable<ContentItem> LoadContentItems(string id, string contentHookId, string file)
        {
            try { var items = Serializer<ContentItem[]>.Load(_fileLocator.GetPath(id, file)); return items.AsEnumerable(); } catch { return new List<ContentItem>(); }
        }

        public static bool ContentHookWhere(ContentItem item, string contentHookId)
            => item != null && (item.ContentHook == contentHookId || string.IsNullOrEmpty(contentHookId));

        public Task<Tuple<string, SerializableList<ContentItem>>> InternalUpdateItemSingleWithChat(string id, string contentHookId, string chatMessageText, int itemIndex, bool editOtherItems, string file, ContentHook[] contentHooks, Project project, int numberOfItemsShown, Action<string> streamFn, SerializableList<ConversationMessage> chatMessages, CancellationToken cancel)
        {
            var list = LoadVisibleContentItems(id, contentHookId, file, numberOfItemsShown);
            return Task.FromResult(new Tuple<string, SerializableList<ContentItem>>("ok", list));
        }

        public Task<Tuple<string, SerializableList<ContentItem>>> InternalUpdateItemSingleWithChatForDate(string id, string contentHookId, string chatMessageText, string date, int itemIndex, bool editOtherItems, string file, ContentHook[] contentHooks, Project project, int numberOfItemsShown, Action<string> streamFn, SerializableList<ConversationMessage> chatMessages, CancellationToken cancel)
            => InternalUpdateItemSingleWithChat(id, contentHookId, chatMessageText, itemIndex, editOtherItems, file, contentHooks, project, numberOfItemsShown, streamFn, chatMessages, cancel);

        public Task<Tuple<string, List<ContentItem>>> InternalRegenerate(string id, string contentHookId, ContentHook[] contentHooks, Project project, Action<string> streamFn, CancellationToken cancel)
            => Task.FromResult(new Tuple<string, List<ContentItem>>("ok", new List<ContentItem>()));

        public Task<Tuple<string, List<ContentItem>>> InternalRegenerateForDate(string id, string contentHookId, string date, ContentHook[] contentHooks, Project project, Action<string> streamFn, CancellationToken cancel)
            => Task.FromResult(new Tuple<string, List<ContentItem>>("ok", new List<ContentItem>()));

        public Task<Tuple<string, List<ContentItem>>> InternalRegenerateForDateAndEvent(string id, string contentHookId, string date, string e, ContentHook[] contentHooks, Project project, Action<string> streamFn, CancellationToken cancel)
            => Task.FromResult(new Tuple<string, List<ContentItem>>("ok", new List<ContentItem>()));

        public Task<Tuple<string, SerializableList<ContentItem>>> InternalUpdateWithChat(string id, string contentHookId, string chatMessageText, Project project, string file, ContentHook[] contentHooks, int numberOfItemsShown, Action<string> streamFn, SerializableList<ConversationMessage> chatMessages, CancellationToken cancel)
        {
            var list = LoadVisibleContentItems(id, contentHookId, file, numberOfItemsShown);
            return Task.FromResult(new Tuple<string, SerializableList<ContentItem>>("ok", list));
        }

        public Task<Tuple<string, SerializableList<ContentItem>>> InternalUpdateWithChatForDate(string id, string contentHookId, string chatMessageText, string date, Project project, string file, ContentHook[] contentHooks, int numberOfItemsShown, Action<string> streamFn, SerializableList<ConversationMessage> chatMessages, CancellationToken cancel)
            => InternalUpdateWithChat(id, contentHookId, chatMessageText, project, file, contentHooks, numberOfItemsShown, streamFn, chatMessages, cancel);

        public class GeneratedPost { public string Text { get; set; } }
        public Task<GeneratedPost> InternalGenerate1ForDate(string projectId, string contentHook, string date, ContentHook[] contentHooks, Project project, Action<string> streamFn, CancellationToken cancel)
            => Task.FromResult(new GeneratedPost { Text = $"Generated content for {contentHook} on {date}" });
    }
}
