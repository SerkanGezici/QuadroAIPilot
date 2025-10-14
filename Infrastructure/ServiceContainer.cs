using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;
using QuadroAIPilot.Services.AI;
using QuadroAIPilot.Services.WebServices;
using QuadroAIPilot.Services.WebServices.Interfaces;
using QuadroAIPilot.Commands;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Modes;
using QuadroAIPilot.Configuration;
using System;
using System.Threading.Tasks;

namespace QuadroAIPilot.Infrastructure
{
    /// <summary>
    /// Dependency Injection container configuration and service registration
    /// </summary>
    public static class ServiceContainer
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// Configures and builds the service container
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add HTTP Client Factory
            services.AddHttpClient();
            
            // Add Memory Cache
            services.AddMemoryCache();

            // Register configuration services
            RegisterConfigurationServices(services);
            
            // Register core services
            RegisterCoreServices(services);
            
            // Register command services
            RegisterCommandServices(services);
            
            // Register managers
            RegisterManagers(services);
            
            // Register UI services
            RegisterUIServices(services);
            
            // Register AI services
            RegisterAIServices(services);

            _serviceProvider = services.BuildServiceProvider();
            return _serviceProvider;
        }

        /// <summary>
        /// Gets a service from the container
        /// </summary>
        public static T GetService<T>() where T : notnull
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("Service container not configured. Call ConfigureServices() first.");

            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets a service from the container (nullable)
        /// </summary>
        public static T? GetOptionalService<T>() where T : class
        {
            return _serviceProvider?.GetService<T>();
        }

        /// <summary>
        /// Registers configuration services
        /// </summary>
        private static void RegisterConfigurationServices(IServiceCollection services)
        {
            // Configuration Manager
            services.AddSingleton<IConfigurationManager>(provider =>
            {
                var configManager = ConfigurationHelper.CreateDefaultManager();
                
                // Load configuration on startup
                Task.Run(async () =>
                {
                    await ConfigurationHelper.EnsureConfigurationFileExistsAsync();
                    await configManager.LoadConfigurationAsync();
                    configManager.StartWatching();
                });
                
                return configManager;
            });
            
            // Configuration as a service (for easy access to current config)
            services.AddSingleton<AppConfiguration>(provider =>
            {
                var configManager = provider.GetRequiredService<IConfigurationManager>();
                return configManager.Current;
            });
            
            // Configuration Service (higher-level wrapper)
            services.AddSingleton<ConfigurationService>();
        }

        /// <summary>
        /// Registers core application services
        /// </summary>
        private static void RegisterCoreServices(IServiceCollection services)
        {
            // Windows API Services (both interface and concrete class)
            services.AddSingleton<WindowsApiService>();
            services.AddSingleton<IWindowsApiService>(provider => provider.GetRequiredService<WindowsApiService>());
            
            // File Services (both interface and concrete class)
            services.AddSingleton<FileSearchService>();
            services.AddSingleton<IFileSearchService>(provider => provider.GetRequiredService<FileSearchService>());
            
            // Application Services (both interface and concrete class)
            services.AddSingleton<ApplicationService>();
            services.AddSingleton<IApplicationService>(provider => provider.GetRequiredService<ApplicationService>());
            
            // Error Handling (static, but register for DI logging)
            // services.AddSingleton<ErrorHandler>(); // ErrorHandler is static
            
            // Application Registry
            services.AddSingleton<ApplicationRegistry>();
            
            // Google Translate Service
            services.AddSingleton<GoogleTranslateService>();
            services.AddSingleton<IGoogleTranslateService>(provider => provider.GetRequiredService<GoogleTranslateService>());
            
            // Browser Integration Service
            services.AddSingleton<BrowserIntegrationService>();
            services.AddSingleton<IBrowserIntegrationService>(provider => provider.GetRequiredService<BrowserIntegrationService>());
            
            // Error Feedback Service (static service, no registration needed but included for completeness)
            // ErrorFeedbackService.GetErrorSuggestion() kullanılır
            
            // Web Content Services
            services.AddSingleton<IContentCache, ContentCacheService>();
            services.AddSingleton<IContentSummaryService, ContentSummaryService>();
            
            // Web Content Service
            services.AddSingleton<WebContentService>();
            services.AddSingleton<IWebContentService>(provider => provider.GetRequiredService<WebContentService>());
            
            // Personal Profile Service
            services.AddSingleton<PersonalProfileService>();
        }

        /// <summary>
        /// Registers command-related services
        /// </summary>
        private static void RegisterCommandServices(IServiceCollection services)
        {
            // Command Factory
            services.AddSingleton<CommandFactory>();
            
            // Command Executor (both interface and concrete class)
            services.AddSingleton<CommandExecutor>();
            services.AddSingleton<ICommandExecutor>(provider => provider.GetRequiredService<CommandExecutor>());
            
            // Command Processor (both interface and concrete class)
            services.AddSingleton<CommandProcessor>(provider =>
            {
                var executor = provider.GetRequiredService<CommandExecutor>();
                var appService = provider.GetRequiredService<ApplicationService>();
                var fileSearchService = provider.GetRequiredService<FileSearchService>();
                var intentDetector = provider.GetService<LocalIntentDetector>();
                // WebViewManager will be set later in MainWindow
                
                return new CommandProcessor(executor, appService, fileSearchService, intentDetector, null);
            });
            services.AddSingleton<ICommandProcessor>(provider => provider.GetRequiredService<CommandProcessor>());
        }

        /// <summary>
        /// Registers manager services
        /// </summary>
        private static void RegisterManagers(IServiceCollection services)
        {
            // Mode Manager
            services.AddSingleton<ModeManager>();
            
            // All other managers (DictationManager, WindowManager, UIManager, EventCoordinator, WindowController) 
            // will be created manually in MainWindow due to XAML dependencies and window handle requirements
        }

        /// <summary>
        /// Registers UI-related services
        /// </summary>
        private static void RegisterUIServices(IServiceCollection services)
        {
            // WebView Manager will be created manually in MainWindow with actual WebView2 control
            // MainWindow will be created manually due to XAML dependency
        }
        
        /// <summary>
        /// Registers AI-related services
        /// </summary>
        private static void RegisterAIServices(IServiceCollection services)
        {
            // Synonym Dictionary (Singleton)
            services.AddSingleton<SynonymDictionary>();
            
            // Intent Patterns (Singleton)
            services.AddSingleton<IntentPatterns>();
            
            // User Learning Service (Singleton - kullanıcı profilini yönetir)
            services.AddSingleton<UserLearningService>();
            
            // Command History (Singleton)
            services.AddSingleton<CommandHistory>();
            
            // Local Intent Detector (Singleton)
            services.AddSingleton<LocalIntentDetector>();
        }

        /// <summary>
        /// Disposes the service container
        /// </summary>
        public static void DisposeContainer()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
                _serviceProvider = null;
            }
        }

        /// <summary>
        /// Creates a new service scope
        /// </summary>
        public static IServiceScope CreateScope()
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("Service container not configured.");

            return _serviceProvider.CreateScope();
        }
    }
}