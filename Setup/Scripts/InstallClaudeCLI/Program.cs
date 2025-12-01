using System;
using System.Threading.Tasks;

namespace QuadroAIPilot.Setup
{
    /// <summary>
    /// Claude CLI Installer - Entry Point
    /// C# Native implementation (v56+)
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Logger başlat
            var logger = new Logger();

            try
            {
                logger.Log("QuadroAIPilot Claude CLI Kurulum Aracı (C# Native)");
                logger.Log($"Başlangıç: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                logger.LogSeparator();

                // Installer oluştur ve çalıştır
                var installer = new ClaudeCLIInstaller(logger);

                bool success = await installer.InstallAsync();

                logger.LogSeparator();
                logger.Log($"Bitiş: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                logger.Log($"Log dosyası: {installer.GetLogFilePath()}");
                logger.LogSeparator();

                if (success)
                {
                    logger.Log("Kurulum BAŞARILI!", LogLevel.Success);
                    return 0; // Success exit code
                }
                else
                {
                    logger.Log("Kurulum BAŞARISIZ!", LogLevel.Error);
                    return 1; // Error exit code
                }
            }
            catch (Exception ex)
            {
                logger.Log("FATAL EXCEPTION!", LogLevel.Error);
                logger.Log($"Exception Type: {ex.GetType().Name}");
                logger.Log($"Message: {ex.Message}");
                logger.Log($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    logger.Log("Inner Exception:", LogLevel.Error);
                    logger.Log($"  Type: {ex.InnerException.GetType().Name}");
                    logger.Log($"  Message: {ex.InnerException.Message}");
                }

                logger.LogSeparator();
                logger.Log("Lütfen log dosyasını QuadroAIPilot geliştiricilerine gönderin.");
                logger.Log($"Log: {logger.GetLogFilePath()}");

                return 2; // Fatal error exit code
            }
        }
    }
}
