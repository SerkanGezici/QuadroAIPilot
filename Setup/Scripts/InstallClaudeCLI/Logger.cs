using System;
using System.IO;

namespace QuadroAIPilot.Setup
{
    /// <summary>
    /// Thread-safe structured logger
    /// Log dosyası: %TEMP%\QuadroAI_ClaudeCLI_CSharp.log
    /// </summary>
    public class Logger
    {
        private readonly string _logFile;
        private readonly object _lock = new object();

        public Logger()
        {
            _logFile = Path.Combine(Path.GetTempPath(), "QuadroAI_ClaudeCLI_CSharp.log");

            // Log dosyasını başlat
            lock (_lock)
            {
                File.WriteAllText(_logFile,
                    $"============================================{Environment.NewLine}" +
                    $"QuadroAIPilot Claude CLI Kurulumu (C# Native){Environment.NewLine}" +
                    $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}{Environment.NewLine}" +
                    $"============================================{Environment.NewLine}");
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = level switch
            {
                LogLevel.Info => "[BILGI]",
                LogLevel.Warning => "[UYARI]",
                LogLevel.Error => "[HATA]",
                LogLevel.Success => "[BAŞARILI]",
                _ => "[LOG]"
            };

            var logEntry = $"{timestamp} {prefix} {message}";

            // Console output
            Console.WriteLine(logEntry);

            // File output (thread-safe)
            lock (_lock)
            {
                File.AppendAllText(_logFile, logEntry + Environment.NewLine);
            }
        }

        public void LogSeparator()
        {
            Log("");
        }

        public string GetLogFilePath() => _logFile;
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }
}
