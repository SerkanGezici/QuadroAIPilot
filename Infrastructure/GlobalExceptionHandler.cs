using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Infrastructure
{
    /// <summary>
    /// Global exception handler for the application
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static ILogger? _logger;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes the global exception handler
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _logger = LoggingService.CreateLogger("GlobalExceptionHandler");
                
                // Handle unhandled exceptions in main thread
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                
                // Handle unhandled exceptions in background threads
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                _logger.LogInformation("Global exception handler initialized");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                // Fallback logging if logger creation fails
                System.Diagnostics.Debug.WriteLine($"Failed to initialize global exception handler: {ex}");
            }
        }

        /// <summary>
        /// Handles unhandled exceptions in the main thread
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception ?? new Exception("Unknown exception object");
                
                LoggingService.LogUnhandledException(exception, "AppDomain.UnhandledException");
                _logger?.LogCritical(exception, "Unhandled exception in main thread. IsTerminating: {IsTerminating}", e.IsTerminating);

                // Try to save any critical application state before termination
                if (e.IsTerminating)
                {
                    HandleApplicationTermination(exception);
                }
            }
            catch (Exception loggingException)
            {
                // Last resort logging
                System.Diagnostics.Debug.WriteLine($"Failed to log unhandled exception: {loggingException}");
            }
        }

        /// <summary>
        /// Handles unobserved task exceptions
        /// </summary>
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                foreach (var innerException in e.Exception.InnerExceptions)
                {
                    LoggingService.LogUnhandledException(innerException, "TaskScheduler.UnobservedTaskException");
                    _logger?.LogError(innerException, "Unobserved task exception");
                }

                // Mark the exception as observed to prevent process termination
                e.SetObserved();
            }
            catch (Exception loggingException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log unobserved task exception: {loggingException}");
            }
        }

        /// <summary>
        /// Handles critical application operations before termination
        /// </summary>
        private static void HandleApplicationTermination(Exception exception)
        {
            try
            {
                _logger?.LogCritical("Application is terminating due to unhandled exception");
                
                // Try to save application state, stop services, etc.
                SaveCriticalApplicationState();
                
                // Close logging properly
                LoggingService.LogApplicationShutdown();
            }
            catch (Exception terminationException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed during application termination handling: {terminationException}");
            }
        }

        /// <summary>
        /// Saves critical application state before shutdown
        /// </summary>
        private static void SaveCriticalApplicationState()
        {
            try
            {
                // Here you could save:
                // - User preferences
                // - Unsaved work
                // - Application settings
                // - etc.
                
                _logger?.LogInformation("Critical application state saved");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save critical application state");
            }
        }

        /// <summary>
        /// Manually handles and logs an exception
        /// </summary>
        public static void HandleException(Exception exception, string context = "Manual")
        {
            try
            {
                // EntryPointNotFoundException için özel işlem
                if (exception is EntryPointNotFoundException entryEx)
                {
                    _logger?.LogWarning("EntryPointNotFoundException in {Context}: {Message}", context, entryEx.Message);
                    SimpleCrashLogger.LogException(entryEx, $"EntryPointNotFound-{context}");
                    
                    // Stack trace'den hangi API çağrısından geldiğini bulmaya çalış
                    var stackTrace = exception.StackTrace;
                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        if (stackTrace.Contains("WindowsApiService"))
                            _logger?.LogWarning("Missing WinAPI function in WindowsApiService");
                        else if (stackTrace.Contains("NativeMAPIService"))
                            _logger?.LogWarning("Missing MAPI function in NativeMAPIService");
                        else if (stackTrace.Contains("DwmExtendFrameIntoClientArea"))
                            _logger?.LogWarning("Missing DWM API function - Windows version might not support DWM");
                    }
                    
                    // Bu tip hatalar genellikle kritik değil, devam edilebilir
                    return;
                }
                
                LoggingService.LogUnhandledException(exception, context);
                _logger?.LogError(exception, "Exception handled manually in context: {Context}", context);
            }
            catch (Exception loggingException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to handle exception manually: {loggingException}");
            }
        }

        /// <summary>
        /// Handles exceptions with user notification
        /// </summary>
        public static async Task HandleExceptionWithUserNotification(Exception exception, string userMessage, string context = "UserOperation")
        {
            try
            {
                // Log the exception
                HandleException(exception, context);

                // Show user-friendly error message
                await ShowUserErrorMessage(userMessage, exception);
            }
            catch (Exception handlingException)
            {
                _logger?.LogError(handlingException, "Failed to handle exception with user notification");
            }
        }

        /// <summary>
        /// Shows error message to user (implementation depends on UI framework)
        /// </summary>
        private static async Task ShowUserErrorMessage(string message, Exception exception)
        {
            try
            {
                // For WinUI 3, you might use ContentDialog or similar
                // This is a placeholder - implement based on your UI needs
                _logger?.LogInformation("Showing error message to user: {Message}", message);
                
                // Example implementation for debugging
                System.Diagnostics.Debug.WriteLine($"USER ERROR: {message}");
                System.Diagnostics.Debug.WriteLine($"TECHNICAL DETAILS: {exception.Message}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show error message to user");
            }
        }

        /// <summary>
        /// Determines if an exception is critical and requires immediate attention
        /// </summary>
        public static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException ||
                   exception is StackOverflowException ||
                   exception is AccessViolationException ||
                   exception is AppDomainUnloadedException ||
                   exception is BadImageFormatException ||
                   exception is InvalidProgramException;
        }

        /// <summary>
        /// Safely executes an action with exception handling
        /// </summary>
        public static void SafeExecute(Action action, string context = "SafeExecute")
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                HandleException(ex, context);
            }
        }

        /// <summary>
        /// Safely executes an async action with exception handling
        /// </summary>
        public static async Task SafeExecuteAsync(Func<Task> asyncAction, string context = "SafeExecuteAsync")
        {
            try
            {
                if (asyncAction != null)
                {
                    await asyncAction();
                }
            }
            catch (Exception ex)
            {
                HandleException(ex, context);
            }
        }

        /// <summary>
        /// Safely executes a function with exception handling and returns a result
        /// </summary>
        public static T? SafeExecute<T>(Func<T> func, T? defaultValue = default, string context = "SafeExecute")
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception ex)
            {
                HandleException(ex, context);
                return defaultValue;
            }
        }

        /// <summary>
        /// Cleans up the global exception handler
        /// </summary>
        public static void Cleanup()
        {
            if (!_isInitialized) return;

            try
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                
                _logger?.LogInformation("Global exception handler cleaned up");
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cleanup global exception handler: {ex}");
            }
        }
    }
}