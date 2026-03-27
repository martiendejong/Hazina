using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models.Social
{
    /// <summary>
    /// JSON converter that handles Comments field in both formats:
    /// - Integer: "Comments": 0 → empty list
    /// - Array: "Comments": [{...}] → keeps as-is
    /// </summary>
    public class FlexibleCommentsConverter : JsonConverter<List<ConnectedFacebookComment>>
    {
        public override List<ConnectedFacebookComment> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                // Handle integer format: "Comments": 0
                // We just ignore the count and return empty list
                reader.GetInt32();
                return new List<ConnectedFacebookComment>();
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Handle array format: "Comments": [{...}]
                return JsonSerializer.Deserialize<List<ConnectedFacebookComment>>(ref reader, options) ?? new List<ConnectedFacebookComment>();
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                // Handle null
                return new List<ConnectedFacebookComment>();
            }

            throw new JsonException($"Unexpected token type for Comments: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, List<ConnectedFacebookComment> value, JsonSerializerOptions options)
        {
            // Always write as array
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
