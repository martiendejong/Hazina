namespace Hazina.Tools.Models
{
    public class HazinaStoreChatFile
    {
        public string File { get; set; }
        public bool IncludeInProject { get; set; } = false;
        public bool Generated { get; set; } = false;
    }
}
