using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevGPTStore.Models;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Data;
using Newtonsoft.Json;
using DevGPTStore.Services;
using System.Threading.Tasks;

namespace DevGPTStore
{
    public class BlogService
    {
        private readonly ProjectFileLocator _fileLocator;

        public BlogService(ProjectFileLocator fileLocator)
        {
            _fileLocator = fileLocator;
        }

        private string GetBlogFolder(string projectId)
        {
            var projectPath = _fileLocator.GetProjectFolder(projectId);
            var blogFolder = Path.Combine(projectPath, "blogs");
            if (!Directory.Exists(blogFolder))
                Directory.CreateDirectory(blogFolder);
            return blogFolder;
        }

        public void AddBlogItem(string projectId, BlogItem item)
        {
            var folder = GetBlogFolder(projectId);
            if (string.IsNullOrEmpty(item.Id))
                item.Id = Guid.NewGuid().ToString();
            var file = Path.Combine(folder, $"{item.Id}.json");
            if (File.Exists(file))
                throw new IOException($"BlogItem with Id {item.Id} already exists.");
            File.WriteAllText(file, JsonConvert.SerializeObject(item));
        }

        public void UpdateBlogItem(string projectId, string id, BlogItem item)
        {
            var folder = GetBlogFolder(projectId);
            var file = Path.Combine(folder, $"{id}.json");
            if (!File.Exists(file))
                throw new FileNotFoundException();
            item.Id = id;
            File.WriteAllText(file, JsonConvert.SerializeObject(item));
        }

        public void DeleteBlogItem(string projectId, string id)
        {
            var folder = GetBlogFolder(projectId);
            var file = Path.Combine(folder, $"{id}.json");
            if (File.Exists(file))
                File.Delete(file);
            else
                throw new FileNotFoundException();
        }

        public BlogItem GetBlogItem(string projectId, string blogId)
        {
            var folder = GetBlogFolder(projectId);
            var file = Path.Combine(folder, blogId);
            if (!Directory.Exists(folder) || !File.Exists(file))
                return new BlogItem { Id = blogId };

            var json = File.ReadAllText(file);
            var item = JsonConvert.DeserializeObject<BlogItem>(json);

            return item;
        }

        public List<BlogItem> GetBlogItems(string projectId)
        {
            var folder = GetBlogFolder(projectId);
            if (!Directory.Exists(folder))
                return new List<BlogItem>();
            var items = new List<BlogItem>();
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var item = JsonConvert.DeserializeObject<BlogItem>(json);
                    if (item != null)
                        items.Add(item);
                }
                catch { }
            }
            return items
                .OrderByDescending(b => b.ScheduledPublishDateTime)
                .ThenByDescending(b => b.PublishedDateTime ?? DateTime.MinValue)
                .ToList();
        }

        public async Task<string> PublishBlogItemToWordPress(WordpressBlogService wpService, BlogItem item, string projectId = null)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            string categorie = item.WordpressCategoryId != 0 ? item.WordpressCategoryId.ToString() : (item.Title ?? "");
            var (success, postId, url) = await wpService.CreateBlogPostAsync(item.Title, categorie, item.Body);
            if (!success || postId == 0)
                throw new Exception("WordPress publicatie mislukt of postId niet ontvangen.");

            item.PublishedDateTime = DateTime.Now;
            if (!string.IsNullOrEmpty(projectId))
            {
                UpdateBlogItem(projectId, item.Id, item);
            }
            return postId.ToString();
        }
    }
}
