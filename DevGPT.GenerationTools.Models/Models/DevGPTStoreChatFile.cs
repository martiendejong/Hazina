namespace DevGPT.GenerationTools.Models
{
    public class DevGPTStoreChatFile
    {
        public string File { get; set; }
        public bool IncludeInProject { get; set; } = false;
        public bool Generated { get; set; } = false;
    }
}
