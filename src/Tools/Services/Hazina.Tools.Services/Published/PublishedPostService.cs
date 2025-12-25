using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HazinaStore.Models;
using Hazina.Tools.Models;
using Hazina.Tools.Data;

namespace HazinaStore.Services
{
    public class PublishedPostService
    {
        public ProjectsRepository Projects { get; set; }
        private readonly ProjectFileLocator _fileLocator;

        public PublishedPostService(ProjectsRepository projects)
        {
            Projects = projects;
            _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
        }

        private string GetPostFilePathForProject(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("projectId is required");
            var projectFolder = _fileLocator.GetProjectFolder(projectId);
            if (!Directory.Exists(projectFolder))
                Directory.CreateDirectory(projectFolder);
            return Path.Combine(projectFolder, ProjectFileLocator.PublishedContentFile);
        }

        private void EnsurePostsFileExists(string projectId)
        {
            var filePath = GetPostFilePathForProject(projectId);
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "[]");
            }
        }

        public List<PublishedContent> GetAll(string projectId)
        {
            try
            {
                EnsurePostsFileExists(projectId);
                var filePath = GetPostFilePathForProject(projectId);
                var json = File.ReadAllText(filePath);
                var posts = JsonSerializer.Deserialize<List<PublishedContent>>(json);
                return posts?.Where(p => p != null).ToList() ?? new List<PublishedContent>();
            }
            catch
            {
                return new List<PublishedContent>();
            }
        }

        public bool Add(string projectId, PublishedContent post)
        {
            try
            {
                if (post == null || post.Id == null)
                {
                    throw new ArgumentNullException(nameof(post));
                }
                var posts = GetAll(projectId);
                posts.Add(post);
                var filePath = GetPostFilePathForProject(projectId);
                var json = JsonSerializer.Serialize(posts);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Delete(string projectId, string postId)
        {
            try
            {
                var posts = GetAll(projectId);
                var post = posts.FirstOrDefault(p => p.Id == postId);
                if (post == null) return false;
                posts.Remove(post);
                var filePath = GetPostFilePathForProject(projectId);
                var json = JsonSerializer.Serialize(posts);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public PublishedContent UpdateTextAndDate(string projectId, string postId, string nieuweText, DateTime publishDate)
        {
            EnsurePostsFileExists(projectId);
            var posts = GetAll(projectId);

            PublishedContent targetPost = null;
            targetPost = posts.FirstOrDefault(p => p.Id != null && p.Id.ToString() == postId);
            if (targetPost == null && int.TryParse(postId, out int numericId))
            {
                targetPost = posts.FirstOrDefault(p => {
                    if (p.Id == null) return false;
                    if (int.TryParse(p.Id, out int parsed))
                        return parsed == numericId;
                    return false;
                });
            }
            if (targetPost == null)
                return null;

            targetPost.Text = nieuweText;
            targetPost.PublishDate = publishDate;

            var filePath = GetPostFilePathForProject(projectId);
            var json = JsonSerializer.Serialize(posts);
            File.WriteAllText(filePath, json);
            return targetPost;
        }

        public PublishedContent UpdatePublishDate(string projectId, string postId, DateTime nieuweDatum)
        {
            EnsurePostsFileExists(projectId);
            var posts = GetAll(projectId);

            PublishedContent targetPost = posts.FirstOrDefault(p => p.Id != null && p.Id.ToString() == postId);
            if (targetPost == null && int.TryParse(postId, out int numericId))
            {
                targetPost = posts.FirstOrDefault(p => {
                    if (p.Id == null) return false;
                    if (int.TryParse(p.Id, out int parsed))
                        return parsed == numericId;
                    return false;
                });
            }
            if (targetPost == null)
                return null;

            targetPost.PublishDate = nieuweDatum;
            var filePath = GetPostFilePathForProject(projectId);
            var json = JsonSerializer.Serialize(posts);
            File.WriteAllText(filePath, json);
            return targetPost;
        }
    }
}

