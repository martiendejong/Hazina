using System.Text;

// Minimal console "Claude Code" style assistant powered by OpenAI LLM

var config = OpenAIConfig.Load();
var client = new OpenAIClientWrapper(config);

var systemPreamble = "You are Claude Code: a pragmatic coding assistant. Be concise, give runnable code snippets, and explain only what is necessary.";

List<DevGPTChatMessage> MakeContext(string prompt)
{
    return new List<DevGPTChatMessage>
    {
        new DevGPTChatMessage { Role = DevGPTMessageRole.System, Text = systemPreamble },
        new DevGPTChatMessage { Role = DevGPTMessageRole.User, Text = prompt }
    };
}

async Task RunOnce(string prompt)
{
    var messages = MakeContext(prompt);
    var sb = new StringBuilder();
    void OnChunk(string chunk)
    {
        sb.Append(chunk);
        Console.Write(chunk);
    }
    Console.OutputEncoding = Encoding.UTF8;
    var _ = await client.GetResponseStream(messages, OnChunk, DevGPTChatResponseFormat.Text, toolsContext: null, images: null, CancellationToken.None);
    Console.WriteLine();
}

if (args.Length > 0)
{
    await RunOnce(string.Join(" ", args));
    return;
}

Console.WriteLine("Claude Code (OpenAI) — type 'exit' to quit");
while (true)
{
    Console.Write("\n> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line) || line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;
    await RunOnce(line!);
}

