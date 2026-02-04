using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter for Embedding class (which extends List&lt;double&gt;).
/// System.Text.Json cannot deserialize List-derived classes by default.
/// </summary>
public class EmbeddingJsonConverter : JsonConverter<Embedding>
{
    public override Embedding? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray token, got {reader.TokenType}");
        }

        var values = new List<double>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                values.Add(reader.GetDouble());
            }
            else
            {
                throw new JsonException($"Expected Number token in array, got {reader.TokenType}");
            }
        }

        return new Embedding(values);
    }

    public override void Write(Utf8JsonWriter writer, Embedding value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var val in value)
        {
            writer.WriteNumberValue(val);
        }
        writer.WriteEndArray();
    }
}
