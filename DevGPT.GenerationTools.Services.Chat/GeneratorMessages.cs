using System;
using System.Collections.Generic;

namespace DevGPT.GenerationTools.Services.Chat
{
    public class GeneratorMessage
    {
        public string Message { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class GeneratorMessageForDate
    {
        public string Message { get; set; }
        public DateTime Date { get; set; }
    }

    public class GeneratorMessageForDateAndEvent
    {
        public string Message { get; set; }
        public DateTime Date { get; set; }
        public string Event { get; set; }
    }
}

