// See https://aka.ms/new-console-template for more information
public class StorePaths
{
    public StorePaths(string rootFolder, string embeddingsFile = "", string chunksFile = "")
    {
        Directory.CreateDirectory(rootFolder);
        if (string.IsNullOrWhiteSpace(embeddingsFile)) embeddingsFile = Path.Combine(rootFolder, "embeddings");
        if(string.IsNullOrWhiteSpace(chunksFile)) chunksFile = Path.Combine(rootFolder, "chunks");
        RootFolder = rootFolder;
        EmbeddingsFile = embeddingsFile;
        ChunksFile = chunksFile;
    }
    public string RootFolder;
    public string EmbeddingsFile;
    public string ChunksFile;
}
