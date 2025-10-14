using System;
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
            this.InitializeComponent();
            
            try
            {
                // Set the working directory to the application directory
                SetWorkingDirectory();
                
                // Initialize logging first
                LoggingService.ConfigureLogging();
                LoggingService.LogApplicationStart();
                
                // Initialize global exception handling
                GlobalExceptionHandler.Initialize();
                
                // Configure Dependency Injection
                ServiceContainer.ConfigureServices();
            }
            catch (Exception ex)
            {
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
            try
            {
                // Create MainWindow manually since it contains XAML controls
                m_window = new MainWindow();
                m_window.Activate();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "App.OnLaunched");
                throw;
            }
        }


    }
}
