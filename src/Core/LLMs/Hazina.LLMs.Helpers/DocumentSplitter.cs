public class DocumentSplitter
{
    public int TokensPerPart { get; set; } = 1000;

    public List<string> SplitFile(string path, string split = "\n")
    {
        var content = File.ReadAllText(path);
        return SplitDocument(content, split);
    }

    public List<string> SplitDocument(string content, string split = "\n")
    {
        var tokenCounter = new TokenCounter();
        var remainingLines = content.Split(split).ToList();

        var result = new List<string>();
        while (remainingLines.Count > 0)
        {
            var partLines = new List<string>();
            bool partComplete = false;
            var moveLineToPart = () =>
            {
                partLines.Add(remainingLines[0]);
                remainingLines.RemoveAt(0);
                var partTokens = tokenCounter.CountTokens(string.Join(split, partLines));
                partComplete = partTokens >= TokensPerPart;
            };
            moveLineToPart();
            while (!partComplete && remainingLines.Count > 0)
            {
                moveLineToPart();
            }

            result.Add(string.Join(split, partLines));
        }

        return result;
    }
}

public class StringSplitter
{
    public static string[] Split(string item)
    {
        return [.. item.Split(",").Select(p => p.Trim())];
    }
}
