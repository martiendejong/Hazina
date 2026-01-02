using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.FileOps.Helpers;
using Mscc.GenerativeAI;
using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Hazina.Tools.Services.Chat
{
    public class ChatImageService : ChatServiceBase, IChatImageService
    {
        private readonly string _openAiApiKey;
        private readonly string _geminiApiKey;
        private readonly IGeneratedImageRepository _generatedImageRepository;
        private readonly IChatMessageService _messageService;
        private readonly IChatMetadataService _metadataService;
        private static readonly HttpClient HttpClient = new();

        public ChatImageService(
            ProjectsRepository projects,
            ProjectFileLocator fileLocator,
            GeneratorAgentBase agent,
            IntakeRepository intake,
            IGeneratedImageRepository generatedImageRepository,
            IChatMetadataService metadataService,
            IChatMessageService messageService)
            : this(
                  projects,
                  fileLocator,
                  intake,
                  agent?.Config?.ApiSettings?.OpenApiKey,
                  agent?.Config?.ApiSettings?.GeminiApiKey,
                  generatedImageRepository,
                  metadataService,
                  messageService)
        {
        }

        public ChatImageService(
            ProjectsRepository projects,
            ProjectFileLocator fileLocator,
            IntakeRepository intake,
            string? openAiApiKey,
            string? geminiApiKey,
            IGeneratedImageRepository generatedImageRepository,
            IChatMetadataService metadataService,
            IChatMessageService messageService)
            : base(projects, fileLocator)
        {
            _openAiApiKey = openAiApiKey ?? string.Empty;
            _geminiApiKey = geminiApiKey ?? string.Empty;
            _generatedImageRepository = generatedImageRepository ?? throw new ArgumentNullException(nameof(generatedImageRepository));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        public Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet)
            => GenerateImageInternal(projectId, chatId, project, chatMessage, cancel, null, isImageSet);

        public Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet)
            => GenerateImageInternal(projectId, chatId, project, chatMessage, cancel, userId, isImageSet);

        // Backward compatible overloads (default to non-image-set)
        public Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel)
            => GenerateImageInternal(projectId, chatId, project, chatMessage, cancel, null, false);

        public Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel)
            => GenerateImageInternal(projectId, chatId, project, chatMessage, cancel, userId, false);

        private async Task<ChatConversation> GenerateImageInternal(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, string? userId, bool isImageSet)
        {
            var prompt = chatMessage?.Message ?? string.Empty;
            // If generating an image set, diversify prompts to encourage variation
            var imageModel = project?.ImageModel ?? ImageModel.GptImage;
            string imageSource;
            string modelInfo;

            try
            {
                // For image sets, generate the first prompt now; additional prompts handled below
                switch (imageModel)
                {
                    case ImageModel.GptImage:
                        imageSource = await GenerateOpenAIImage(prompt, imageModel, cancel);
                        modelInfo = "GPT Image (gpt-image-1)";
                        break;

                    case ImageModel.DallE3:
                    case ImageModel.DallE2:
                        imageSource = await GenerateOpenAIImage(prompt, imageModel, cancel);
                        modelInfo = imageModel == ImageModel.DallE3 ? "DALL-E 3" : "DALL-E 2";
                        break;

                    case ImageModel.NanoBanana:
                        imageSource = await GenerateNanoBananaImage(prompt, cancel);
                        modelInfo = "Nano Banana (Gemini 2.5 Flash Image)";
                        break;

                    default:
                        throw new NotSupportedException($"Image model {imageModel} is not supported");
                }

                var resolved = await ResolveImageDataAsync(imageSource, cancel);
                resolved = TryMakeLogoBackgroundTransparent(resolved, prompt, isImageSet);
                var extension = DetermineExtension(resolved.ContentType, resolved.SourceUrl);
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var storedUserId = string.IsNullOrWhiteSpace(userId) ? null : userId;

                Console.WriteLine($"ChatImageService: projectId={projectId}, userId={userId}, storedUserId={storedUserId}, fileName={fileName}");

                await _generatedImageRepository.SaveImageAsync(projectId, storedUserId, fileName, resolved.Data);

                // Also persist to uploads so it shows as an uploaded document and can be selected in chat input
                try
                {
                    var uploadsFolder = Path.Combine(FileLocator.GetProjectFolder(projectId), "uploads");
                    FileHelper.EnsureDirectoryExists(uploadsFolder);
                    var uploadPath = Path.Combine(uploadsFolder, fileName);
                    await File.WriteAllBytesAsync(uploadPath, resolved.Data);

                    var listFilePath = Path.Combine(FileLocator.GetProjectFolder(projectId), "uploadedFiles.json");
                    var uploadedFile = FileHelper.GetUploadedFileDetails(uploadPath, fileName, 0);
                    await FileHelper.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ChatImageService: Failed to sync generated image to uploads: {ex.Message}");
                }

                var metadata = new GeneratedImageInfo
                {
                    ProjectId = projectId,
                    ChatId = chatId,
                    FileName = fileName,
                    Prompt = prompt,
                    Model = modelInfo,
                    SourceUrl = resolved.SourceUrl ?? string.Empty,
                    UserId = storedUserId ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    Tags = DetermineImageTags(prompt)
                };
                _generatedImageRepository.Add(metadata, projectId, storedUserId);

                var imageUrl = BuildImageUrl(projectId, fileName, storedUserId);

                Console.WriteLine($"ChatImageService: Generated imageUrl={imageUrl}");

                // For image sets (background generation), don't add prompts to chat - they're internal only
                // For regular chat image generation, add user/assistant messages as before
                SerializableList<ConversationMessage> chatMessages;

                if (isImageSet)
                {
                    // Background image set generation - don't pollute the chat with prompts
                    // Just create a minimal response for the caller without persisting to chat
                    chatMessages = new SerializableList<ConversationMessage>
                    {
                        new ConversationMessage
                        {
                            Role = ChatMessageRole.Assistant,
                            Text = $"![Generated Image]({imageUrl})",
                            Payload = new
                            {
                                type = "image-set",
                                url = imageUrl,
                                fileName,
                                model = modelInfo
                            }
                        }
                    };
                    // Do NOT persist to chat - this is background generation
                }
                else
                {
                    // Regular chat image generation - add to conversation history
                    chatMessages = _messageService.GetChatMessages(projectId, chatId, userId);
                    var userMessage = new ConversationMessage
                    {
                        Role = ChatMessageRole.User,
                        Text = prompt
                    };
                    chatMessages.Add(userMessage);

                    var assistantMessage = new ConversationMessage
                    {
                        Role = ChatMessageRole.Assistant,
                        Text = $"![Generated Image]({imageUrl})\n\n*Generated with {modelInfo}*"
                    };
                    chatMessages.Add(assistantMessage);

                    // Persist messages to chat file only for regular image generation
                    _messageService.StoreChatMessages(projectId, chatId, chatMessages, userId);
                }

                // Get or create metadata
                var chatMetadata = string.IsNullOrWhiteSpace(userId)
                    ? _metadataService.GetChatMetaData(projectId, chatId)
                    : _metadataService.GetChatMetaDataUser(projectId, chatId, userId);

                if (chatMetadata == null)
                {
                    chatMetadata = new ChatMetadata
                    {
                        Id = chatId,
                        Name = "Image",
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        IsPinned = false,
                        ProjectId = projectId
                    };
                }
                else
                {
                    chatMetadata.Modified = DateTime.UtcNow;
                    chatMetadata.LastUpdated = DateTime.UtcNow;
                }

                var convo = new ChatConversation
                {
                    MetaData = chatMetadata,
                    ChatMessages = chatMessages
                };

                return convo;
            }
            catch (Exception ex)
            {
                // Create error message without echoing prompt
                var errorMessage = new ConversationMessage { Role = ChatMessageRole.Assistant, Text = $"Error generating image: {ex.Message}" };

                // Get existing messages and append new ones
                var chatMessages = _messageService.GetChatMessages(projectId, chatId, userId);
                chatMessages.Add(errorMessage);

                // Persist error messages to chat file
                _messageService.StoreChatMessages(projectId, chatId, chatMessages, userId);

                // Get or create metadata
                var chatMetadata = string.IsNullOrWhiteSpace(userId)
                    ? _metadataService.GetChatMetaData(projectId, chatId)
                    : _metadataService.GetChatMetaDataUser(projectId, chatId, userId);

                if (chatMetadata == null)
                {
                    chatMetadata = new ChatMetadata
                    {
                        Id = chatId,
                        Name = "Image",
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        IsPinned = false,
                        ProjectId = projectId
                    };
                }
                else
                {
                    chatMetadata.Modified = DateTime.UtcNow;
                    chatMetadata.LastUpdated = DateTime.UtcNow;
                }

                var convo = new ChatConversation
                {
                    MetaData = chatMetadata,
                    ChatMessages = chatMessages
                };
                return convo;
            }
        }

        protected virtual async Task<string> GenerateOpenAIImage(string prompt, ImageModel model, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API key not configured.");
            }

            var config = new OpenAIConfig(_openAiApiKey);
            config.ImageModel = model switch
            {
                ImageModel.GptImage => "gpt-image-1",
                ImageModel.DallE3 => "dall-e-3",
                ImageModel.DallE2 => "dall-e-2",
                _ => "gpt-image-1" // Default to GPT Image
            };

            var client = new OpenAIClientWrapper(config);
            var result = await client.GetImage(prompt, null, null, null, cancel);

            if (result?.Result != null)
            {
                // GPT-Image returns ImageBytes, DALL-E returns URL
                // Check both and return appropriate format
                if (result.Result.Url != null)
                {
                    return result.Result.Url.ToString();
                }

                if (result.Result.ImageBytes != null)
                {
                    // Convert ImageBytes to data URI for processing
                    var bytes = result.Result.ImageBytes.ToArray();
                    var base64 = Convert.ToBase64String(bytes);
                    return $"data:image/png;base64,{base64}";
                }

                throw new Exception("No image URL or bytes returned from OpenAI");
            }

            throw new Exception("Failed to generate image with OpenAI");
        }

        protected virtual async Task<string> GenerateNanoBananaImage(string prompt, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(_geminiApiKey))
            {
                throw new InvalidOperationException("Gemini API key not configured. Please add GeminiApiKey to your config.");
            }

            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(model: Model.Gemini25FlashImage);

            var response = await model.GenerateContent(prompt);

            if (response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault() is Part imagePart &&
                imagePart.InlineData != null)
            {
                var imageBytes = Convert.FromBase64String(imagePart.InlineData.Data);
                var mimeType = imagePart.InlineData.MimeType;

                var base64Image = Convert.ToBase64String(imageBytes);
                return $"data:{mimeType};base64,{base64Image}";
            }

            throw new Exception("Failed to generate image with Nano Banana (Gemini 2.5 Flash Image)");
        }

        private static string BuildImageUrl(string projectId, string fileName, string? userId)
        {
            // Expose generated images via the uploaded documents endpoint so they behave like regular uploads
            return $"/api/uploadeddocuments/file/{Uri.EscapeDataString(projectId)}/{Uri.EscapeDataString(fileName)}";
        }

        private static string DetermineExtension(string? contentType, string? sourceUrl)
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var uri = new Uri(sourceUrl);
                    var extension = Path.GetExtension(uri.AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(extension))
                    {
                        return extension;
                    }
                }
                catch
                {
                    // Ignore invalid URI
                }
            }

            return contentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/svg+xml" => ".svg",
                _ => ".png",
            };
        }

        private static async Task<ResolvedImage> ResolveImageDataAsync(string source, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new InvalidOperationException("Image source is empty.");
            }

            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = source.IndexOf(',');
                if (commaIndex < 0)
                {
                    throw new FormatException("Invalid data URI for generated image");
                }

                var meta = source[5..commaIndex];
                var isBase64 = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
                var mediaType = isBase64 ? meta[..^7] : meta;
                var payload = source[(commaIndex + 1)..];
                var bytes = Convert.FromBase64String(payload);
                return new ResolvedImage(bytes, string.IsNullOrWhiteSpace(mediaType) ? "image/png" : mediaType, null);
            }

            using var response = await HttpClient.GetAsync(source, cancel);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsByteArrayAsync(cancel);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return new ResolvedImage(data, contentType, source);
        }

        private static ResolvedImage TryMakeLogoBackgroundTransparent(ResolvedImage resolved, string prompt, bool isImageSet)
        {
            if (!isImageSet || string.IsNullOrWhiteSpace(prompt) || !prompt.Contains("logo", StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }

            try
            {
                // Use fully qualified type to avoid ambiguity with Mscc.GenerativeAI.Image
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(resolved.Data);
                if (HasTransparency(image))
                {
                    return resolved;
                }

                var background = GetCornerBackgroundColor(image);
                var threshold = 30;

                // Use ImageSharp v3 API: ProcessPixelRows
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (var x = 0; x < row.Length; x++)
                        {
                            var pixel = row[x];
                            if (IsCloseToBackground(pixel, background, threshold))
                            {
                                row[x] = new Rgba32(pixel.R, pixel.G, pixel.B, 0);
                            }
                        }
                    }
                });

                // Validate that transparency was actually achieved
                if (!HasTransparency(image))
                {
                    // Log failure - transparency processing didn't produce transparent pixels
                    Console.WriteLine($"[Logo Transparency] Warning: Processing failed to create transparency. Background color: R={background.R}, G={background.G}, B={background.B}. Returning original image.");
                    return resolved;
                }

                // Count transparent pixels to ensure sufficient transparency
                var transparentPixelCount = 0;
                var totalPixels = image.Width * image.Height;
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (var x = 0; x < row.Length; x++)
                        {
                            if (row[x].A == 0)
                                transparentPixelCount++;
                        }
                    }
                });

                var transparencyPercentage = (transparentPixelCount * 100.0) / totalPixels;

                // If less than 5% of pixels are transparent, the processing likely failed
                if (transparencyPercentage < 5.0)
                {
                    Console.WriteLine($"[Logo Transparency] Warning: Only {transparencyPercentage:F2}% of pixels are transparent. Processing may have failed. Returning original image.");
                    return resolved;
                }

                Console.WriteLine($"[Logo Transparency] Success: {transparencyPercentage:F2}% of pixels are transparent. Background removed successfully.");

                using var output = new MemoryStream();
                image.SaveAsPng(output);
                return new ResolvedImage(output.ToArray(), "image/png", resolved.SourceUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logo Transparency] Error during transparency processing: {ex.Message}. Returning original image.");
                return resolved;
            }
        }

        private static bool HasTransparency(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            var hasAlpha = false;
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        if (row[x].A < 255)
                        {
                            hasAlpha = true;
                            return; // Exit early
                        }
                    }
                }
            });
            return hasAlpha;
        }

        private static Rgba32 GetCornerBackgroundColor(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            var corners = new[]
            {
                image[0, 0],
                image[image.Width - 1, 0],
                image[0, image.Height - 1],
                image[image.Width - 1, image.Height - 1]
            };

            return corners
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        private static bool IsCloseToBackground(Rgba32 pixel, Rgba32 background, int threshold)
        {
            var dr = pixel.R - background.R;
            var dg = pixel.G - background.G;
            var db = pixel.B - background.B;
            var distance = Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);
            return distance <= threshold;
        }

        /// <summary>
        /// Determine appropriate tags for a generated image based on the prompt
        /// </summary>
        private static List<string> DetermineImageTags(string prompt)
        {
            var tags = new List<string> { "image", "generated" };

            if (string.IsNullOrWhiteSpace(prompt))
                return tags;

            var lowerPrompt = prompt.ToLowerInvariant();

            // Detect specific image types from prompt keywords
            if (lowerPrompt.Contains("logo"))
                tags.Add("logo");

            if (lowerPrompt.Contains("banner") || lowerPrompt.Contains("header"))
                tags.Add("banner");

            if (lowerPrompt.Contains("icon"))
                tags.Add("icon");

            if (lowerPrompt.Contains("illustration") || lowerPrompt.Contains("drawing"))
                tags.Add("illustration");

            if (lowerPrompt.Contains("photo") || lowerPrompt.Contains("photograph") || lowerPrompt.Contains("realistic"))
                tags.Add("photo");

            if (lowerPrompt.Contains("diagram") || lowerPrompt.Contains("chart") || lowerPrompt.Contains("infographic"))
                tags.Add("diagram");

            if (lowerPrompt.Contains("background") || lowerPrompt.Contains("wallpaper"))
                tags.Add("background");

            if (lowerPrompt.Contains("product") || lowerPrompt.Contains("mockup"))
                tags.Add("product");

            if (lowerPrompt.Contains("social media") || lowerPrompt.Contains("instagram") ||
                lowerPrompt.Contains("facebook") || lowerPrompt.Contains("twitter"))
                tags.Add("social-media");

            return tags.Distinct().ToList();
        }

        private readonly struct ResolvedImage
        {
            public byte[] Data { get; }
            public string ContentType { get; }
            public string? SourceUrl { get; }

            public ResolvedImage(byte[] data, string contentType, string? sourceUrl)
            {
                Data = data;
                ContentType = contentType;
                SourceUrl = sourceUrl;
            }
        }
    }
}
