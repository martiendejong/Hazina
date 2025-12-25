using System.Text.Json.Serialization;

public abstract class ChatResponse<T> where T : ChatResponse<T>, new()
{
    [JsonIgnore]
    public abstract T _example { get; }
    public static T Example => new T()._example;

    [JsonIgnore]
    public abstract string _signature { get; }
    public static string Signature => new T()._signature;

    public ChatResponse() { }
}