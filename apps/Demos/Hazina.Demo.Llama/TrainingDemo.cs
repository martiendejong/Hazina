using System.Diagnostics;

public static class TrainingDemo
{
    // Demonstrates how to invoke llama.cpp's finetune binary to create a LoRA adapter from a JSONL dataset.
    // Note: LLamaSharp focuses on inference. Finetuning happens outside via llama.cpp or frameworks like Axolotl/Unsloth.
    // Build llama.cpp (with examples) to get the 'finetune' tool, then call this method with proper paths.
    public static async Task<int> RunLoraFinetuneAsync(
        string finetuneExePath,
        string baseModelGguf,
        string trainDataJsonl,
        string outLoraPath,
        int epochs = 1,
        int batchSize = 4,
        CancellationToken cancel = default)
    {
        var args = $"--model \"{baseModelGguf}\" --train-data \"{trainDataJsonl}\" --out-lora \"{outLoraPath}\" --epochs {epochs} --batch {batchSize}";

        var psi = new ProcessStartInfo
        {
            FileName = finetuneExePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

        if (!p.Start()) return -1;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(cancel);
        return p.ExitCode;
    }
}


