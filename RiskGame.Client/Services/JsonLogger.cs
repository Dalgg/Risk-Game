using System;
using System.IO;
using System.Text.Json;

namespace RiskGame.Services
{
    public static class JsonLogger
    {
        private const string LogDirectory = "logs";
        private const string LogFile = "app_logs.jsonl";

        public static void Error(Exception exception, string messageTemplate, params object[] args)
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            string formattedMessage = string.Format(messageTemplate, args);
            
            string logEntry = JsonSerializer.Serialize(new
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Message = formattedMessage,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace
            });

            string path = Path.Combine(LogDirectory, LogFile);
            File.AppendAllText(path, logEntry + Environment.NewLine);
        }
    }
}
