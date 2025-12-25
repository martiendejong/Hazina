using HazinaStore.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Hazina.Tools.Data;
using Hazina.Tools.Services.FileOps.Helpers;
using Hazina.Tools.Services.WordPress;
using HazinaStore;

namespace Hazina.Tools.Services.Web
{
    /// <summary>
    /// Service responsible for web scraping operations including sitemap reading,
    /// webpage scraping, and website import functionality.
    /// This refactored version avoids direct LLM dependencies and hardcoded API keys.
    /// </summary>
    public class WebScrapingService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ProjectFileLocator _fileLocator;
        private readonly IntakeRepository _intake;
        private readonly string _projectId;

        public WebScrapingService(string apiKey, string model, ProjectFileLocator fileLocator, IntakeRepository intake, string projectId, object initContentAgent)
        {
            _apiKey = apiKey;
            _model = model;
            _fileLocator = fileLocator;
            _intake = intake;
            _projectId = projectId;
        }

        // Minimal context ctor retained for compatibility
        public WebScrapingService(string apiKey, string model, ProjectFileLocator fileLocator, IntakeRepository intake, string projectId, object initContentAgent, bool minimalContext = false)
            : this(apiKey, model, fileLocator, intake, projectId, initContentAgent)
        { }

        public async Task<string> ReadSitemapAsync(string url)
        {
            try
            {
                var sitemapUrl = EnsureAbsolute(url);
                using var http = new HttpClient();
                return await http.GetStringAsync(sitemapUrl);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> ReadWebPageAsync(string url, bool raw = false)
        {
            try
            {
                return await WebPageScraper.ScrapeWebPage(url, raw);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Imports a website: tries WordPress API import, falls back to parsing sitemap.xml and scraping top pages.
        /// Writes pages to Uploads and updates uploadedFiles.json, splitting long files when needed.
        /// </summary>
        public async Task<bool> ImportWholeWebsiteAsync(string baseUrl)
        {
            // Try dedicated WordPress import first
            try
            {
                var projectsRepo = new ProjectsRepository(HazinaStoreConfigLoader.LoadHazinaStoreConfig(), null);
                var contentRetrieval = new HazinaStore.ContentRetrieval.ContentRetrievalService(_fileLocator, projectsRepo.AppConfig, _apiKey);
                await contentRetrieval.ImportWordpressSite(_projectId, baseUrl);
                return true;
            }
            catch { /* fall back to sitemap scraping */ }

            try
            {
                var urls = await GetAllSitemapUrls(baseUrl);

                // Keep only relevant on-site pages and trim to top 25
                var filtered = urls
                    .Where(u => IsLikelyRelevantPage(baseUrl, u))
                    .Take(25)
                    .ToList();

                var projectFolder = _fileLocator.GetProjectFolder(_projectId);
                var uploadsFolder = Path.Combine(projectFolder, "Uploads");
                var listFilePath = Path.Combine(projectFolder, "uploadedFiles.json");
                WebHelpers.EnsureDirectoryExists(uploadsFolder);

                var tokenCounter = new Hazina.Tools.Services.FileOps.Helpers.TokenCounter();

                foreach (var page in filtered)
                {
                    try
                    {
                        var webpage = await WebPageScraper.ScrapeWebPage(page, raw: false);
                        var filename = BuildSafeFilename(page);
                        var filePath = Path.Combine(uploadsFolder, filename);

                        File.WriteAllText(filePath, webpage);

                        var tokenCount = tokenCounter.CountTokens(webpage);
                        var uploadedFile = WebHelpers.MakeUploadedFile(filePath, filename, tokenCount);
                        File.WriteAllText(Path.Combine(uploadsFolder, uploadedFile.TextFilename), webpage);
                        await WebHelpers.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);

                        var files = await WebHelpers.SplitFiles(filePath, webpage);
                        if (files.Count > 1)
                        {
                            uploadedFile.Parts.Clear();
                            foreach (var _ in files)
                                uploadedFile.Parts.Add(new List<double>());
                            await WebHelpers.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);
                        }
                    }
                    catch
                    {
                        // Continue with next page on failure
                    }
                }

                // Initialize legacy store embeddings (no-op placeholder)
                // legacy sync no-op

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<string> PerformWebSearchAsync(string query)
        {
            // RapidAPI search was removed; avoid shipping keys. Provide a safe fallback message.
            return Task.FromResult("Web search is not configured in this environment.");
        }

        private static string EnsureAbsolute(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be empty.");
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url.Trim('/');
            }
            return url;
        }

        private static string BuildSafeFilename(string pageUrl)
        {
            var parts = pageUrl.Split('/')
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Skip(1);
            var name = string.Join("_", parts);
            name = name.Replace(".", string.Empty);
            if (string.IsNullOrWhiteSpace(name)) name = "index";
            return name;
        }

        private static bool IsLikelyRelevantPage(string baseUrl, string url)
        {
            try
            {
                var baseHost = new Uri(EnsureAbsolute(baseUrl)).Host;
                var u = new Uri(EnsureAbsolute(url));
                if (!string.Equals(u.Host, baseHost, StringComparison.OrdinalIgnoreCase))
                    return false;
                var path = u.AbsolutePath.ToLowerInvariant();
                if (path.Contains("/tag/") || path.Contains("/category/") || path.Contains("/feed"))
                    return false;
                return true;
            }
            catch { return false; }
        }

        private static async Task<List<string>> GetAllSitemapUrls(string baseUrl)
        {
            var urls = new List<string>();
            var sitemapUrl = EnsureAbsolute(baseUrl.TrimEnd('/') + "/sitemap.xml");
            using var http = new HttpClient();
            string xml;
            try
            {
                xml = await http.GetStringAsync(sitemapUrl);
            }
            catch
            {
                return urls;
            }

            try
            {
                var doc = XDocument.Parse(xml);
                XNamespace ns = doc.Root!.Name.Namespace;
                if (doc.Root!.Name.LocalName.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase))
                {
                    var children = doc.Root!.Elements(ns + "sitemap").Select(e => e.Element(ns + "loc")?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                    foreach (var child in children)
                    {
                        try
                        {
                            var childXml = await http.GetStringAsync(child);
                            var childDoc = XDocument.Parse(childXml);
                            var childUrls = childDoc.Root!.Elements(ns + "url").Select(e => e.Element(ns + "loc")?.Value).Where(v => !string.IsNullOrWhiteSpace(v));
                            urls.AddRange(childUrls!);
                        }
                        catch { }
                    }
                }
                else
                {
                    var pageUrls = doc.Root!.Elements(ns + "url").Select(e => e.Element(ns + "loc")?.Value).Where(v => !string.IsNullOrWhiteSpace(v));
                    urls.AddRange(pageUrls!);
                }
            }
            catch { }

            return urls.Distinct().ToList();
        }
    }
}


