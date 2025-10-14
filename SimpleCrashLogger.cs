using System;
using System.IO;
using System.Text;

namespace QuadroAIPilot
{
    /// <summary>
    /// Simple crash logger to diagnose startup issues
    /// </summary>
    public static class SimpleCrashLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuadroAIPilot",
            "startup_crash.log"
        );

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void LogException(Exception ex, string context = "")
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] EXCEPTION in {context}:");
                sb.AppendLine($"Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
                
                // EntryPointNotFoundException için özel işlem
                if (ex is EntryPointNotFoundException entryEx)
                {
                    sb.AppendLine($"Missing Entry Point: {entryEx.Message}");
                    if (ex.StackTrace != null)
                    {
                        // Stack trace'den hangi DLL ve fonksiyonun eksik olduğunu bulmaya çalış
                        var lines = ex.StackTrace.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("DllImport") || line.Contains("at "))
                            {
                                sb.AppendLine($"  -> {line.Trim()}");
                            }
                        }
                    }
                }
                
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"InnerException: {ex.InnerException.Message}");
                    sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}");
                }
                
                sb.AppendLine(new string('-', 80));
                
                File.AppendAllText(LogPath, sb.ToString());
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}