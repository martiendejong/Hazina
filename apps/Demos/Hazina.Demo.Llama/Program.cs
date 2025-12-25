using LLama;
using LLama.Common;

class Program
{
    static async Task Main()
    {
        // Path to your local GGUF model (set LLAMA_MODEL env var to override)
        string modelPath = Environment.GetEnvironmentVariable("LLAMA_MODEL") 
                            ?? "models/mistral-7b-instruct.Q4_K_M.gguf";

        // Load model
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 2048,
            // If you install a GPU backend package (Cuda/Metal/Vulkan),
            // set GpuLayerCount > 0 to offload layers to GPU.
            // GpuLayerCount = 0
        };

        using var weights = LLamaWeights.LoadFromFile(parameters);
        using var context = weights.CreateContext(parameters);

        // Simple inference
        string prompt = "Explain quantum entanglement in simple terms:";
        var executor = new InteractiveExecutor(context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 200
        };

        await foreach (var text in executor.InferAsync(prompt, inferenceParams))
        {
            Console.Write(text);
        }

        Console.WriteLine();
    }
}

