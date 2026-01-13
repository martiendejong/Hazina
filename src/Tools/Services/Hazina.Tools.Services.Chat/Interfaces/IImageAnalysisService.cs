using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat.Interfaces
{
    public interface IImageAnalysisService
    {
        /// <summary>
        /// Analyze image content using AI vision models
        /// </summary>
        /// <param name="imageUrl">URL or file path to the image</param>
        /// <param name="imageBytes">Optional: image bytes if not using URL</param>
        /// <param name="context">Additional context (e.g., "This is a company logo")</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Analysis result with description and tags</returns>
        Task<ImageAnalysisResult> AnalyzeImageAsync(
            string imageUrl = null,
            byte[] imageBytes = null,
            string context = null,
            CancellationToken cancel = default);
    }

    public class ImageAnalysisResult
    {
        /// <summary>
        /// AI-generated description of the image content
        /// Example: "A modern geometric logo with blue and white colors, featuring abstract shapes"
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Content-based tags extracted from the image
        /// Examples: "logo", "geometric", "blue", "professional", "modern"
        /// Excludes generic tags like "image" or "file"
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Confidence score (0-1) of the analysis
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Additional metadata detected from the image
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
