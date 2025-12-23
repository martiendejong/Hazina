using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Models
{
    public class SerializableList<T> : List<T>, ISerializer
    {
        public SerializableList()
            : base()
        {
        }
        public SerializableList(IEnumerable<T> items)
            : base(items)
        {
        }
        public static JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        public string Serialize() => JsonSerializer.Serialize(this, GetType(), JsonSerializerOptions);
        public static string Serialize(SerializableList<T> t) => JsonSerializer.Serialize(t, t.GetType(), JsonSerializerOptions);
        public void Save(string file) => File.WriteAllText(file, Serialize());
        public static void Save(SerializableList<T> t, string file) => File.WriteAllText(file, Serialize(t));
        public static SerializableList<T> Deserialize(string json) => JsonSerializer.Deserialize<SerializableList<T>>(json, JsonSerializerOptions);
        public static SerializableList<T> Load(string file) => Deserialize(File.ReadAllText(file));
    }
}

