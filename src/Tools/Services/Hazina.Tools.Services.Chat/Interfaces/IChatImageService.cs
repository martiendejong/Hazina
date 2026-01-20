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
    }
}
