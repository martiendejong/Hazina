using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Hazina.AI.Training.LoRA;

/// <summary>
/// Low-Rank Adaptation linear layer.
/// Implements the LoRA technique from "LoRA: Low-Rank Adaptation of Large Language Models"
/// (Hu et al., 2021).
/// </summary>
/// <remarks>
/// Instead of fine-tuning the full weight matrix W, LoRA decomposes the update into:
/// W' = W + (alpha/r) * B @ A
/// where A is (in_features, rank) and B is (rank, out_features).
/// This reduces trainable parameters from d*k to r*(d+k) where r &lt;&lt; min(d,k).
/// </remarks>
public class LoRALinear : Module<Tensor, Tensor>
{
    private readonly Linear _baseLinear;
    private readonly int _rank;
    private readonly float _alpha;
    private readonly float _dropout;
    private readonly float _scaling;

    // LoRA matrices
    private readonly Parameter _loraA;
    private readonly Parameter _loraB;
    private readonly Dropout? _dropoutLayer;

    /// <summary>
    /// The rank of the low-rank decomposition.
    /// </summary>
    public int Rank => _rank;

    /// <summary>
    /// The scaling factor alpha.
    /// </summary>
    public float Alpha => _alpha;

    /// <summary>
    /// Number of trainable parameters in the LoRA adapter.
    /// </summary>
    public long TrainableParameters => _loraA.numel() + _loraB.numel();

    /// <summary>
    /// Creates a new LoRA linear layer wrapping an existing linear layer.
    /// </summary>
    /// <param name="baseLinear">The base linear layer to adapt.</param>
    /// <param name="rank">Rank of the low-rank decomposition (default: 16).</param>
    /// <param name="alpha">Scaling factor (default: 32).</param>
    /// <param name="dropout">Dropout probability (default: 0.0).</param>
    /// <param name="useRSLoRA">Use rank-stabilized scaling (1/sqrt(r) instead of 1/r).</param>
    public LoRALinear(
        Linear baseLinear,
        int rank = 16,
        float alpha = 32f,
        float dropout = 0.0f,
        bool useRSLoRA = false)
        : base(nameof(LoRALinear))
    {
        _baseLinear = baseLinear;
        _rank = rank;
        _alpha = alpha;
        _dropout = dropout;

        // Scaling factor: alpha / rank (or alpha / sqrt(rank) for RSLoRA)
        _scaling = useRSLoRA
            ? alpha / MathF.Sqrt(rank)
            : alpha / rank;

        var inFeatures = baseLinear.weight!.shape[1];
        var outFeatures = baseLinear.weight!.shape[0];

        // Initialize LoRA matrices
        // A: Kaiming uniform initialization
        // B: Zero initialization (so LoRA has no effect at start)
        _loraA = Parameter(torch.zeros(inFeatures, rank, dtype: baseLinear.weight!.dtype));
        _loraB = Parameter(torch.zeros(rank, outFeatures, dtype: baseLinear.weight!.dtype));

        // Initialize A with Kaiming uniform
        nn.init.kaiming_uniform_(_loraA, a: MathF.Sqrt(5));

        if (dropout > 0)
        {
            _dropoutLayer = Dropout(dropout);
            RegisterComponents();
        }

        // Freeze base weights
        _baseLinear.weight!.requires_grad = false;
        if (_baseLinear.bias is not null)
            _baseLinear.bias.requires_grad = false;

        RegisterComponents();
    }

    /// <inheritdoc />
    public override Tensor forward(Tensor input)
    {
        // Base forward pass (frozen)
        var result = _baseLinear.forward(input);

        // LoRA forward pass
        var loraInput = input;
        if (_dropoutLayer is not null && training)
        {
            loraInput = _dropoutLayer.forward(loraInput);
        }

        // LoRA: x @ A @ B * scaling
        var loraOutput = torch.matmul(torch.matmul(loraInput, _loraA), _loraB) * _scaling;

        return result + loraOutput;
    }

    /// <summary>
    /// Merges LoRA weights into the base linear layer.
    /// After merging, the base layer can be used without LoRA overhead.
    /// </summary>
    /// <returns>A new Linear layer with merged weights.</returns>
    public Linear Merge()
    {
        using var _ = torch.no_grad();

        var mergedWeight = _baseLinear.weight! + torch.matmul(_loraA, _loraB).t() * _scaling;

        var merged = Linear(
            mergedWeight.shape[1],
            mergedWeight.shape[0],
            hasBias: _baseLinear.bias is not null,
            dtype: mergedWeight.dtype);

        merged.weight!.copy_(mergedWeight);

        if (_baseLinear.bias is not null)
        {
            merged.bias!.copy_(_baseLinear.bias);
        }

        return merged;
    }

    /// <summary>
    /// Saves only the LoRA weights (A and B matrices).
    /// </summary>
    /// <param name="path">Path to save the weights.</param>
    public void SaveLoRAWeights(string path)
    {
        // Save A and B matrices using TorchSharp's state_dict approach
        var dir = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(dir);

        _loraA.save(Path.Combine(dir, "lora_a.pt"));
        _loraB.save(Path.Combine(dir, "lora_b.pt"));

        // Write metadata
        File.WriteAllText(path, $"rank={_rank}\nalpha={_alpha}\ndropout={_dropout}");
    }

    /// <summary>
    /// Loads LoRA weights from a file.
    /// </summary>
    /// <param name="path">Path to the weights file.</param>
    public void LoadLoRAWeights(string path)
    {
        using var _ = torch.no_grad();

        var dir = Path.GetDirectoryName(path) ?? ".";
        var loraAPath = Path.Combine(dir, "lora_a.pt");
        var loraBPath = Path.Combine(dir, "lora_b.pt");

        if (File.Exists(loraAPath))
        {
            var loraA = torch.load(loraAPath);
            _loraA.copy_(loraA);
            loraA.Dispose();
        }

        if (File.Exists(loraBPath))
        {
            var loraB = torch.load(loraBPath);
            _loraB.copy_(loraB);
            loraB.Dispose();
        }
    }

    /// <summary>
    /// Gets the LoRA parameters for optimizer registration.
    /// </summary>
    public IEnumerable<Parameter> GetLoRAParameters()
    {
        yield return _loraA;
        yield return _loraB;
    }
}
