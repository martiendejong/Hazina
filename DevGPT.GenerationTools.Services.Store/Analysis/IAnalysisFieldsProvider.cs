using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Store
{
    public class AnalysisFieldInfo
    {
        public string Key { get; set; }
        public string File { get; set; }
        public string DisplayName { get; set; }
        public string ConfigFileName { get; set; }
        public string ComponentName { get; set; }
        public string RowComponentName { get; set; }
        /// <summary>
        /// Optional generic type name for structured output (e.g., "ToneOfVoice").
        /// When set, the content must be valid JSON matching this type's structure.
        /// </summary>
        public string GenericType { get; set; }
        /// <summary>
        /// JSON schema signature for the GenericType, used to instruct the LLM on expected format.
        /// </summary>
        public string TypeSignature { get; set; }
    }

    public interface IAnalysisFieldsProvider
    {
        Task<IReadOnlyList<AnalysisFieldInfo>> GetFieldsAsync(string projectId);
        Task<bool> SaveFieldAsync(string projectId, string key, string content, string feedback = null, string chatId = null, string userId = null);
    }

    public class AnalysisToolsOptions
    {
        public bool Enabled { get; set; } = true;
    }
}

