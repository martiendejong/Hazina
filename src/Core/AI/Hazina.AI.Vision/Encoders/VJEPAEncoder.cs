using Hazina.AI.Core;
using Hazina.AI.Inference.Configuration;
using Hazina.AI.Inference.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Hazina.AI.Vision.Encoders;

/// <summary>
/// V-JEPA 2 video encoder for world model representations.
/// Uses Meta's V-JEPA architecture exported to ONNX for video understanding.
/// </summary>
/// <remarks>
/// V-JEPA (Video Joint Embedding Predictive Architecture) learns visual representations
/// by predicting masked video regions in latent space. V-JEPA 2 achieves SOTA on video
/// understanding benchmarks and enables zero-shot robotic planning.
///
/// To use this encoder:
/// 1. Export V-JEPA 2 model to ONNX from PyTorch:
///    torch.onnx.export(model, dummy_input, "v-jepa-2.onnx", ...)
/// 2. Download from Meta's model hub (when available in ONNX format)
/// </remarks>
public class VJEPAEncoder : IVideoEncoder
{
    private readonly VJEPAConfig _config;
    private readonly ILogger<VJEPAEncoder> _logger;
    private readonly OnnxModel _encoder;
    private readonly OnnxModel? _predictor;
    private bool _disposed;

    /// <summary>
    /// Embedding dimension of the encoder output.
    /// </summary>
    public int EmbeddingDim => _config.EmbeddingDim;

    /// <summary>
    /// Number of frames per video clip.
    /// </summary>
    public int FramesPerClip => _config.FramesPerClip;

    private VJEPAEncoder(
        VJEPAConfig config,
        OnnxModel encoder,
        OnnxModel? predictor,
        ILogger<VJEPAEncoder> logger)
    {
        _config = config;
        _encoder = encoder;
        _predictor = predictor;
        _logger = logger;
    }

