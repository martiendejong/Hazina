using Hazina.Tools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatImageService
    {
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);

        /// <summary>
        /// Generates an image from a prompt and returns the raw bytes.
        /// Used by LayeredImageService for generating individual layers.
        /// </summary>
        /// <param name="prompt">The image generation prompt.</param>
        /// <param name="imageModel">The image model to use.</param>
        /// <param name="width">Desired width (will be mapped to closest supported size).</param>
        /// <param name="height">Desired height (will be mapped to closest supported size).</param>
        /// <param name="cancel">Cancellation token.</param>
        /// <returns>PNG image bytes.</returns>
        Task<byte[]> GenerateImageBytesAsync(string prompt, ImageModel imageModel, int width, int height, CancellationToken cancel);

        /// <summary>
        /// Generates an image from a prompt with context images for reference.
        /// Used by LayeredImageService for sequential layer generation where each layer can see previous layers.
        /// </summary>
        /// <param name="prompt">The image generation prompt.</param>
        /// <param name="imageModel">The image model to use.</param>
        /// <param name="width">Desired width (will be mapped to closest supported size).</param>
        /// <param name="height">Desired height (will be mapped to closest supported size).</param>
        /// <param name="contextImages">Optional list of context images (base64 encoded) for the model to reference.</param>
        /// <param name="contextDescriptions">Optional descriptions of the context (other layers, canvas info, etc).</param>
        /// <param name="cancel">Cancellation token.</param>
        /// <returns>PNG image bytes.</returns>
        Task<byte[]> GenerateImageBytesWithContextAsync(
            string prompt,
            ImageModel imageModel,
            int width,
            int height,
            List<byte[]>? contextImages,
            string? contextDescriptions,
            CancellationToken cancel);
    }
}
