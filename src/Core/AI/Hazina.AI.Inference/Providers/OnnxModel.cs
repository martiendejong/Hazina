using Hazina.AI.Core;
using Hazina.AI.Inference.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Hazina.AI.Inference.Providers;

/// <summary>
/// ONNX model wrapper implementing IModelInference.
/// Provides GPU-accelerated inference for any ONNX model.
/// </summary>
public sealed class OnnxModel : IModelInference
{
    private readonly OnnxConfig _config;
    private readonly ILogger<OnnxModel> _logger;
    private readonly InferenceSession _session;
    private bool _disposed;

    /// <inheritdoc />
    public Core.ModelMetadata Metadata { get; }

    /// <summary>
    /// Input node names for the model.
    /// </summary>
    public IReadOnlyList<string> InputNames { get; }

    /// <summary>
    /// Output node names for the model.
    /// </summary>
    public IReadOnlyList<string> OutputNames { get; }

    private OnnxModel(
        OnnxConfig config,
        InferenceSession session,
        ILogger<OnnxModel> logger)
    {
        _config = config;
        _session = session;
        _logger = logger;

        InputNames = session.InputNames.ToList();
        OutputNames = session.OutputNames.ToList();

        Metadata = BuildMetadata(config, session);
    }

    /// <summary>
    /// Loads an ONNX model asynchronously.
    /// </summary>
    /// <param name="config">Model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Loaded ONNX model.</returns>
    public static async Task<OnnxModel> LoadAsync(
        OnnxConfig config,
        ILogger<OnnxModel>? logger = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Load(config, logger), ct);
    }

    /// <summary>
    /// Loads an ONNX model synchronously.
    /// </summary>
    public static OnnxModel Load(OnnxConfig config, ILogger<OnnxModel>? logger = null)
    {
        logger ??= NullLogger<OnnxModel>.Instance;

        if (!File.Exists(config.ModelPath))
            throw new FileNotFoundException($"Model file not found: {config.ModelPath}");

        logger.LogInformation("Loading ONNX model from {ModelPath}", config.ModelPath);

        var sessionOptions = CreateSessionOptions(config);
        var session = new InferenceSession(config.ModelPath, sessionOptions);

        logger.LogInformation(
            "Model loaded. Inputs: {Inputs}, Outputs: {Outputs}",
            string.Join(", ", session.InputNames),
            string.Join(", ", session.OutputNames));

        return new OnnxModel(config, session, logger);
    }

    /// <summary>
    /// Convenience method to load model from path with default settings.
    /// </summary>
    public static Task<OnnxModel> LoadAsync(string modelPath, CancellationToken ct = default)
    {
        return LoadAsync(new OnnxConfig { ModelPath = modelPath }, ct: ct);
    }

    /// <inheritdoc />
    public Task<float[]> InferAsync(float[] input, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var inputMeta = _session.InputMetadata.First();
            var shape = inputMeta.Value.Dimensions.Select(d => d == -1 ? 1 : (int)d).ToArray();

            // Ensure input matches expected size
            var expectedSize = shape.Aggregate(1, (a, b) => a * b);
            if (input.Length != expectedSize)
            {
                // Try to infer batch dimension
                shape[0] = input.Length / shape.Skip(1).Aggregate(1, (a, b) => a * b);
            }

            var tensor = new DenseTensor<float>(input, shape);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMeta.Key, tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();

            return output;
        }, ct);
    }

    /// <inheritdoc />
    public Task<float[][]> InferBatchAsync(float[][] inputs, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var batchSize = inputs.Length;
            var inputSize = inputs[0].Length;

            // Flatten batch
            var flatInput = new float[batchSize * inputSize];
            for (int i = 0; i < batchSize; i++)
            {
                Array.Copy(inputs[i], 0, flatInput, i * inputSize, inputSize);
            }

            var inputMeta = _session.InputMetadata.First();
            var shape = inputMeta.Value.Dimensions.Select(d => d == -1 ? 1 : (int)d).ToArray();
            shape[0] = batchSize;

            var tensor = new DenseTensor<float>(flatInput, shape);
            var onnxInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMeta.Key, tensor)
            };

            using var results = _session.Run(onnxInputs);
            var output = results.First().AsTensor<float>().ToArray();

            // Unflatten output
            var outputSize = output.Length / batchSize;
            var batchOutput = new float[batchSize][];
            for (int i = 0; i < batchSize; i++)
            {
                batchOutput[i] = new float[outputSize];
                Array.Copy(output, i * outputSize, batchOutput[i], 0, outputSize);
            }

            return batchOutput;
        }, ct);
    }

    /// <inheritdoc />
    public Task<Dictionary<string, float[]>> InferNamedAsync(
        Dictionary<string, float[]> namedInputs,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var inputs = new List<NamedOnnxValue>();

            foreach (var (name, data) in namedInputs)
            {
                if (!_session.InputMetadata.TryGetValue(name, out var meta))
                    throw new ArgumentException($"Unknown input: {name}");

                var shape = meta.Dimensions.Select(d => d == -1 ? 1 : (int)d).ToArray();

                // Infer first dimension from data
                var expectedSize = shape.Skip(1).Aggregate(1, (a, b) => a * b);
                if (expectedSize > 0)
                    shape[0] = data.Length / expectedSize;

                var tensor = new DenseTensor<float>(data, shape);
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, tensor));
            }

            using var results = _session.Run(inputs);

            var outputs = new Dictionary<string, float[]>();
            foreach (var result in results)
            {
                outputs[result.Name] = result.AsTensor<float>().ToArray();
            }

            return outputs;
        }, ct);
    }

    /// <inheritdoc />
    public void WarmUp()
    {
        _logger.LogInformation("Warming up model...");

        var inputMeta = _session.InputMetadata.First();
        var shape = inputMeta.Value.Dimensions.Select(d => d == -1 ? 1 : (int)d).ToArray();
        var size = shape.Aggregate(1, (a, b) => a * b);

        var dummyInput = new float[size];
        var tensor = new DenseTensor<float>(dummyInput, shape);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputMeta.Key, tensor)
        };

        using var _ = _session.Run(inputs);

        _logger.LogInformation("Warm-up complete");
    }

    private static SessionOptions CreateSessionOptions(OnnxConfig config)
    {
        var options = new SessionOptions
        {
            EnableMemoryPattern = config.EnableMemoryPattern,
            GraphOptimizationLevel = config.OptimizationLevel switch
            {
                Configuration.GraphOptimizationLevel.None => Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_DISABLE_ALL,
                Configuration.GraphOptimizationLevel.Basic => Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_BASIC,
                Configuration.GraphOptimizationLevel.Extended => Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                Configuration.GraphOptimizationLevel.All => Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_ALL,
                _ => Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_ALL
            }
        };

        if (config.ThreadCount > 0)
        {
            options.InterOpNumThreads = config.ThreadCount;
            options.IntraOpNumThreads = config.ThreadCount;
        }

        if (!string.IsNullOrEmpty(config.OptimizedModelPath))
        {
            options.OptimizedModelFilePath = config.OptimizedModelPath;
        }

        // Configure execution provider
        switch (config.ExecutionProvider)
        {
            case ExecutionProvider.CUDA:
                try
                {
                    options.AppendExecutionProvider_CUDA(config.DeviceId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "CUDA execution provider not available. Install Microsoft.ML.OnnxRuntime.Gpu package.", ex);
                }
                break;

            case ExecutionProvider.DirectML:
                try
                {
                    options.AppendExecutionProvider_DML(config.DeviceId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "DirectML execution provider not available. Install Microsoft.ML.OnnxRuntime.DirectML package.", ex);
                }
                break;

            case ExecutionProvider.TensorRT:
                try
                {
                    options.AppendExecutionProvider_Tensorrt(config.DeviceId);
                    options.AppendExecutionProvider_CUDA(config.DeviceId); // Fallback
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "TensorRT execution provider not available.", ex);
                }
                break;

            case ExecutionProvider.CPU:
            default:
                // CPU is default, no additional configuration needed
                break;
        }

        // Add custom session options
        foreach (var (key, value) in config.SessionOptions)
        {
            options.AddSessionConfigEntry(key, value);
        }

        return options;
    }

    private static Core.ModelMetadata BuildMetadata(OnnxConfig config, InferenceSession session)
    {
        var inputShapes = new Dictionary<string, int[]>();
        var outputShapes = new Dictionary<string, int[]>();

        foreach (var (name, meta) in session.InputMetadata)
        {
            inputShapes[name] = meta.Dimensions.Select(d => (int)d).ToArray();
        }

        foreach (var (name, meta) in session.OutputMetadata)
        {
            outputShapes[name] = meta.Dimensions.Select(d => (int)d).ToArray();
        }

        var customMetadata = new Dictionary<string, string>();
        foreach (var (key, value) in session.ModelMetadata.CustomMetadataMap)
        {
            customMetadata[key] = value;
        }

        return new Core.ModelMetadata
        {
            Name = Path.GetFileNameWithoutExtension(config.ModelPath),
            Format = Core.ModelFormat.ONNX,
            InputShapes = inputShapes,
            OutputShapes = outputShapes,
            FileSizeBytes = new FileInfo(config.ModelPath).Length,
            CustomMetadata = customMetadata
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _session.Dispose();
        _disposed = true;
    }
}
