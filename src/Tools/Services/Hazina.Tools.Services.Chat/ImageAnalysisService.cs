using Hazina.LLMs.OpenAI;
using Hazina.Tools.Services.Chat.Interfaces;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public class ImageAnalysisService : IImageAnalysisService
    {
        private readonly string _openAiApiKey;

        public ImageAnalysisService(string openAiApiKey)
        {
            _openAiApiKey = openAiApiKey ?? throw new ArgumentNullException(nameof(openAiApiKey));
        }

        public async Task<ImageAnalysisResult> AnalyzeImageAsync(
            string imageUrl = null,
            byte[] imageBytes = null,
            string context = null,
            CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) && (imageBytes == null || imageBytes.Length == 0))
            {
                throw new ArgumentException("Either imageUrl or imageBytes must be provided");
            }

            try
            {
                Console.WriteLine($"[ImageAnalysisService] Starting analysis...");
                Console.WriteLine($"[ImageAnalysisService] OpenAI API Key present: {!string.IsNullOrEmpty(_openAiApiKey)} (length: {_openAiApiKey?.Length ?? 0})");

                var config = new OpenAIConfig(_openAiApiKey);
                config.Model = "gpt-4o";  // GPT-4 Turbo with vision
                Console.WriteLine($"[ImageAnalysisService] Using model: {config.Model}");

                var client = new OpenAIClientWrapper(config);
                Console.WriteLine($"[ImageAnalysisService] OpenAI client created successfully");

                // Build analysis prompt
                var systemPrompt = @"You are an expert image analyst. Analyze the provided image and return ONLY a valid JSON object with this exact structure:
{
  ""description"": ""A detailed description of the image content, style, colors, and composition"",
  ""tags"": [""tag1"", ""tag2"", ""tag3""],
  ""metadata"": {
    ""primaryColors"": ""list of main colors"",
    ""style"": ""artistic style or type"",
    ""mood"": ""overall mood or tone""
  }
}

IMPORTANT RULES FOR TAGS:
- DO NOT include generic tags like 'image', 'picture', 'file', 'uploaded', 'photo' (unless it's specifically a photograph)
- DO include content-based tags: what is shown, style, colors, emotions, categories
- Tags should be specific and useful for searching: 'geometric-logo', 'professional', 'minimalist', 'blue-theme'
- Keep tags lowercase and hyphenated
- Maximum 10 tags
- Focus on what makes this image unique and searchable

DESCRIPTION GUIDELINES:
- Be specific and descriptive
- Include colors, style, composition
- Mention text if visible
- Describe the purpose or context if evident
- Keep it concise (1-3 sentences)

Return ONLY the JSON object, no additional text.";

                var userPrompt = string.IsNullOrWhiteSpace(context)
                    ? "Analyze this image and provide description, tags, and metadata."
                    : $"Analyze this image. Context: {context}\n\nProvide description, tags, and metadata.";

                // Create messages with image
                var messages = new List<HazinaChatMessage>
                {
                    new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.System,
                        Text = systemPrompt
                    },
                    new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.User,
                        Text = userPrompt
                    }
                };

                // Prepare image data
                List<ImageData> images = null;
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    images = new List<ImageData>
                    {
                        new ImageData
                        {
                            Name = "image_to_analyze",
                            BinaryData = BinaryData.FromBytes(imageBytes),
                            MimeType = "image/png"
                        }
                    };
                }
                else if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    // For URL-based images, download and convert to bytes
                    using var httpClient = new System.Net.Http.HttpClient();
                    var imageData = await httpClient.GetByteArrayAsync(imageUrl, cancel);
                    images = new List<ImageData>
                    {
                        new ImageData
                        {
                            Name = "image_to_analyze",
                            BinaryData = BinaryData.FromBytes(imageData),
                            MimeType = "image/png"
                        }
                    };
                }

                // Get analysis from GPT-4 Vision
                Console.WriteLine($"[ImageAnalysisService] Calling GPT-4 Vision API with {images?.Count ?? 0} images...");
                var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, images, cancel);
                Console.WriteLine($"[ImageAnalysisService] API call complete. Response success: {response != null}");
                var jsonResponse = response.Result?.Trim() ?? "{}";
                Console.WriteLine($"[ImageAnalysisService] Raw response (first 500 chars): {jsonResponse?.Substring(0, Math.Min(500, jsonResponse?.Length ?? 0))}");

                // Try to extract JSON if wrapped in markdown code blocks
                if (jsonResponse.StartsWith("```"))
                {
                    var lines = jsonResponse.Split('\n');
                    jsonResponse = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
                }

                // Parse JSON response
                var analysis = JsonSerializer.Deserialize<AnalysisResponse>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (analysis == null)
                {
                    throw new Exception("Failed to parse AI response");
                }

                return new ImageAnalysisResult
                {
                    Description = analysis.Description ?? string.Empty,
                    Tags = analysis.Tags ?? new List<string>(),
                    Metadata = analysis.Metadata ?? new Dictionary<string, string>(),
                    Confidence = 0.9  // GPT-4 Vision generally high confidence
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ImageAnalysisService] Error analyzing image: {ex.Message}");
                
                // Return fallback result on error
                return new ImageAnalysisResult
                {
                    Description = "Image analysis failed",
                    Tags = new List<string> { "unanalyzed" },
                    Confidence = 0.0
                };
            }
        }

        private class AnalysisResponse
        {
            public string Description { get; set; }
            public List<string> Tags { get; set; }
            public Dictionary<string, string> Metadata { get; set; }
        }
    }
}
