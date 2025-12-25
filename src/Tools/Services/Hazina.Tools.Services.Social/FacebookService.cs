using Hazina.Tools.Models.Social;
using Hazina.Tools.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Hazina.Tools.Services.Social
{
    public class FacebookService
    {
        public ProjectsRepository Projects { get; set; }
        private ProjectFileLocator _fileLocator => new ProjectFileLocator(Projects.ProjectsFolder);
        public void Remove(string projectid, string pageid)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectid);
            var socialMediaFolder = Path.Combine(projectFolder, "socialmedia");
            var facebookFolder = Path.Combine(socialMediaFolder, "facebook");
            var pagesFile = Path.Combine(facebookFolder, "pages");
            var json = System.IO.File.ReadAllText(pagesFile);
            var pages = JsonSerializer.Deserialize<List<ConnectedFacebookPage>>(json);
            var existingPage = pages.FirstOrDefault(p => p.Id == pageid);
            pages.Remove(existingPage);

            System.IO.File.WriteAllText(pagesFile, JsonSerializer.Serialize(pages));
        }

        public void Store(string projectid, ConnectedFacebookPage page)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectid);
            var socialMediaFolder = Path.Combine(projectFolder, "socialmedia");
            var facebookFolder = Path.Combine(socialMediaFolder, "facebook");
            Directory.CreateDirectory(facebookFolder);

            var pagesFile = Path.Combine(facebookFolder, "pages");
            var pages = new List<ConnectedFacebookPage>();
            if (System.IO.File.Exists(pagesFile))
            {
                var json = System.IO.File.ReadAllText(pagesFile);
                pages = JsonSerializer.Deserialize<List<ConnectedFacebookPage>>(json);
            }
            var existingPage = pages.FirstOrDefault(p => p.Id == page.Id);
            if (existingPage == null)
                pages.Add(page);
            else
            {
                existingPage.Url = page.Url;
                existingPage.Token = page.Token;
                existingPage.Name = page.Name;
            }
            System.IO.File.WriteAllText(pagesFile, JsonSerializer.Serialize(pages));
        }

        public List<ConnectedFacebookPage> GetImportedPages(string projectid)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectid);
            var socialMediaFolder = Path.Combine(projectFolder, "socialmedia");
            var facebookFolder = Path.Combine(socialMediaFolder, "facebook");
            Directory.CreateDirectory(facebookFolder);

            var pagesFile = Path.Combine(facebookFolder, "pages");
            var pages = new List<ConnectedFacebookPage>();
            if (System.IO.File.Exists(pagesFile))
            {
                var json = System.IO.File.ReadAllText(pagesFile);
                pages = JsonSerializer.Deserialize<List<ConnectedFacebookPage>>(json);
            }

            foreach (var page in pages)
            {
                var postsFile = Path.Combine(facebookFolder, page.Id);
                page.Posts = ExtractRows(postsFile);
            }

            return pages;
        }

        public void StorePosts(string projectid, string pageid, ConnectedFacebookPost[] posts)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectid);
            var socialMediaFolder = Path.Combine(projectFolder, "socialmedia");
            var facebookFolder = Path.Combine(socialMediaFolder, "facebook");
            Directory.CreateDirectory(facebookFolder);
            var pagesFile = Path.Combine(facebookFolder, "pages");

            if (!System.IO.File.Exists(pagesFile)) throw new Exception("No pages have been imported");
            var json = System.IO.File.ReadAllText(pagesFile);
            var pages = JsonSerializer.Deserialize<List<ConnectedFacebookPage>>(json);

            if (!pages.Any(p => p.Id == pageid)) throw new Exception("Page not found");

            var postsFile = Path.Combine(facebookFolder, pageid);

            var existingPosts = ExtractRows(postsFile);

            foreach (var newItem in posts)
            {
                var existingItem = existingPosts.FirstOrDefault(x => x.Id == newItem.Id);
                if (existingItem != null)
                {
                    existingItem.Url = newItem.Url;
                    existingItem.Message = newItem.Message;
                    existingItem.Created = newItem.Created;
                    existingItem.Reactions = newItem.Reactions;
                    if (newItem.Comments != null && newItem.Comments.Any())
                    {
                        if (existingItem.Comments == null)
                        {
                            existingItem.Comments = [];
                        }

                        foreach (var comment in newItem.Comments)
                        {
                            var existingComment = existingItem.Comments.FirstOrDefault(x => x.Id == comment.Id);
                            if (existingComment != null)
                            {
                                existingComment.Message = comment.Message;
                                existingComment.Created = comment.Created;
                            }
                            else
                            {
                                existingItem.Comments.Add(comment);
                            }
                        }
                    }
                }
                else
                {
                    existingPosts.Add(newItem);
                }
            }

            var itemsPerStep = 25;
            var step = 1;
            for (var i = 0; i < existingPosts.Count; i += itemsPerStep)
            {
                var fileContent = JsonSerializer.Serialize(existingPosts.Skip(i).Take(itemsPerStep).ToList());
                var rowsFile = postsFile + "." + i;
                System.IO.File.WriteAllText(rowsFile, fileContent);
                ++step;
            }
        }

        public static List<ConnectedFacebookPost> ExtractRows(string postsFile)
        {
            var existingPosts = new List<ConnectedFacebookPost>();

            var ii = 0;
            var rowsFile2 = postsFile + "." + ii;
            while (System.IO.File.Exists(rowsFile2))
            {
                var postsjson = System.IO.File.ReadAllText(rowsFile2);
                existingPosts.AddRange(JsonSerializer.Deserialize<List<ConnectedFacebookPost>>(postsjson));
                ++ii;
                rowsFile2 = postsFile + "." + ii;
            }

            return existingPosts;
        }

    }

}

