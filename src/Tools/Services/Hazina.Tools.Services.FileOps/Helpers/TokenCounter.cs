namespace Hazina.Tools.Services.FileOps.Helpers
{
    public class TokenCounter
    {
        public int CountTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            // naive token approximation by whitespace-separated words
            return text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
