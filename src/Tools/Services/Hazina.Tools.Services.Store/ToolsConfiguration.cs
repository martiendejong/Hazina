namespace Hazina.Tools.Services.Store
{
    /// <summary>
    /// Configuration for which tools should be available in StoreToolsContext.
    /// Each client can customize which tools they want to enable.
    /// </summary>
    public class ToolsConfiguration
    {
        /// <summary>
        /// Enable web search capability (PerformWebSearch tool)
        /// </summary>
        public bool EnableWebSearch { get; set; } = true;

        /// <summary>
        /// Enable reasoning capability (PerformReasoning tool)
        /// </summary>
        public bool EnableReasoning { get; set; } = true;

        /// <summary>
        /// Enable web scraping tools (PerformReadSitemap, PerformReadHtmlPage)
        /// </summary>
        public bool EnableWebScraping { get; set; } = true;

        /// <summary>
        /// Enable file operation tools (PerformReadProjectFile, AnalyseChatDocument, AnalyseChatPdfFile, PerformGetProjectFilesList)
        /// </summary>
        public bool EnableFileOperations { get; set; } = true;

        /// <summary>
        /// Enable BigQuery integration (PerformGetBigQueryResults tool)
        /// </summary>
        public bool EnableBigQuery { get; set; } = false;

        /// <summary>
        /// Enable data gathering tools (StoreGatheredData)
        /// </summary>
        public bool EnableDataGathering { get; set; } = true;

        /// <summary>
        /// Enable analysis field tools (PerformGetAnalysisFieldValue, PerformUpdateAnalysisField, PerformGenerateAnalysisFieldWithFeedback)
        /// </summary>
        public bool EnableAnalysisTools { get; set; } = true;

        /// <summary>
        /// Enable workflow component tools (ShowLogoDecision, etc.)
        /// </summary>
        public bool EnableWorkflowComponents { get; set; } = true;

        /// <summary>
        /// Returns a configuration with all tools enabled (default behavior)
        /// </summary>
        public static ToolsConfiguration All()
        {
            return new ToolsConfiguration
            {
                EnableWebSearch = true,
                EnableReasoning = true,
                EnableWebScraping = true,
                EnableFileOperations = true,
                EnableBigQuery = true,
                EnableDataGathering = true,
                EnableAnalysisTools = true,
                EnableWorkflowComponents = true
            };
        }

        /// <summary>
        /// Returns a minimal configuration with only essential tools
        /// </summary>
        public static ToolsConfiguration Minimal()
        {
            return new ToolsConfiguration
            {
                EnableWebSearch = false,
                EnableReasoning = true,
                EnableWebScraping = false,
                EnableFileOperations = true,
                EnableBigQuery = false,
                EnableDataGathering = true,
                EnableAnalysisTools = false,
                EnableWorkflowComponents = false
            };
        }

        /// <summary>
        /// Returns default configuration (currently same as All)
        /// </summary>
        public static ToolsConfiguration Default()
        {
            return All();
        }
    }
}
