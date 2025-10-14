using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Service for configuring and managing structured logging with Serilog
    /// </summary>
    public static class LoggingService
    {
        private static volatile bool _isConfigured = false;
        private static readonly object _lockObject = new object();
        private static ILoggerFactory _loggerFactory;
        private static readonly object _factoryLock = new object();

        /// <summary>
        /// Configures Serilog for the application
        /// </summary>
        public static void ConfigureLogging()
        {
            lock (_lockObject)
            {
                if (_isConfigured) return;

                try
                {
                    // Create logs directory in AppData instead of Program Files
                    var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuadroAIPilot");
                    var logsPath = Path.Combine(appDataPath, "Logs");
                    Directory.CreateDirectory(logsPath);

                    // Configure Serilog
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Warning()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("System", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .Enrich.WithThreadId()
                        .Enrich.WithEnvironmentName()
                        .Enrich.WithMachineName()
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                        .WriteTo.File(
                            formatter: new CompactJsonFormatter(),
                            path: Path.Combine(logsPath, "quadroai-.json"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            shared: true)
                        .WriteTo.File(
                            path: Path.Combine(logsPath, "quadroai-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                        .CreateLogger();

                    Log.Warning("QuadroAI Pilot application starting up");
                    Log.Warning("Logging configured successfully");
                    
                    _isConfigured = true;
                }
                catch (Exception ex)
                {
                    // Fallback to console if file logging fails
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Warning()
                        .WriteTo.Console()
                        .CreateLogger();
                    
                    Log.Error(ex, "Failed to configure file logging, falling back to console only");
                    _isConfigured = true;
                }
            }
        }

        /// <summary>
        /// Creates an ILogger instance for the specified type
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
        {
            if (!_isConfigured)
            {
                ConfigureLogging();
            }

            return GetOrCreateLoggerFactory().CreateLogger<T>();
        }

        /// <summary>
        /// Creates an ILogger instance for the specified category
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            if (!_isConfigured)
            {
                ConfigureLogging();
            }

            return GetOrCreateLoggerFactory().CreateLogger(categoryName);
        }

        /// <summary>
        /// Gets or creates the singleton LoggerFactory instance
        /// </summary>
        private static ILoggerFactory GetOrCreateLoggerFactory()
        {
            if (_loggerFactory == null)
            {
                lock (_factoryLock)
                {
                    if (_loggerFactory == null)
                    {
                        _loggerFactory = LoggerFactory.Create(builder =>
                            builder.AddSerilog(Log.Logger));
                    }
                }
            }
            return _loggerFactory;
        }

        /// <summary>
        /// Logs application startup information
        /// </summary>
        public static void LogApplicationStart()
        {
            if (!_isConfigured)
            {
                ConfigureLogging();
            }

            Log.Warning("=== QuadroAI Pilot Application Started ===");
            Log.Warning("Version: {Version}", GetApplicationVersion());
            Log.Warning("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
            Log.Warning("Process ID: {ProcessId}", Environment.ProcessId);
            Log.Warning("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);
            Log.Warning("Command Line: {CommandLine}", Environment.CommandLine);
        }

        /// <summary>
        /// Logs application shutdown information
        /// </summary>
        public static void LogApplicationShutdown()
        {
            Log.Warning("=== QuadroAI Pilot Application Shutting Down ===");
            Log.CloseAndFlush();
        }

        /// <summary>
        /// Logs unhandled exceptions
        /// </summary>
        public static void LogUnhandledException(Exception exception, string context = "Unknown")
        {
            Log.Fatal(exception, "Unhandled exception in {Context}", context);
        }

        /// <summary>
        /// Logs performance metrics
        /// </summary>
        public static void LogPerformanceMetric(string operationName, long elapsedMilliseconds, string additionalInfo = null)
        {
            Log.Information("Performance: {OperationName} completed in {ElapsedMs}ms {AdditionalInfo}",
                operationName, elapsedMilliseconds, additionalInfo ?? "");
        }

        /// <summary>
        /// Logs command execution
        /// </summary>
        public static void LogCommandExecution(string command, bool success, long elapsedMilliseconds, string details = null)
        {
            if (success)
            {
                Log.Information("Command executed successfully: {Command} in {ElapsedMs}ms {Details}",
                    command, elapsedMilliseconds, details ?? "");
            }
            else
            {
                Log.Warning("Command execution failed: {Command} after {ElapsedMs}ms {Details}",
                    command, elapsedMilliseconds, details ?? "");
            }
        }

        /// <summary>
        /// Logs security events
        /// </summary>
        public static void LogSecurityEvent(string eventType, string details, bool isBlocked = false)
        {
            if (isBlocked)
            {
                Log.Warning("Security: {EventType} blocked - {Details}", eventType, details);
            }
            else
            {
                Log.Information("Security: {EventType} - {Details}", eventType, details);
            }
        }

        /// <summary>
        /// Logs error messages
        /// </summary>
        public static void LogError(string message, Exception exception = null)
        {
            if (exception != null)
                Log.Error(exception, message);
            else
                Log.Error(message);
        }

        /// <summary>
        /// Logs warning messages
        /// </summary>
        public static void LogWarning(string message)
        {
            Log.Warning(message);
        }

        /// <summary>
        /// Logs verbose/debug messages
        /// </summary>
        public static void LogVerbose(string message)
        {
            Log.Verbose(message);
        }

        /// <summary>
        /// Gets the application version
        /// </summary>
        private static string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}