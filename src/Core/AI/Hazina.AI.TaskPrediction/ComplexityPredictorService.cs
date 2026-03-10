using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.AI.TaskPrediction;

/// <summary>
/// Default implementation of complexity prediction using learned coefficients
/// </summary>
public class ComplexityPredictorService : IComplexityPredictor
{
    private readonly ILogger<ComplexityPredictorService> _logger;
    private readonly string _modelPath;
    private PredictionModel _model;

    public ComplexityPredictorService(ILogger<ComplexityPredictorService> logger, string? modelPath = null)
    {
        _logger = logger;
        _modelPath = modelPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hazina",
            "TaskPrediction",
            "prediction-model.json"
        );
        _model = LoadModel();
    }

    public async Task<ComplexityPrediction> PredictComplexity(
        string taskDescription,
        string taskType,
        Dictionary<string, object>? metadata = null)
    {
        await Task.CompletedTask; // Async signature for future ML model integration

        var features = ExtractFeatures(taskDescription, taskType, metadata);
        var complexity = CalculateComplexity(features, taskType);
        var hours = CalculateHours(complexity, taskType);
        var confidence = CalculateConfidence(features, taskType);
        var similar = FindSimilarTasks(taskDescription, taskType);

        _logger.LogInformation(
            "Predicted complexity {Complexity} ({Hours}h) for task type {Type} with confidence {Confidence:P0}",
            complexity, hours, taskType, confidence
        );

        return new ComplexityPrediction
        {
            Complexity = complexity,
            PredictedHours = hours,
            Confidence = confidence,
            Method = _model.TaskTypes.ContainsKey(taskType) ? "learned_model" : "base_estimate",
            SimilarTasks = similar,
            Factors = features
        };
    }

    public async Task UpdateModel(TaskOutcome outcome)
    {
        await Task.CompletedTask;

        if (!_model.TaskTypes.ContainsKey(outcome.TaskType))
        {
            _model.TaskTypes[outcome.TaskType] = new TaskTypeModel
            {
                TaskType = outcome.TaskType,
                BaseComplexity = 5.0,
                HoursPerComplexity = 1.0
            };
        }

        var typeModel = _model.TaskTypes[outcome.TaskType];
        typeModel.Outcomes.Add(outcome);

        // Exponential moving average for coefficients
        var alpha = 0.1; // Learning rate
        typeModel.BaseComplexity = (1 - alpha) * typeModel.BaseComplexity + alpha * outcome.ActualComplexity;
        typeModel.HoursPerComplexity = (1 - alpha) * typeModel.HoursPerComplexity + alpha * (outcome.ActualHours / Math.Max(outcome.ActualComplexity, 1));

        _logger.LogInformation(
            "Updated model for {Type}: BaseComplexity={Base:F2}, HoursPerComplexity={Hours:F2}",
            outcome.TaskType, typeModel.BaseComplexity, typeModel.HoursPerComplexity
        );

        SaveModel();
    }

    public async Task<double> GetAccuracy(string taskType)
    {
        await Task.CompletedTask;

        if (!_model.TaskTypes.ContainsKey(taskType))
            return 0.0;

        var outcomes = _model.TaskTypes[taskType].Outcomes;
        if (outcomes.Count == 0)
            return 0.0;

        // Calculate prediction accuracy (within 20% of actual)
        var correctPredictions = 0;
        foreach (var outcome in outcomes)
        {
            var predicted = CalculateComplexity(new Dictionary<string, double>(), taskType);
            var error = Math.Abs(predicted - outcome.ActualComplexity) / Math.Max(outcome.ActualComplexity, 1.0);
            if (error <= 0.2)
                correctPredictions++;
        }

        return (double)correctPredictions / outcomes.Count;
    }

    public async Task<CalibrationMetrics> GetCalibration()
    {
        await Task.CompletedTask;

        var metrics = new CalibrationMetrics
        {
            AccuracyByType = new Dictionary<string, double>()
        };

        foreach (var (type, typeModel) in _model.TaskTypes)
        {
            if (typeModel.Outcomes.Count > 0)
            {
                metrics.AccuracyByType[type] = await GetAccuracy(type);
                metrics.TotalOutcomes += typeModel.Outcomes.Count;
            }
        }

        metrics.OverallAccuracy = metrics.AccuracyByType.Values.DefaultIfEmpty(0).Average();
        metrics.TotalPredictions = metrics.TotalOutcomes;

        // Calculate MAE
        var errors = new List<double>();
        foreach (var typeModel in _model.TaskTypes.Values)
        {
            foreach (var outcome in typeModel.Outcomes)
            {
                var predicted = CalculateHours(outcome.ActualComplexity, outcome.TaskType);
                errors.Add(Math.Abs(predicted - outcome.ActualHours));
            }
        }
        metrics.MeanAbsoluteError = errors.DefaultIfEmpty(0).Average();

        return metrics;
    }

    private Dictionary<string, double> ExtractFeatures(string description, string taskType, Dictionary<string, object>? metadata)
    {
        var features = new Dictionary<string, double>
        {
            ["word_count"] = description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            ["char_count"] = description.Length,
            ["has_api"] = description.Contains("API", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0,
            ["has_database"] = description.Contains("database", StringComparison.OrdinalIgnoreCase) || description.Contains("DB", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0,
            ["has_ui"] = description.Contains("UI", StringComparison.OrdinalIgnoreCase) || description.Contains("frontend", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0,
            ["has_test"] = description.Contains("test", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0,
        };

        return features;
    }

    private int CalculateComplexity(Dictionary<string, double> features, string taskType)
    {
        var baseComplexity = _model.TaskTypes.ContainsKey(taskType)
            ? _model.TaskTypes[taskType].BaseComplexity
            : 5.0;

        // Simple linear model (will be replaced with ML in future)
        var complexity = baseComplexity;
        complexity += features.GetValueOrDefault("has_api", 0) * 1.5;
        complexity += features.GetValueOrDefault("has_database", 0) * 1.5;
        complexity += features.GetValueOrDefault("has_ui", 0) * 1.0;
        complexity += features.GetValueOrDefault("has_test", 0) * 0.5;
        complexity += features.GetValueOrDefault("word_count", 0) / 50.0;

        return Math.Clamp((int)Math.Round(complexity), 1, 10);
    }

    private double CalculateHours(int complexity, string taskType)
    {
        var hoursPerComplexity = _model.TaskTypes.ContainsKey(taskType)
            ? _model.TaskTypes[taskType].HoursPerComplexity
            : 1.0;

        return complexity * hoursPerComplexity;
    }

    private double CalculateConfidence(Dictionary<string, double> features, string taskType)
    {
        if (!_model.TaskTypes.ContainsKey(taskType))
            return 0.3; // Low confidence for unknown types

        var outcomeCount = _model.TaskTypes[taskType].Outcomes.Count;
        return Math.Min(0.5 + (outcomeCount * 0.05), 0.95); // Confidence grows with data
    }

    private List<string> FindSimilarTasks(string description, string taskType)
    {
        // Simplified similarity - will use embeddings in future
        return [];
    }

    private PredictionModel LoadModel()
    {
        try
        {
            if (File.Exists(_modelPath))
            {
                var json = File.ReadAllText(_modelPath);
                var model = JsonSerializer.Deserialize<PredictionModel>(json);
                if (model != null)
                {
                    _logger.LogInformation("Loaded prediction model from {Path} with {Count} task types", _modelPath, model.TaskTypes.Count);
                    return model;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load prediction model, using defaults");
        }

        return new PredictionModel();
    }

    private void SaveModel()
    {
        try
        {
            var dir = Path.GetDirectoryName(_modelPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_model, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_modelPath, json);
            _logger.LogInformation("Saved prediction model to {Path}", _modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save prediction model");
        }
    }
}

internal class PredictionModel
{
    public Dictionary<string, TaskTypeModel> TaskTypes { get; set; } = [];
}

internal class TaskTypeModel
{
    public required string TaskType { get; set; }
    public double BaseComplexity { get; set; } = 5.0;
    public double HoursPerComplexity { get; set; } = 1.0;
    public List<TaskOutcome> Outcomes { get; set; } = [];
}
