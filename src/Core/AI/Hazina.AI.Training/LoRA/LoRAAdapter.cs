using Hazina.AI.Core;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Hazina.AI.Training.LoRA;

/// <summary>
/// LoRA adapter that can be attached to transformer models.
/// Manages multiple LoRALinear layers for target modules.
/// </summary>
public class LoRAAdapter : ILoRAAdapter
{
    private readonly LoRAConfig _config;
    private readonly Dictionary<string, LoRALinear> _loraLayers = new();
    private Module? _attachedModel;
    private bool _disposed;

    /// <inheritdoc />
    public int Rank => _config.Rank;

    /// <inheritdoc />
    public float Alpha => _config.Alpha;

    /// <inheritdoc />
    public float Dropout => _config.Dropout;

    /// <inheritdoc />
    public IReadOnlyList<string> TargetModules => _config.TargetModules;

    /// <inheritdoc />
    public long TrainableParameterCount => _loraLayers.Values.Sum(l => l.TrainableParameters);

    /// <summary>
    /// Creates a new LoRA adapter with the given configuration.
    /// </summary>
    public LoRAAdapter(LoRAConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Creates a new LoRA adapter with default configuration.
    /// </summary>
    public LoRAAdapter(int rank = 16, float alpha = 32f, float dropout = 0.05f)
        : this(new LoRAConfig
        {
            Rank = rank,
            Alpha = alpha,
            Dropout = dropout
        })
    {
    }

    /// <inheritdoc />
    public void AttachTo(object baseModel)
    {
        if (baseModel is not Module torchModule)
            throw new ArgumentException("Model must be a TorchSharp Module", nameof(baseModel));

        _attachedModel = torchModule;

        // Find and wrap target linear layers
        foreach (var (name, module) in torchModule.named_modules())
        {
            if (!IsTargetModule(name, module))
                continue;

            if (module is Linear linearModule)
            {
                var loraLinear = new LoRALinear(
                    linearModule,
                    _config.Rank,
                    _config.Alpha,
                    _config.Dropout,
                    _config.UseRSLoRA);

                _loraLayers[name] = loraLinear;

                // Replace the module in the parent
                ReplaceModule(torchModule, name, loraLinear);
            }
        }

        if (_loraLayers.Count == 0)
        {
            throw new InvalidOperationException(
                $"No target modules found matching: {string.Join(", ", _config.TargetModules)}");
        }
    }

    /// <inheritdoc />
    public void Detach()
    {
        if (_attachedModel == null)
            return;

        // Restore original linear layers
        foreach (var (name, loraLinear) in _loraLayers)
        {
            // Get the base linear from the LoRA layer
            var baseLinear = GetBaseLinear(loraLinear);
            if (baseLinear != null)
            {
                ReplaceModule(_attachedModel, name, baseLinear);
            }
        }

        _loraLayers.Clear();
        _attachedModel = null;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var dir = Path.GetDirectoryName(path) ?? ".";
            Directory.CreateDirectory(dir);

            // Save each LoRA layer's weights
            foreach (var (name, loraLayer) in _loraLayers)
            {
                var safeName = name.Replace(".", "_").Replace("/", "_");
                loraLayer.SaveLoRAWeights(Path.Combine(dir, $"{safeName}.lora"));
            }

            // Save metadata
            var metadata = new Dictionary<string, object>
            {
                ["rank"] = _config.Rank,
                ["alpha"] = _config.Alpha,
                ["dropout"] = _config.Dropout,
                ["target_modules"] = _config.TargetModules,
                ["layer_names"] = _loraLayers.Keys.ToList()
            };

            var configPath = Path.ChangeExtension(path, ".json");
            var json = System.Text.Json.JsonSerializer.Serialize(metadata);
            File.WriteAllText(configPath, json);
        }, ct);
    }

    /// <inheritdoc />
    public async Task LoadAsync(string path, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var configPath = Path.ChangeExtension(path, ".json");
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"LoRA config not found: {configPath}");

            var json = File.ReadAllText(configPath);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);

            if (metadata == null)
                throw new InvalidOperationException("Failed to load LoRA metadata");

            var dir = Path.GetDirectoryName(path) ?? ".";

            foreach (var (name, loraLayer) in _loraLayers)
            {
                var safeName = name.Replace(".", "_").Replace("/", "_");
                var loraPath = Path.Combine(dir, $"{safeName}.lora");
                if (File.Exists(loraPath))
                {
                    loraLayer.LoadLoRAWeights(loraPath);
                }
            }
        }, ct);
    }

    /// <inheritdoc />
    public object MergeWithBase(object baseModel)
    {
        if (baseModel is not Module torchModule)
            throw new ArgumentException("Model must be a TorchSharp Module", nameof(baseModel));

        // Clone the model first
        var mergedModel = torchModule; // Note: TorchSharp doesn't have easy clone

        foreach (var (name, loraLayer) in _loraLayers)
        {
            var mergedLinear = loraLayer.Merge();
            ReplaceModule(mergedModel, name, mergedLinear);
        }

        return mergedModel;
    }

    /// <inheritdoc />
    public void Train()
    {
        foreach (var layer in _loraLayers.Values)
        {
            layer.train();
        }
    }

    /// <inheritdoc />
    public void Eval()
    {
        foreach (var layer in _loraLayers.Values)
        {
            layer.eval();
        }
    }

    /// <summary>
    /// Gets all LoRA parameters for optimizer registration.
    /// </summary>
    public IEnumerable<Parameter> GetParameters()
    {
        return _loraLayers.Values.SelectMany(l => l.GetLoRAParameters());
    }

    private bool IsTargetModule(string name, Module module)
    {
        // Check if module is a Linear layer by its type name
        var typeName = module.GetType().Name;
        if (!typeName.Contains("Linear"))
            return false;

        foreach (var target in _config.TargetModules)
        {
            if (name.EndsWith(target) || name.Contains($".{target}.") || name == target)
                return true;
        }

        return false;
    }

    private static void ReplaceModule(Module parent, string fullName, Module newModule)
    {
        var parts = fullName.Split('.');

        // Navigate to parent of target module
        Module current = parent;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var found = false;
            foreach (var (name, child) in current.named_children())
            {
                if (name == parts[i])
                {
                    current = child;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new InvalidOperationException($"Could not find module: {string.Join(".", parts[..(i + 1)])}");
        }

        // Replace the final module using register_module
        current.register_module(parts[^1], newModule);
    }

    private static Linear? GetBaseLinear(LoRALinear loraLinear)
    {
        // Access the base linear through reflection since it's private
        var field = typeof(LoRALinear).GetField("_baseLinear",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return field?.GetValue(loraLinear) as Linear;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Detach();

        foreach (var layer in _loraLayers.Values)
        {
            layer.Dispose();
        }

        _loraLayers.Clear();
        _disposed = true;
    }
}
