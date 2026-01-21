using Hazina.AI.Core;
using Hazina.AI.Inference.Configuration;
using Hazina.AI.Inference.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Hazina.AI.Vision.Pipelines;

/// <summary>
/// Vision pipeline for image and video encoding using ONNX models.
/// Supports various vision encoders including CLIP, V-JEPA, BLIP, etc.
/// </summary>
public class VisionPipeline : IVisionPipeline
{
    private readonly VisionPipelineConfig _config;
    private readonly ILogger<VisionPipeline> _logger;
    private readonly OnnxModel _encoder;
    private bool _disposed;

    private VisionPipeline(
        VisionPipelineConfig config,
        OnnxModel encoder,
        ILogger<VisionPipeline> logger)
    {
        _config = config;
        _encoder = encoder;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new VisionPipeline with the specified configuration.
    /// </summary>
    public static async Task<VisionPipeline> CreateAsync(
        VisionPipelineConfig config,
        ILogger<VisionPipeline>? logger = null,
        CancellationToken ct = default)
    {
        logger ??= NullLogger<VisionPipeline>.Instance;

        var onnxConfig = new OnnxConfig
        {
            ModelPath = config.EncoderModelPath,
            ExecutionProvider = config.ExecutionProvider,
            DeviceId = config.DeviceId
        };

        var encoder = await OnnxModel.LoadAsync(onnxConfig, ct: ct);
        encoder.WarmUp();

        return new VisionPipeline(config, encoder, logger);
    }

    /// <inheritdoc />
    public async Task<float[]> EncodeImageAsync(byte[] imageData, CancellationToken ct = default)
    {
        using var image = Image.Load<Rgb24>(imageData);
        var preprocessed = PreprocessImage(image);
        return await _encoder.InferAsync(preprocessed, ct);
    }

    /// <inheritdoc />
    public async Task<float[]> EncodeImageAsync(string imagePath, CancellationToken ct = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, ct);
        return await EncodeImageAsync(imageData, ct);
    }

    /// <inheritdoc />
    public async Task<float[][]> EncodeImagesAsync(IEnumerable<byte[]> images, CancellationToken ct = default)
    {
        var imageList = images.ToList();
        var preprocessed = new float[imageList.Count][];

        for (int i = 0; i < imageList.Count; i++)
        {
            using var image = Image.Load<Rgb24>(imageList[i]);
            preprocessed[i] = PreprocessImage(image);
        }

        return await _encoder.InferBatchAsync(preprocessed, ct);
    }

    /// <inheritdoc />
    public async Task<float[]> EncodeVideoAsync(string videoPath, CancellationToken ct = default)
    {
        var frames = await ExtractFramesAsync(videoPath, ct);
        var frameEmbeddings = await EncodeFramesAsync(frames, ct);

        // Average pooling over frame embeddings
        return AveragePoolEmbeddings(new[] { frameEmbeddings });
    }

    /// <inheritdoc />
    public async Task<VideoSegmentEmbeddings> EncodeVideoSegmentsAsync(
        string videoPath,
        int segmentDurationMs = 5000,
        CancellationToken ct = default)
    {
        var videoInfo = await GetVideoInfoAsync(videoPath, ct);
        var totalDurationMs = (int)(videoInfo.Duration.TotalMilliseconds);
        var numSegments = (totalDurationMs + segmentDurationMs - 1) / segmentDurationMs;

        var segmentEmbeddings = new List<float[]>();
        var startTimes = new List<int>();
        var endTimes = new List<int>();

        for (int i = 0; i < numSegments; i++)
        {
            ct.ThrowIfCancellationRequested();

            var startMs = i * segmentDurationMs;
            var endMs = Math.Min(startMs + segmentDurationMs, totalDurationMs);

            var frames = await ExtractFramesAsync(videoPath, startMs, endMs, ct);
            var embedding = await EncodeFramesAsync(frames, ct);

            segmentEmbeddings.Add(embedding);
            startTimes.Add(startMs);
            endTimes.Add(endMs);
        }

        // Compute global embedding
        var globalEmbedding = AveragePoolEmbeddings(segmentEmbeddings.ToArray());

        return new VideoSegmentEmbeddings
        {
            Segments = segmentEmbeddings.ToArray(),
            StartTimesMs = startTimes.ToArray(),
            EndTimesMs = endTimes.ToArray(),
            TotalDurationMs = totalDurationMs,
            GlobalEmbedding = globalEmbedding
        };
    }

