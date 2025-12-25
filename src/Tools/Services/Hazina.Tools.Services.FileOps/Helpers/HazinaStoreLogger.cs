using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Hazina.Tools.Services.FileOps.Helpers
{
    public interface ILogger
    {
        void Log(List<OpenAI.Chat.ChatMessage> messages, string responseContent);
    }

    public class HazinaStoreLogger : ILogger
    {
        public string Project { get; set; }
        private readonly Serilog.Core.Logger _logger;

        public HazinaStoreLogger(string logFilePath, string project)
        {
            Project = project;
            _logger = new LoggerConfiguration()
                .WriteTo.File(
                    logFilePath,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Message:lj}{NewLine}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();
        }

        public static string GetMessageType(OpenAI.Chat.ChatMessage m)
        {
            if (m is AssistantChatMessage) return "Assistant";
            if (m is UserChatMessage) return "REGULAR";
            if (m is SystemChatMessage) return "System";
            if (m is ToolChatMessage) return "Tool";
            return "Unknown";
        }

        public void Log(List<OpenAI.Chat.ChatMessage> messages, string responseContent)
        {
            var logEntry = new LogEntry();
            logEntry.Date = DateTime.Now.ToString("MM-dd-yy HH:mm");
            logEntry.Source = GetType().Name;
            logEntry.Messages = messages.Select(m => new LogMessage { Role = GetMessageType(m), Message = m.Content.FirstOrDefault()?.Text ?? "" }).ToList();
            logEntry.Project = Project;

            var message = new LogMessage { Message = responseContent, Role = "Assistant" };
            logEntry.Messages.Add(message);

            var data = logEntry.Serialize();
            var prefix = File.Exists(_logger.ToString()) ? "," : "";
            _logger.Information("{Prefix}{Data}", prefix, data);
        }

        public void Dispose()
        {
            _logger?.Dispose();
        }
    }
}

