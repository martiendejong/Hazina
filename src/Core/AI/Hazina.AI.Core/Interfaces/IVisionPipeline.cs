namespace Hazina.AI.Core;

/// <summary>
/// Interface for vision processing pipelines.
/// Handles image and video encoding for downstream AI tasks.
/// </summary>
public interface IVisionPipeline : IDisposable
{
    /// <summary>
    /// Encodes an image into an embedding vector.
    /// </summary>
    /// <param name="imageData">Raw image bytes (PNG, JPEG, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Image embedding vector.</returns>
    Task<float[]> EncodeImageAsync(byte[] imageData, CancellationToken ct = default);

    /// <summary>
    /// Encodes an image from a file path.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Image embedding vector.</returns>
    Task<float[]> EncodeImageAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// Encodes a batch of images.
    /// </summary>
    /// <param name="images">Collection of image bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch of image embeddings.</returns>
    Task<float[][]> EncodeImagesAsync(IEnumerable<byte[]> images, CancellationToken ct = default);

    /// <summary>
    /// Encodes a video into an embedding vector.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Video embedding vector.</returns>
    Task<float[]> EncodeVideoAsync(string videoPath, CancellationToken ct = default);

    /// <summary>
    /// Encodes a video into per-segment embeddings.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="segmentDurationMs">Duration of each segment in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Embeddings for each video segment.</returns>
    Task<VideoSegmentEmbeddings> EncodeVideoSegmentsAsync(
        string videoPath,
        int segmentDurationMs = 5000,
        CancellationToken ct = default);

    /// <summary>
    /// Encodes raw video frames.
    /// </summary>
    /// <param name="frames">Collection of frame bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated frame embedding.</returns>
    Task<float[]> EncodeFramesAsync(IEnumerable<byte[]> frames, CancellationToken ct = default);
}

/// <summary>
/// Video encoder specifically for V-JEPA style models.
/// </summary>
public interface IVideoEncoder : IDisposable
{
    /// <summary>
    /// Extracts tubelets (spatio-temporal patches) from video frames.
    /// </summary>
    /// <param name="frames">Preprocessed video frames as tensors.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tubelet embeddings.</returns>
    Task<float[][]> ExtractTubeletsAsync(float[][][] frames, CancellationToken ct = default);

    /// <summary>
    /// Encodes video for world model predictions (action-conditioned).
    /// </summary>
    /// <param name="videoPath">Path to the video.</param>
    /// <param name="actions">Optional action sequence for action-conditioned encoding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>World model state representation.</returns>
    Task<WorldModelState> EncodeForPredictionAsync(
        string videoPath,
        float[][]? actions = null,
        CancellationToken ct = default);
}

/// <summary>
/// Embeddings for video segments with timing information.
/// </summary>
public record VideoSegmentEmbeddings
{
    /// <summary>
    /// Embedding vectors for each segment.
    /// </summary>
    public required float[][] Segments { get; init; }

    /// <summary>
    /// Start times for each segment in milliseconds.
    /// </summary>
    public required int[] StartTimesMs { get; init; }

    /// <summary>
    /// End times for each segment in milliseconds.
    /// </summary>
    public required int[] EndTimesMs { get; init; }

    /// <summary>
    /// Total video duration in milliseconds.
    /// </summary>
    public required int TotalDurationMs { get; init; }

    /// <summary>
    /// Aggregated embedding for the entire video.
    /// </summary>
    public float[]? GlobalEmbedding { get; init; }
}

/// <summary>
/// World model state for video prediction tasks.
/// </summary>
public record WorldModelState
{
    /// <summary>
    /// Latent state representation.
    /// </summary>
    public required float[] LatentState { get; init; }

    /// <summary>
    /// Predicted future states (if action-conditioned).
    /// </summary>
    public float[][]? PredictedFutures { get; init; }

    /// <summary>
    /// Confidence scores for predictions.
    /// </summary>
    public float[]? ConfidenceScores { get; init; }
}

/// <summary>
/// Configuration for vision pipelines.
/// </summary>
public record VisionPipelineConfig
{
    /// <summary>
    /// Path to the encoder model (ONNX format).
    /// </summary>
    public required string EncoderModelPath { get; init; }

    /// <summary>
    /// Target frame size (width, height).
    /// </summary>
    public (int Width, int Height) FrameSize { get; init; } = (224, 224);

    /// <summary>
    /// Number of frames per video clip for encoding.
    /// </summary>
    public int FramesPerClip { get; init; } = 16;

    /// <summary>
    /// Frames to sample per second from video.
    /// </summary>
    public float FrameSampleRate { get; init; } = 2.0f;

    /// <summary>
    /// Whether to normalize pixel values.
    /// </summary>
    public bool NormalizePixels { get; init; } = true;

    /// <summary>
    /// Mean values for normalization (RGB).
    /// </summary>
    public float[] NormalizationMean { get; init; } = new[] { 0.485f, 0.456f, 0.406f };

    /// <summary>
    /// Standard deviation values for normalization (RGB).
    /// </summary>
    public float[] NormalizationStd { get; init; } = new[] { 0.229f, 0.224f, 0.225f };

    /// <summary>
    /// Execution provider for ONNX inference.
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; init; } = ExecutionProvider.CPU;

    /// <summary>
    /// GPU device ID (if using CUDA).
    /// </summary>
    public int DeviceId { get; init; } = 0;
}

/// <summary>
/// Execution providers for model inference.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>CPU execution.</summary>
    CPU,
    /// <summary>NVIDIA CUDA.</summary>
    CUDA,
    /// <summary>DirectML (Windows).</summary>
    DirectML,
    /// <summary>NVIDIA TensorRT.</summary>
    TensorRT,
    /// <summary>AMD ROCm.</summary>
    ROCm,
    /// <summary>Apple CoreML.</summary>
    CoreML,
    /// <summary>OpenVINO.</summary>
    OpenVINO
}
