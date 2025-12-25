using Hazina.Tools.Models.WordPress.Blogs;
using Hazina.Tools.Models;

namespace Hazina.Tools.Services.FileOps.Helpers
{
    public class LogMessage
    {
        public string Role { get; set; }
        public string Message { get; set; }
    }

    public class LogEntry : Serializer<LogEntry>
    {
        public string Project { get; set; }
        public string Date { get; set; }
        public string Source { get; set; }
        public List<LogMessage> Messages { get; set; }
    }
}
