using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Centralized logging service with log levels
    /// </summary>
    public static class LogService
    {
        public enum LogLevel
        {
            Verbose = 0,    // Her şeyi logla (döngüler dahil)
            Debug = 1,      // Debug bilgileri
            Info = 2,       // Genel bilgi mesajları
            Warning = 3,    // Uyarılar
            Error = 4,      // Hatalar
            Critical = 5    // Kritik hatalar
        }

        // Mevcut log seviyesi - Production'da Info veya üstü olmalı
#if DEBUG
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;
#else
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;
#endif

        /// <summary>
        /// Log a verbose message (döngü içi detaylar için)
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogVerbose(string message, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Verbose)
                Debug.WriteLine($"[VERBOSE][{caller}] {message}");
        }

        /// <summary>
        /// Log a debug message
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogDebug(string message, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Debug)
                Debug.WriteLine($"[DEBUG][{caller}] {message}");
        }

        /// <summary>
        /// Log an info message
        /// </summary>
        public static void LogInfo(string message, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Info)
                Debug.WriteLine($"[INFO][{caller}] {message}");
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public static void LogWarning(string message, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Warning)
                Debug.WriteLine($"[WARNING][{caller}] {message}");
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        public static void LogError(string message, Exception ex = null, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Error)
            {
                Debug.WriteLine($"[ERROR][{caller}] {message}");
                if (ex != null)
                    Debug.WriteLine($"[ERROR][{caller}] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Log a critical error message
        /// </summary>
        public static void LogCritical(string message, Exception ex = null, [CallerMemberName] string caller = "")
        {
            if (CurrentLogLevel <= LogLevel.Critical)
            {
                Debug.WriteLine($"[CRITICAL][{caller}] {message}");
                if (ex != null)
                    Debug.WriteLine($"[CRITICAL][{caller}] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }
    }
}