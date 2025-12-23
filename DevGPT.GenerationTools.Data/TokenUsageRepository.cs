using DevGPT.GenerationTools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DevGPT.GenerationTools.Data
{
    /// <summary>
    /// Repository for tracking and retrieving token usage per project
    /// </summary>
    public class TokenUsageRepository
    {
        private readonly ProjectFileLocator _fileLocator;

        public TokenUsageRepository(ProjectFileLocator fileLocator)
        {
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        /// <summary>
        /// Records token usage for a project on the current date
        /// </summary>
        public void RecordUsage(string projectId, int promptTokens, int completionTokens, string model)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            var usage = new TokenUsage
            {
                ProjectId = projectId,
                Date = DateTime.UtcNow.Date,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                Model = model ?? "unknown"
            };

            var filePath = GetUsageFilePath(projectId, usage.Date);
            EnsureDirectory(filePath);

            // Load existing usage for today if it exists
            var existingUsage = LoadUsageForDate(projectId, usage.Date);
            if (existingUsage != null)
            {
                // Aggregate with existing usage
                usage.PromptTokens += existingUsage.PromptTokens;
                usage.CompletionTokens += existingUsage.CompletionTokens;
                usage.TotalTokens += existingUsage.TotalTokens;
            }

            var json = JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Gets token usage summaries for a project within a date range
        /// </summary>
        public List<TokenUsageSummary> GetUsage(string projectId, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            var usageFolder = GetUsageFolder(projectId);
            if (!Directory.Exists(usageFolder))
                return new List<TokenUsageSummary>();

            var summaries = new List<TokenUsageSummary>();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var usage = LoadUsageForDate(projectId, date);
                summaries.Add(new TokenUsageSummary
                {
                    Date = date,
                    TotalTokens = usage?.TotalTokens ?? 0,
                    PromptTokens = usage?.PromptTokens ?? 0,
                    CompletionTokens = usage?.CompletionTokens ?? 0
                });
            }

            return summaries;
        }

        /// <summary>
        /// Gets aggregated usage for the current week (last 7 days)
        /// </summary>
        public List<TokenUsageSummary> GetWeekUsage(string projectId)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-6); // Last 7 days including today
            return GetUsage(projectId, startDate, endDate);
        }

        /// <summary>
        /// Gets aggregated usage for the current month (last 30 days)
        /// </summary>
        public List<TokenUsageSummary> GetMonthUsage(string projectId)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-29); // Last 30 days including today
            return GetUsage(projectId, startDate, endDate);
        }

        /// <summary>
        /// Gets aggregated usage for the current year (12 months)
        /// </summary>
        public List<TokenUsageSummary> GetYearUsage(string projectId)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = new DateTime(endDate.Year, 1, 1); // Start of current year

            var allDays = GetUsage(projectId, startDate, endDate);

            // Group by month
            var monthlyUsage = allDays
                .GroupBy(u => new DateTime(u.Date.Year, u.Date.Month, 1))
                .Select(g => new TokenUsageSummary
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    PromptTokens = g.Sum(u => u.PromptTokens),
                    CompletionTokens = g.Sum(u => u.CompletionTokens)
                })
                .OrderBy(u => u.Date)
                .ToList();

            // Ensure all 12 months are present
            var result = new List<TokenUsageSummary>();
            for (int month = 1; month <= 12; month++)
            {
                var monthDate = new DateTime(endDate.Year, month, 1);
                var existing = monthlyUsage.FirstOrDefault(u => u.Date == monthDate);
                result.Add(existing ?? new TokenUsageSummary
                {
                    Date = monthDate,
                    TotalTokens = 0,
                    PromptTokens = 0,
                    CompletionTokens = 0
                });
            }

            return result;
        }

        private TokenUsage? LoadUsageForDate(string projectId, DateTime date)
        {
            var filePath = GetUsageFilePath(projectId, date);
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<TokenUsage>(json);
            }
            catch
            {
                return null;
            }
        }

        private string GetUsageFolder(string projectId)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectId);
            return Path.Combine(projectFolder, ".token-usage");
        }

        private string GetUsageFilePath(string projectId, DateTime date)
        {
            var folder = GetUsageFolder(projectId);
            var filename = $"{date:yyyy-MM-dd}.json";
            return Path.Combine(folder, filename);
        }

        private void EnsureDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
