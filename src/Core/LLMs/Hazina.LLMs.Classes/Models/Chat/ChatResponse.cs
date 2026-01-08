using System.Text.Json.Serialization;

/// <summary>
/// Base class for chat response models with example and signature support.
/// </summary>
/// <typeparam name="T">The concrete response type.</typeparam>
public abstract class ChatResponse<T> where T : ChatResponse<T>, new()
{
    /// <summary>
    /// Gets an example instance of the response type.
    /// </summary>
    [JsonIgnore]
    public abstract T _example { get; }

    /// <summary>
    /// Gets a static example instance.
    /// </summary>
    public static T Example => new T()._example;

    /// <summary>
    /// Gets the type signature string.
    /// </summary>
    [JsonIgnore]
    public abstract string _signature { get; }

    /// <summary>
    /// Gets the static type signature.
    /// </summary>
    public static string Signature => new T()._signature;

    /// <summary>
    /// Initializes a new instance of the ChatResponse class.
    /// </summary>
    public ChatResponse() { }
}