    /// <summary>
    /// Creates a V-JEPA encoder from ONNX model files.
    /// </summary>
    /// <param name="config">Encoder configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<VJEPAEncoder> CreateAsync(
        VJEPAConfig config,
        ILogger<VJEPAEncoder>? logger = null,
        CancellationToken ct = default)
    {
        logger ??= NullLogger<VJEPAEncoder>.Instance;
        logger.LogInformation("Loading V-JEPA encoder from {Path}", config.EncoderModelPath);

        var onnxConfig = new OnnxConfig
        {
            ModelPath = config.EncoderModelPath,
            ExecutionProvider = config.ExecutionProvider,
            DeviceId = config.DeviceId
        };

        var encoder = await OnnxModel.LoadAsync(onnxConfig, ct: ct);

        OnnxModel? predictor = null;
        if (!string.IsNullOrEmpty(config.PredictorModelPath) && File.Exists(config.PredictorModelPath))
        {
            logger.LogInformation("Loading V-JEPA predictor from {Path}", config.PredictorModelPath);
            var predictorConfig = new OnnxConfig
            {
                ModelPath = config.PredictorModelPath,
                ExecutionProvider = config.ExecutionProvider,
                DeviceId = config.DeviceId
            };
            predictor = await OnnxModel.LoadAsync(predictorConfig, ct: ct);
        }

        return new VJEPAEncoder(config, encoder, predictor, logger);
    }

    /// <inheritdoc />
    public async Task<float[][]> ExtractTubeletsAsync(float[][][] frames, CancellationToken ct = default)
    {
        // V-JEPA uses tubelet (spatio-temporal patch) extraction
        // Each tubelet is a 3D patch of size (tubelet_size, patch_size, patch_size)

        var numFrames = frames.Length;
        var height = (int)Math.Sqrt(frames[0][0].Length / 3); // Assuming CHW format
        var width = height;

        var tubeletSize = _config.TubeletSize;
        var patchSize = _config.PatchSize;

        var numTubeletsT = numFrames / tubeletSize;
        var numTubeletsH = height / patchSize;
        var numTubeletsW = width / patchSize;

        var tubelets = new List<float[]>();

        for (int t = 0; t < numTubeletsT; t++)
        {
            for (int h = 0; h < numTubeletsH; h++)
            {
                for (int w = 0; w < numTubeletsW; w++)
                {
                    var tubelet = ExtractTubelet(frames, t, h, w, tubeletSize, patchSize);
                    tubelets.Add(tubelet);
                }
            }
        }

        // Encode tubelets through the encoder
        var tubeletArray = tubelets.ToArray();
        return await _encoder.InferBatchAsync(tubeletArray, ct);
    }

    /// <inheritdoc />
    public async Task<WorldModelState> EncodeForPredictionAsync(
        string videoPath,
        float[][]? actions = null,
        CancellationToken ct = default)
    {
        // Extract and preprocess frames
        var frames = await ExtractAndPreprocessFramesAsync(videoPath, ct);

        // Get encoder representations
        var flatFrames = FlattenFrames(frames);
        var encoderOutput = await _encoder.InferAsync(flatFrames, ct);

        // If predictor is available. create world model predictions
        float[][]? predictions = null;
        float[]? confidences = null;

        if (_predictor != null && actions != null)
        {
            // Prepare action-conditioned input
            var predictorInput = new Dictionary<string, float[]>
            {
                ["encoder_output"] = encoderOutput,
                ["actions"] = FlattenActions(actions)
            };

            var predOutput = await _predictor.InferNamedAsync(predictorInput, ct);

            if (predOutput.TryGetValue("predictions", out var preds))
            {
                predictions = UnflattenPredictions(preds, actions.Length);
            }

            if (predOutput.TryGetValue("confidence", out var conf))
            {
                confidences = conf;
            }
        }

        return new WorldModelState
        {
            LatentState = encoderOutput,
            PredictedFutures = predictions,
            ConfidenceScores = confidences
        };
    }

    private async Task<float[][][]> ExtractAndPreprocessFramesAsync(string videoPath, CancellationToken ct)
    {
        var frames = new List<float[][]>();

        // Use FFMpegCore to extract frames
        var analysis = await FFMpegCore.FFProbe.AnalyseAsync(videoPath);
        var duration = analysis.Duration;
        var frameInterval = duration.TotalSeconds / _config.FramesPerClip;

        var tempDir = Path.Combine(Path.GetTempPath(), $"vjepa_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            for (int i = 0; i < _config.FramesPerClip; i++)
            {
                ct.ThrowIfCancellationRequested();

                var frameTime = TimeSpan.FromSeconds(i * frameInterval);
                var tempFile = Path.Combine(tempDir, $"frame_{i:D4}.png");

                // Extract frame to temp file (parameters: input, output, size?, captureTime?)
                await FFMpegCore.FFMpeg.SnapshotAsync(
                    videoPath,
                    tempFile,
                    new System.Drawing.Size(_config.FrameSize, _config.FrameSize),
                    frameTime);

                // Load and preprocess
                using var image = await Image.LoadAsync<Rgb24>(tempFile, ct);
                var preprocessed = PreprocessFrame(image);
                frames.Add(new[] { preprocessed });
            }
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore cleanup errors */ }
            }
        }

        return frames.ToArray();
    }

    private float[] PreprocessFrame(Image<Rgb24> image)
    {
        // Resize to target size
        image.Mutate(x => x.Resize(_config.FrameSize, _config.FrameSize));

        var size = _config.FrameSize;
        var pixels = new float[3 * size * size];

        // ImageNet normalization (as used by V-JEPA)
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var pixel = image[x, y];

                pixels[0 * size * size + y * size + x] = (pixel.R / 255f - mean[0]) / std[0];
                pixels[1 * size * size + y * size + x] = (pixel.G / 255f - mean[1]) / std[1];
                pixels[2 * size * size + y * size + x] = (pixel.B / 255f - mean[2]) / std[2];
            }
        }

        return pixels;
    }

    private static float[] ExtractTubelet(
        float[][][] frames,
        int tIdx, int hIdx, int wIdx,
        int tubeletSize, int patchSize)
    {
        var tubelet = new List<float>();

        for (int t = 0; t < tubeletSize; t++)
        {
            var frameIdx = tIdx * tubeletSize + t;
            if (frameIdx >= frames.Length) break;

            var frame = frames[frameIdx][0]; // Assuming single channel array per frame
            var height = (int)Math.Sqrt(frame.Length / 3);

            for (int c = 0; c < 3; c++)
            {
                for (int h = 0; h < patchSize; h++)
                {
                    for (int w = 0; w < patchSize; w++)
                    {
                        var y = hIdx * patchSize + h;
                        var x = wIdx * patchSize + w;
                        var idx = c * height * height + y * height + x;

                        if (idx < frame.Length)
                            tubelet.Add(frame[idx]);
                    }
                }
            }
        }

        return tubelet.ToArray();
    }

    private static float[] FlattenFrames(float[][][] frames)
    {
        var result = new List<float>();
        foreach (var frame in frames)
        {
            foreach (var channel in frame)
            {
                result.AddRange(channel);
            }
        }
        return result.ToArray();
    }

    private static float[] FlattenActions(float[][] actions)
    {
        var result = new List<float>();
        foreach (var action in actions)
        {
            result.AddRange(action);
        }
        return result.ToArray();
    }

    private static float[][] UnflattenPredictions(float[] flat, int numPredictions)
    {
        var predSize = flat.Length / numPredictions;
        var result = new float[numPredictions][];

        for (int i = 0; i < numPredictions; i++)
        {
            result[i] = new float[predSize];
            Array.Copy(flat, i * predSize, result[i], 0, predSize);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _encoder.Dispose();
        _predictor?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Configuration for V-JEPA encoder.
/// </summary>
public record VJEPAConfig
{
    /// <summary>
    /// Path to the V-JEPA encoder ONNX model.
    /// </summary>
    public required string EncoderModelPath { get; init; }

    /// <summary>
    /// Path to the V-JEPA predictor ONNX model (optional, for world model predictions).
    /// </summary>
    public string? PredictorModelPath { get; init; }

    /// <summary>
    /// Frame size (both width and height).
    /// </summary>
    public int FrameSize { get; init; } = 224;

    /// <summary>
    /// Number of frames per video clip.
    /// </summary>
    public int FramesPerClip { get; init; } = 16;

    /// <summary>
    /// Embedding dimension of the encoder output.
    /// </summary>
    public int EmbeddingDim { get; init; } = 1280; // V-JEPA 2 uses ViT-H

    /// <summary>
    /// Temporal size of tubelets.
    /// </summary>
    public int TubeletSize { get; init; } = 2;

    /// <summary>
    /// Spatial size of patches.
    /// </summary>
    public int PatchSize { get; init; } = 14;

    /// <summary>
    /// Execution provider for ONNX inference.
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; init; } = ExecutionProvider.CPU;

    /// <summary>
    /// GPU device ID.
    /// </summary>
    public int DeviceId { get; init; } = 0;
}
