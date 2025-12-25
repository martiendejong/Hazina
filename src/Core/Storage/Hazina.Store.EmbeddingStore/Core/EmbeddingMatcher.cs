using OpenAI.Chat;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using static System.Net.Mime.MediaTypeNames;

public class EmbeddingMatcher
{
    public int MaxInputTokens = 8000;
    public int MaxQueryTokens = 8000;

    public TokenCounter TokenCounter = new TokenCounter();

    public string CutOffQuery(string query, int maxTokens = 0)
    {
        maxTokens = maxTokens == 0 ? MaxInputTokens : maxTokens;

        // if query is too many tokens we keep cutting at the front because with chat messages the end is what matters
        var tokens = TokenCounter.CountTokens(query);
        while (tokens > maxTokens)
        {
            double divider = (double)tokens / (double)maxTokens;
            var x = query.Length / divider;
            var y = (int)x;
            int newLength = (int)x;
            query = query.Substring(query.Length - newLength);
            tokens = TokenCounter.CountTokens(query);
        }
        return query;
    }

    public async Task<List<string>> TakeTop(List<RelevantEmbedding> total, int maxTokens = 0)
    {
        maxTokens = maxTokens == 0 ? MaxQueryTokens : maxTokens;

        var selectedDocuments = new List<string>();
        int currentTokenCount = 0;

        foreach (var document in total)
        {
            if (document.Document == null || document.GetText == null) throw new Exception("Illegal embedding, Document of GetText are null");

            var text = await document.GetText(document.Document.Key);
            if (text == null) continue;
            var documentView = $"File path: {document.Document.Key}\nFile content:\n{text}";
            if (document.StoreName != null) documentView = $"Store: {document.StoreName}\n{documentView}";

            // Count tokens for the current document
            int documentTokenCount = TokenCounter.CountTokens(documentView);

            // Check if adding this document would exceed max tokens
            if (currentTokenCount + documentTokenCount <= maxTokens)
            {
                selectedDocuments.Add(documentView);
                currentTokenCount += documentTokenCount;
            }
            else
            {
                // Stop if adding the document would exceed max tokens
                break;
            }
        }
        return selectedDocuments;
    }
    public static List<(double similarity, EmbeddingInfo document)> GetEmbeddingsWithSimilarity(Embedding query, IEnumerable<EmbeddingInfo> embeddings)
    {
        return embeddings.Select(document => (document.Data.CosineSimilarity(query), document)).OrderByDescending(d => d.Item1).ToList();
    }
}
