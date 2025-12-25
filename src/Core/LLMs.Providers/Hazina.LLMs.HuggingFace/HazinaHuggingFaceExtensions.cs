using System.Collections.Generic;


namespace Hazina.LLMs.HuggingFace;

public static class HazinaHuggingFaceExtensions
{
    public static Dictionary<string, object> ToHuggingFacePayload(this List<HazinaChatMessage> messages)
    {
        // Construct a HuggingFace compatible payload from chat messages
        // This is a stub, adjust as needed
        return new Dictionary<string, object> {
            { "inputs", string.Join("\n", messages.ConvertAll(m => $"[{m.Role?.Role}] {m.Text}")) }
        };
    }
}
