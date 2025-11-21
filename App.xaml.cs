using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuadroAIPilot
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? m_window;
        
        /// <summary>
        /// Gets the main window instance
        /// </summary>
        public Window? MainWindow => m_window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            SimpleCrashLogger.Clear(); // Clear previous crash log
            SimpleCrashLogger.Log("App constructor started");
            
            try
            {
                this.InitializeComponent();
                SimpleCrashLogger.Log("InitializeComponent completed");
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "InitializeComponent");
                throw;
            }
            
            try
            {
                // Set the working directory to the application directory
                SetWorkingDirectory();
                SimpleCrashLogger.Log($"Working directory set to: {Environment.CurrentDirectory}");
                
                // Initialize logging first
                LoggingService.ConfigureLogging();
                LoggingService.LogApplicationStart();
                SimpleCrashLogger.Log("Logging configured");
                
                // Initialize global exception handling
                GlobalExceptionHandler.Initialize();
                SimpleCrashLogger.Log("Global exception handler initialized");
                
                // Configure Dependency Injection
                ServiceContainer.ConfigureServices();
                SimpleCrashLogger.Log("Services configured");

                // Load default AI Provider from settings
                var settingsManager = Managers.SettingsManager.Instance;
                State.AppState.DefaultAIProvider = settingsManager.Settings.DefaultAIProvider;
                SimpleCrashLogger.Log($"Default AI Provider: {settingsManager.Settings.DefaultAIProvider}");

                // ChatGPT Python Bridge'i başlat (arka planda, headless mode)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000); // 3 saniye bekle (UI yüklensin)
                        SimpleCrashLogger.Log("ChatGPT Python Bridge başlatılıyor (headless)...");
                        await Services.ChatGPTPythonBridge.Instance.StartBridgeAsync();
                    }
                    catch (Exception bridgeEx)
                    {
                        SimpleCrashLogger.LogException(bridgeEx, "ChatGPTBridge");
                    }
                });

                // Gemini Python Bridge'i başlat (arka planda, 5 saniye sonra - resource conflict önleme)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(8000); // 8 saniye bekle (ChatGPT'den sonra)
                        SimpleCrashLogger.Log("Gemini Python Bridge başlatılıyor...");
                        await Services.GeminiPythonBridge.Instance.StartBridgeAsync();
                    }
                    catch (Exception bridgeEx)
                    {
                        SimpleCrashLogger.LogException(bridgeEx, "GeminiBridge");
                    }
                });

                // Auto-update sistemini başlat (arka planda)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(10000); // 10 saniye bekle (UI yüklensin)
                        SimpleCrashLogger.Log("Auto-update kontrolü başlatılıyor...");
                        await Services.UpdateService.Instance.StartupUpdateCheckAsync();
                    }
                    catch (Exception updateEx)
                    {
                        SimpleCrashLogger.LogException(updateEx, "AutoUpdate");
                    }
                });
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "App.Constructor");
                // Fallback exception handling during startup
                GlobalExceptionHandler.HandleException(ex, "App.Constructor");
                throw; // Re-throw to prevent application from starting in invalid state
            }
        }

        /// <summary>
        /// Sets the working directory to the application directory to ensure relative paths work correctly
        /// </summary>
        private void SetWorkingDirectory()
        {
            try
            {
                var appDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(appDirectory))
                {
                    System.Environment.CurrentDirectory = appDirectory;
                }
            }
            catch (Exception)
            {
                // Error handled silently - non-critical operation
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            SimpleCrashLogger.Log("OnLaunched started");

            try
            {
                // Create MainWindow manually since it contains XAML controls
                SimpleCrashLogger.Log("Creating MainWindow...");
                m_window = new MainWindow();
                SimpleCrashLogger.Log("MainWindow created");

                // Window kapatma event'i - Python bridge'leri temizle
                m_window.Closed += (s, e) =>
                {
                    try
                    {
                        SimpleCrashLogger.Log("Window closed - stopping Python bridges...");
                        Services.ChatGPTPythonBridge.Instance.Dispose();
                        Services.GeminiPythonBridge.Instance.Dispose();
                    }
                    catch (Exception cleanupEx)
                    {
                        SimpleCrashLogger.LogException(cleanupEx, "PythonBridge.Cleanup");
                    }
                };

                SimpleCrashLogger.Log("Activating window...");
                m_window.Activate();
                SimpleCrashLogger.Log("Window activated");
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "App.OnLaunched");
                GlobalExceptionHandler.HandleException(ex, "App.OnLaunched");
                throw;
            }
        }


    }
}
