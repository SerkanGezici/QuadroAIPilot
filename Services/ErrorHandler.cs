using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Centralized error handling and logging service
    /// </summary>
    public static class ErrorHandler
    {
        /// <summary>
        /// Safely executes an async operation with error handling
        /// </summary>
        public static async Task<T?> SafeExecuteAsync<T>(Func<Task<T>> operation, string context, T? defaultValue = default)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(ex, context);
                return defaultValue;
            }
        }

        /// <summary>
        /// Safely executes an async operation without return value
        /// </summary>
        public static async Task SafeExecuteAsync(Func<Task> operation, string context)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(ex, context);
            }
        }

        /// <summary>
        /// Safely executes a synchronous operation with error handling
        /// </summary>
        public static T? SafeExecute<T>(Func<T> operation, string context, T? defaultValue = default)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                LogError(ex, context);
                return defaultValue;
            }
        }

        /// <summary>
        /// Safely executes a synchronous operation without return value
        /// </summary>
        public static void SafeExecute(Action operation, string context)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                LogError(ex, context);
            }
        }

        /// <summary>
        /// Logs error asynchronously
        /// </summary>
        public static async Task LogErrorAsync(Exception ex, string context)
        {
            await Task.Run(() => LogError(ex, context));
        }

        /// <summary>
        /// Logs error synchronously
        /// </summary>
        public static void LogError(Exception ex, string context)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var errorMessage = $"[ERROR] {timestamp} - Context: {context}\n" +
                             $"Exception: {ex.GetType().Name}\n" +
                             $"Message: {ex.Message}\n" +
                             $"StackTrace: {ex.StackTrace}\n" +
                             new string('-', 80);

            Debug.WriteLine(errorMessage);
            
            // TODO: Future enhancement - write to file or send to logging service
            // File.AppendAllTextAsync("error.log", errorMessage + "\n");
        }

        /// <summary>
        /// Gets a user-friendly error message from exception
        /// </summary>
        public static string GetUserFriendlyMessage(this Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => "Bu işlem için yeterli yetkiniz yok.",
                System.IO.FileNotFoundException => "İstenen dosya bulunamadı.",
                System.IO.DirectoryNotFoundException => "İstenen klasör bulunamadı.",
                TimeoutException => "İşlem zaman aşımına uğradı.",
                ArgumentException => "Geçersiz parametre.",
                InvalidOperationException => "Geçersiz işlem.",
                _ => "Beklenmeyen bir hata oluştu."
            };
        }

        /// <summary>
        /// Logs performance metrics
        /// </summary>
        public static void LogPerformance(string operation, TimeSpan duration)
        {
            if (duration.TotalMilliseconds > 1000)
            {
                Debug.WriteLine($"[PERFORMANCE WARNING] {operation} took {duration.TotalMilliseconds:F0}ms");
            }
            else
            {
                Debug.WriteLine($"[PERFORMANCE] {operation} completed in {duration.TotalMilliseconds:F0}ms");
            }
        }

        /// <summary>
        /// Measures and logs operation performance
        /// </summary>
        public static async Task<T?> MeasureAsync<T>(Func<Task<T>> operation, string operationName, T? defaultValue = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await operation();
                stopwatch.Stop();
                LogPerformance(operationName, stopwatch.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogPerformance($"{operationName} (FAILED)", stopwatch.Elapsed);
                await LogErrorAsync(ex, operationName);
                return defaultValue;
            }
        }
    }
}