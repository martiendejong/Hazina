using DevGPT.GenerationTools.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Services.WordPress;
using DevGPT.GenerationTools.Services.FileOps.Helpers;
using System.Data.SqlTypes;
using System.Collections.Generic;
using System.Linq;

namespace DevGPTStore.ContentRetrieval
{
    public class ContentRetrievalService
    {
        private readonly ProjectFileLocator _fileLocator;
        private readonly IConfiguration _config;
        private string ApiKey;

        public ContentRetrievalService(ProjectFileLocator fileLocator, IConfiguration config, string apiKey)
        {
            ApiKey = apiKey;
            _fileLocator = fileLocator;
            _config = config;
        }

        public static string StripNonAscii(string input)
        {
            return Regex.Replace(input, "[^a-zA-Z0-9]", "");
        }

        public async Task ImportWordpressSite(string projectId, string siteurl)
        {
            var site = _config.GetSection("ApiSettings").GetValue<string>("SocialMediaHandboekWebsite");
            var user = _config.GetSection("ApiSettings").GetValue<string>("SocialMediaHandboekUser");
            var pass = _config.GetSection("ApiSettings").GetValue<string>("SocialMediaHandboekPassword");

            var pages = await WordPressScraper.ScrapeWordpressPages(site + "/wp-json/wp/v2/pages", user, pass, (link, title, content, raw) => true);
            var categoryPosts = await TryHandleAll(async () => await WordPressScraper.ScrapeWordpressCategories(site, user, pass, (link, title, content, raw) => true));
            var posts = await TryHandleAll(async () => await WordPressScraper.ScrapeWordpressPosts(site + "/wp-json/wp/v2/posts", user, pass, (link, title, content, raw) => true));

            var combined = pages
                .Concat(categoryPosts)
                .Concat(posts)
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value);

            var projectFolder = _fileLocator.GetProjectFolder(projectId);
            var uploadsFolder = Path.Combine(projectFolder, "Uploads");
            CRHelpers.EnsureDirectoryExists(uploadsFolder);

            foreach (var item in combined)
            {
                var content = item.Value;
                var name = "handboek." + StripNonAscii(item.Key) + ".html.txt";
                var filePath = Path.Combine(uploadsFolder, name);
                await File.WriteAllTextAsync(filePath, content.Content ?? string.Empty);

                // Update legacy uploadedFiles.json metadata
                var uploaded = CRHelpers.MakeUploadedFile(filePath, name);
                var listFilePath = Path.Combine(projectFolder, "uploadedFiles.json");
                await CRHelpers.UpdateUploadedFilesListAsync(listFilePath, uploaded);
            }
        }

        private static async Task<T?> TryHandleAll<T>(Func<Task<T>> func)
        {
            try
            {
                return await func();
            }
            catch (Exception ex) { }

            return default;
        }

    }
}



