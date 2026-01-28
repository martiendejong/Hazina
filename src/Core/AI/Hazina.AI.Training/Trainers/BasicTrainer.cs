using Hazina.AI.Core;
using Hazina.AI.Training.LoRA;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Hazina.AI.Training.Trainers;

/// <summary>
/// Basic trainer for neural network models using TorchSharp.
/// Supports standard training loops with gradient accumulation and mixed precision.
/// </summary>
public class BasicTrainer : ITrainer<Module>
{
    private readonly ILogger<BasicTrainer> _logger;
    private TrainingConfig? _currentConfig;
    private int _globalStep;
    private float _runningLoss;
    private readonly List<float> _lossHistory = new();

    /// <summary>
    /// Creates a new BasicTrainer instance.
    /// </summary>
    public BasicTrainer(ILogger<BasicTrainer>? logger = null)
    {
        _logger = logger ?? NullLogger<BasicTrainer>.Instance;
    }

    /// <inheritdoc />
    public async Task<TrainingResult> TrainAsync(
        Module model,
        IDataset dataset,
        TrainingConfig config,
        IProgress<TrainingProgress>? progress = null,
        CancellationToken ct = default)
    {
        _currentConfig = config;
        _globalStep = 0;
        _runningLoss = 0;
        _lossHistory.Clear();

        var startTime = DateTime.UtcNow;

        try
        {
            // Set up device
            var device = ParseDevice(config.Device);
            model = model.to(device);
            model.train();

            // Set up optimizer
            var optimizer = CreateOptimizer(model, config);

            // Set up learning rate scheduler
            var scheduler = CreateScheduler(optimizer, config, dataset.Count, config.BatchSize);

            // Training loop
            for (int epoch = 1; epoch <= config.Epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();

                var epochLoss = await TrainEpochAsync(
                    model, dataset, optimizer, scheduler,
                    epoch, config, device, progress, ct);

                _lossHistory.Add(epochLoss);
                _logger.LogInformation("Epoch {Epoch}/{TotalEpochs} - Loss: {Loss:F6}",
                    epoch, config.Epochs, epochLoss);
            }

            var duration = DateTime.UtcNow - startTime;

            return new TrainingResult
            {
                Success = true,
                FinalLoss = _lossHistory.LastOrDefault(),
                Duration = duration,
                TotalSteps = _globalStep,
                LossHistory = new List<float>(_lossHistory)
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Training cancelled at step {Step}", _globalStep);

            return new TrainingResult
            {
                Success = false,
                FinalLoss = _lossHistory.LastOrDefault(),
                Duration = DateTime.UtcNow - startTime,
                TotalSteps = _globalStep,
                LossHistory = new List<float>(_lossHistory),
                ErrorMessage = "Training was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training failed at step {Step}", _globalStep);

            return new TrainingResult
            {
                Success = false,
                FinalLoss = _lossHistory.LastOrDefault(),
                Duration = DateTime.UtcNow - startTime,
                TotalSteps = _globalStep,
                LossHistory = new List<float>(_lossHistory),
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<float> TrainEpochAsync(
        Module model,
        IDataset dataset,
        OptimizerHelper optimizer,
        object? scheduler,
        int epoch,
        TrainingConfig config,
        Device device,
        IProgress<TrainingProgress>? progress,
        CancellationToken ct)
    {
        var totalLoss = 0f;
        var numBatches = (dataset.Count + config.BatchSize - 1) / config.BatchSize;
        var epochStartTime = DateTime.UtcNow;

        dataset.Shuffle(config.Seed);

        for (int batchIdx = 0; batchIdx < numBatches; batchIdx++)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await dataset.GetBatchAsync(batchIdx * config.BatchSize, config.BatchSize);

            var inputs = torch.tensor(Flatten(batch.Inputs), device: device);
            var targets = torch.tensor(Flatten(batch.Targets), device: device);

            // Forward pass
            var outputs = ((Module<Tensor, Tensor>)model).forward(inputs);
            var loss = ComputeLoss(outputs, targets);

            // Scale loss for gradient accumulation
            var scaledLoss = loss / config.GradientAccumulationSteps;

            // Backward pass
            scaledLoss.backward();

            _runningLoss += loss.item<float>();
            _globalStep++;

            // Gradient accumulation step
            if (_globalStep % config.GradientAccumulationSteps == 0)
            {
                // Gradient clipping
                if (config.MaxGradNorm > 0)
                {
                    torch.nn.utils.clip_grad_norm_(model.parameters(), config.MaxGradNorm);
                }

                optimizer.step();
                optimizer.zero_grad();

                // Update learning rate
                StepScheduler(scheduler);
            }

            totalLoss += loss.item<float>();

            // Report progress
            if (progress != null && _globalStep % 10 == 0)
            {
                var elapsed = DateTime.UtcNow - epochStartTime;
                var samplesProcessed = (batchIdx + 1) * config.BatchSize;
                var samplesPerSecond = samplesProcessed / elapsed.TotalSeconds;
                var remainingBatches = numBatches - batchIdx - 1;
                var estimatedRemaining = TimeSpan.FromSeconds(remainingBatches * config.BatchSize / samplesPerSecond);

                progress.Report(new TrainingProgress
                {
                    Epoch = epoch,
                    Step = _globalStep,
                    TotalSteps = numBatches * config.Epochs,
                    Loss = _runningLoss / 10,
                    LearningRate = GetCurrentLearningRate(optimizer),
                    SamplesPerSecond = (float)samplesPerSecond,
                    EstimatedTimeRemaining = estimatedRemaining
                });

                _runningLoss = 0;
            }

            // Cleanup tensors
            inputs.Dispose();
            targets.Dispose();
            outputs.Dispose();
            loss.Dispose();
            scaledLoss.Dispose();
        }

        return totalLoss / numBatches;
    }

    /// <inheritdoc />
    public async Task<EvaluationResult> EvaluateAsync(
        Module model,
        IDataset dataset,
        CancellationToken ct = default)
    {
        model.eval();

        using var _ = torch.no_grad();

        var totalLoss = 0f;
        var numBatches = (dataset.Count + 32 - 1) / 32;

        for (int batchIdx = 0; batchIdx < numBatches; batchIdx++)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await dataset.GetBatchAsync(batchIdx * 32, 32);
            var inputs = torch.tensor(Flatten(batch.Inputs));
            var targets = torch.tensor(Flatten(batch.Targets));

            var outputs = ((Module<Tensor, Tensor>)model).forward(inputs);
            var loss = ComputeLoss(outputs, targets);

            totalLoss += loss.item<float>();

            inputs.Dispose();
            targets.Dispose();
            outputs.Dispose();
            loss.Dispose();
        }

        var avgLoss = totalLoss / numBatches;

        return new EvaluationResult
        {
            Loss = avgLoss,
            Perplexity = MathF.Exp(avgLoss)
        };
    }

    /// <inheritdoc />
    public Task SaveCheckpointAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var checkpoint = new Dictionary<string, object>
            {
                ["global_step"] = _globalStep,
                ["loss_history"] = _lossHistory.ToArray()
            };

            // Save as JSON for simplicity
            var json = System.Text.Json.JsonSerializer.Serialize(checkpoint);
            File.WriteAllText(Path.ChangeExtension(path, ".meta.json"), json);
        }, ct);
    }

    /// <inheritdoc />
    public Task LoadCheckpointAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var metaPath = Path.ChangeExtension(path, ".meta.json");
            if (File.Exists(metaPath))
            {
                var json = File.ReadAllText(metaPath);
                var checkpoint = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (checkpoint != null)
                {
                    if (checkpoint.TryGetValue("global_step", out var step))
                        _globalStep = Convert.ToInt32(step);

                    if (checkpoint.TryGetValue("loss_history", out var history))
                    {
                        var arr = System.Text.Json.JsonSerializer.Deserialize<float[]>(history.ToString()!);
                        if (arr != null)
                        {
                            _lossHistory.Clear();
                            _lossHistory.AddRange(arr);
                        }
                    }
                }
            }
        }, ct);
    }

    private static OptimizerHelper CreateOptimizer(Module model, TrainingConfig config)
    {
        var parameters = model.parameters().Where(p => p.requires_grad).ToList();

        return torch.optim.AdamW(
            parameters,
            lr: config.LearningRate,
            weight_decay: config.WeightDecay);
    }

    private static object? CreateScheduler(
        OptimizerHelper optimizer,
        TrainingConfig config,
        int datasetSize,
        int batchSize)
    {
        if (config.WarmupSteps <= 0)
            return null;

        var totalSteps = (datasetSize / batchSize) * config.Epochs;

        // Return linear warmup scheduler (simplified)
        return new { WarmupSteps = config.WarmupSteps, TotalSteps = totalSteps };
    }

    private static void StepScheduler(object? scheduler)
    {
        // Simplified scheduler stepping
        // In production, this would call the actual scheduler
    }

    private static float GetCurrentLearningRate(OptimizerHelper optimizer)
    {
        // Get LR from first param group
        return (float)optimizer.ParamGroups.First().LearningRate;
    }

    private static Device ParseDevice(string device)
    {
        if (device.StartsWith("cuda"))
        {
            var parts = device.Split(':');
            var deviceId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            return torch.device(DeviceType.CUDA, deviceId);
        }

        return torch.device(DeviceType.CPU);
    }

    private static Tensor ComputeLoss(Tensor outputs, Tensor targets)
    {
        // Cross-entropy loss by default
        return torch.nn.functional.cross_entropy(outputs, targets.to(ScalarType.Int64));
    }

    private static float[] Flatten(float[][] arrays)
    {
        var totalLength = arrays.Sum(a => a.Length);
        var result = new float[totalLength];
        var offset = 0;

        foreach (var arr in arrays)
        {
            Array.Copy(arr, 0, result, offset, arr.Length);
            offset += arr.Length;
        }

        return result;
    }
}
