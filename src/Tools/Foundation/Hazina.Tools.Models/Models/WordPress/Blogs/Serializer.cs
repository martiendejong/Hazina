using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models.WordPress.Blogs
{
    public class Serializer<T> : ISerializer
    {
        public static JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
        {
            Converters = {
                new JsonStringEnumConverter(),
                new SafeJsonConverter<string>(),
            },
        };
        public string Serialize() => JsonSerializer.Serialize(this, GetType(), JsonSerializerOptions);
        public static string Serialize(T t) => JsonSerializer.Serialize(t, t.GetType(), JsonSerializerOptions);
        public void Save(string file) => File.WriteAllText(file, Serialize());
        public static void Save(T t, string file) => File.WriteAllText(file, Serialize(t));
        public static T Deserialize(string json) => JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        public static T Load(string file) => Deserialize(File.ReadAllText(file));
    }
}

