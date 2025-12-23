using System;

namespace DevGPT.GenerationTools.Models
{
    /// <summary>
    /// Represents token usage for a project on a specific date
    /// </summary>
    public class TokenUsage
    {
        public string ProjectId { get; set; }
        public DateTime Date { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public string Model { get; set; }

        public TokenUsage()
        {
            ProjectId = string.Empty;
            Date = DateTime.UtcNow.Date;
            Model = string.Empty;
        }
    }

    /// <summary>
    /// Aggregated token usage for reporting
    /// </summary>
    public class TokenUsageSummary
    {
        public DateTime Date { get; set; }
        public int TotalTokens { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
}
