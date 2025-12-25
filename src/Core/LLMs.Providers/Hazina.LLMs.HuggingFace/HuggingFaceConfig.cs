namespace Hazina.LLMs.HuggingFace;

public class HuggingFaceConfig
{
    public string ApiKey { get; set; }
    public string Endpoint { get; set; }
    public string Model { get; set; }

    public static HuggingFaceConfig Load(string path = "huggingfaceconfig.json")
    {
        // Load from a config file (implementation depends on future requirements)
        // For now, provide dummy loading
        return new HuggingFaceConfig { ApiKey = "your_hf_key", Endpoint = "https://api-inference.huggingface.co", Model = "gpt2" };
    }
}
