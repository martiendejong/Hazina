//using System.Text.Json;
//using System.Text.Json.Serialization;

//namespace backend.Models
//{
//    public class SafeJsonConverter<T> : JsonConverter<T>
//    {
//        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//        {
//            try
//            {
//                JsonSerializerOptions newOptions = GetOptionsWithoutSelf(options);
//                return JsonSerializer.Deserialize<T>(ref reader, newOptions);
//            }
//            catch (JsonException)
//            {
//                // Return default if deserialization fails
//                return default;
//            }
//        }

//        private static JsonSerializerOptions GetOptionsWithoutSelf(JsonSerializerOptions options)
//        {
//            var safe = options.Converters.First(c => c is SafeJsonConverter<T>);
//            List<JsonConverter> c = options.Converters.ToList();
//            c.Remove(safe);
//            var newOptions = new JsonSerializerOptions();
//            c.ForEach(i => newOptions.Converters.Add(i));
//            return newOptions;
//        }

//        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
//        {
//            JsonSerializerOptions newOptions = GetOptionsWithoutSelf(options);
//            JsonSerializer.Serialize(writer, value, newOptions);
//        }
//    }


//    public class Serializer<T> : ISerializer
//    {
//        public static JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
//        {
//            Converters = { 
//                new JsonStringEnumConverter(), 
//                new SafeJsonConverter<string>(),
//            },
//        };
//        public string Serialize() => JsonSerializer.Serialize(this, GetType(), JsonSerializerOptions);
//        public static string Serialize(T t) => JsonSerializer.Serialize(t, t.GetType(), JsonSerializerOptions);
//        public void Save(string file) => File.WriteAllText(file, Serialize());
//        public static void Save(T t, string file) => File.WriteAllText(file, Serialize(t));
//        public static T Deserialize(string json) => JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
//        public static T Load(string file) => Deserialize(File.ReadAllText(file));
//    }

//    public class SerializableList<T> : List<T>, ISerializer
//    {
//        public SerializableList()
//            : base()
//        {
//        }
//        public SerializableList(IEnumerable<T> items) 
//            : base(items) 
//        {
//        }
//        public static JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
//        {
//            Converters = { new JsonStringEnumConverter() }
//        };
//        public string Serialize() => JsonSerializer.Serialize(this, GetType(), JsonSerializerOptions);
//        public static string Serialize(SerializableList<T> t) => JsonSerializer.Serialize(t, t.GetType(), JsonSerializerOptions);
//        public void Save(string file) => File.WriteAllText(file, Serialize());
//        public static void Save(SerializableList<T> t, string file) => File.WriteAllText(file, Serialize(t));
//        public static SerializableList<T> Deserialize(string json) => JsonSerializer.Deserialize<SerializableList<T>>(json, JsonSerializerOptions);
//        public static SerializableList<T> Load(string file) => Deserialize(File.ReadAllText(file));
//    }
//}
