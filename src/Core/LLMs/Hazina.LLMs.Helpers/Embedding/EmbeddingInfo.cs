using System.Text.Json.Serialization;

public class EmbeddingInfo
{
    public string Key { get; set; }
    public string Checksum { get; set; }

    [JsonConverter(typeof(EmbeddingJsonConverter))]
    public Embedding Data { get; set; }

    public EmbeddingInfo(string key, string checksum, Embedding data)
    {
        Key = key;
        Checksum = checksum;
        Data = data;
    }
}