using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models.WordPress.Blogs
{
    public class SafeJsonConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                JsonSerializerOptions newOptions = GetOptionsWithoutSelf(options);
                return JsonSerializer.Deserialize<T>(ref reader, newOptions);
            }
            catch (JsonException)
            {
                // Return default if deserialization fails
                return default;
            }
        }

        private static JsonSerializerOptions GetOptionsWithoutSelf(JsonSerializerOptions options)
        {
            var safe = options.Converters.First(c => c is SafeJsonConverter<T>);
            List<JsonConverter> c = options.Converters.ToList();
            c.Remove(safe);
            var newOptions = new JsonSerializerOptions();
            c.ForEach(i => newOptions.Converters.Add(i));
            return newOptions;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            JsonSerializerOptions newOptions = GetOptionsWithoutSelf(options);
            JsonSerializer.Serialize(writer, value, newOptions);
        }
    }
}

