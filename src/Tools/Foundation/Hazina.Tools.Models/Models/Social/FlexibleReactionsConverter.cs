using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models.Social
{
    /// <summary>
    /// JSON converter that handles Reactions field in both formats:
    /// - Integer: "Reactions": 2 → {"total": 2}
    /// - Dictionary: "Reactions": {"like": 1, "love": 1} → keeps as-is
    /// </summary>
    public class FlexibleReactionsConverter : JsonConverter<Dictionary<string, int>>
    {
        public override Dictionary<string, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                // Handle integer format: "Reactions": 2
                var count = reader.GetInt32();
                return new Dictionary<string, int> { { "total", count } };
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Handle dictionary format: "Reactions": {"like": 1, "love": 1}
                return JsonSerializer.Deserialize<Dictionary<string, int>>(ref reader, options);
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                // Handle null
                return new Dictionary<string, int>();
            }

            throw new JsonException($"Unexpected token type for Reactions: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, int> value, JsonSerializerOptions options)
        {
            // Always write as dictionary
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