    /// <inheritdoc />
    public async Task<float[]> EncodeFramesAsync(IEnumerable<byte[]> frames, CancellationToken ct = default)
    {
        var frameList = frames.ToList();

        if (frameList.Count == 0)
            return Array.Empty<float>();

        var embeddings = await EncodeImagesAsync(frameList, ct);

        // Average pooling over frames
        return AveragePoolEmbeddings(embeddings);
    }

    private float[] PreprocessImage(Image<Rgb24> image)
    {
        // Resize to target size
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(_config.FrameSize.Width, _config.FrameSize.Height),
            Mode = ResizeMode.Crop
        }));

        var width = _config.FrameSize.Width;
        var height = _config.FrameSize.Height;
        var pixels = new float[3 * height * width]; // CHW format

        // Extract and normalize pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];

                // Normalize to [0, 1] then apply mean/std normalization
                var r = (pixel.R / 255f - _config.NormalizationMean[0]) / _config.NormalizationStd[0];
                var g = (pixel.G / 255f - _config.NormalizationMean[1]) / _config.NormalizationStd[1];
                var b = (pixel.B / 255f - _config.NormalizationMean[2]) / _config.NormalizationStd[2];

                // CHW format
                pixels[0 * height * width + y * width + x] = r;
                pixels[1 * height * width + y * width + x] = g;
                pixels[2 * height * width + y * width + x] = b;
            }
        }

        return pixels;
    }

    private async Task<List<byte[]>> ExtractFramesAsync(string videoPath, CancellationToken ct)
    {
        var info = await GetVideoInfoAsync(videoPath, ct);
        return await ExtractFramesAsync(videoPath, 0, (int)info.Duration.TotalMilliseconds, ct);
    }

    private async Task<List<byte[]>> ExtractFramesAsync(
        string videoPath,
        int startMs,
        int endMs,
        CancellationToken ct)
    {
        var frames = new List<byte[]>();
        var durationSec = (endMs - startMs) / 1000.0;
        var frameCount = (int)Math.Min(_config.FramesPerClip, durationSec * _config.FrameSampleRate);

        // Use FFMpegCore to extract frames
        var startTime = TimeSpan.FromMilliseconds(startMs);

        var tempDir = Path.Combine(Path.GetTempPath(), $"vision_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            for (int i = 0; i < frameCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var frameTime = startTime + TimeSpan.FromSeconds(i / _config.FrameSampleRate);
                var tempFile = Path.Combine(tempDir, $"frame_{i:D4}.png");

                // Extract frame to temp file (parameters: input, output, size?, captureTime?)
                await FFMpegCore.FFMpeg.SnapshotAsync(
                    videoPath,
                    tempFile,
                    new System.Drawing.Size(_config.FrameSize.Width, _config.FrameSize.Height),
                    frameTime);

                // Read frame data
                var frameData = await File.ReadAllBytesAsync(tempFile, ct);
                frames.Add(frameData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract some frames from {VideoPath}", videoPath);
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }

        return frames;
    }

    private static async Task<VideoInfo> GetVideoInfoAsync(string videoPath, CancellationToken ct)
    {
        var analysis = await FFMpegCore.FFProbe.AnalyseAsync(videoPath);
        return new VideoInfo
        {
            Duration = analysis.Duration,
            Width = analysis.PrimaryVideoStream?.Width ?? 0,
            Height = analysis.PrimaryVideoStream?.Height ?? 0,
            FrameRate = analysis.PrimaryVideoStream?.FrameRate ?? 30
        };
    }

    private static float[] AveragePoolEmbeddings(float[][] embeddings)
    {
        if (embeddings.Length == 0)
            return Array.Empty<float>();

        var embeddingDim = embeddings[0].Length;
        var result = new float[embeddingDim];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < embeddingDim; i++)
            {
                result[i] += embedding[i];
            }
        }

        for (int i = 0; i < embeddingDim; i++)
        {
            result[i] /= embeddings.Length;
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _encoder.Dispose();
        _disposed = true;
    }

    private record VideoInfo
    {
        public TimeSpan Duration { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public double FrameRate { get; init; }
    }
}
