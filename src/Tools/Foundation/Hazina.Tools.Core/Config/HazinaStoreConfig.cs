using Hazina.LLMs.OpenAI;

namespace HazinaStore.Models
{
    public class HazinaStoreConfig
    {
        public ApiSettings ApiSettings;
        public ProjectSettings ProjectSettings;
        public GoogleOAuthSettings GoogleOAuthSettings;
        public SupabaseSettings SupabaseSettings;
        public SqliteSettings SqliteSettings;
        public OpenAIConfig OpenAI;
    }
}
