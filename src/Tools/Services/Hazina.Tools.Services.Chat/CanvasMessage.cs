namespace DevGPT.GenerationTools.Services.Chat
{
    public class CanvasMessage
    {
        public string Before { get; set; }
        public string After { get; set; }
        public string Selected { get; set; }
        public string Text { get; set; }
        public string Prompt { get; set; }
        public int Index { get; set; }
    }
}

