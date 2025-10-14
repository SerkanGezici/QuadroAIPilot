using QuadroAIPilot.Configuration;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Service for accessing and managing application configuration
    /// </summary>
    public class ConfigurationService : IDisposable
    {
        #region Fields

        private readonly IConfigurationManager _configurationManager;
        private bool _disposed = false;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when configuration changes
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current application configuration
        /// </summary>
        public AppConfiguration Current => _configurationManager.Current;

        /// <summary>
        /// Gets speech configuration
        /// </summary>
        public SpeechConfiguration Speech => Current.Speech;

        /// <summary>
        /// Gets UI configuration
        /// </summary>
        public UIConfiguration UI => Current.UI;

        /// <summary>
        /// Gets command configuration
        /// </summary>
        public CommandConfiguration Commands => Current.Commands;

        /// <summary>
        /// Gets email configuration
        /// </summary>
        public EmailConfiguration Email => Current.Email;

        /// <summary>
        /// Gets security configuration
        /// </summary>
        public SecurityConfiguration Security => Current.Security;

        /// <summary>
        /// Gets performance configuration
        /// </summary>
        public PerformanceConfiguration Performance => Current.Performance;

        /// <summary>
        /// Gets user configuration
        /// </summary>
        public UserConfiguration User => Current.User;

        #endregion

        #region Constructor

        public ConfigurationService(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            
            // Subscribe to configuration changes
            _configurationManager.ConfigurationChanged += OnConfigurationChanged;
            
            Debug.WriteLine("[ConfigurationService] Initialized");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Saves current configuration
        /// </summary>
        public async Task<bool> SaveAsync()
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                return await _configurationManager.SaveConfigurationAsync();
            }, "SaveConfiguration", false);
        }

        /// <summary>
        /// Reloads configuration from file
        /// </summary>
        public async Task<bool> ReloadAsync()
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                return await _configurationManager.LoadConfigurationAsync();
            }, "ReloadConfiguration", false);
        }

        /// <summary>
        /// Updates speech configuration
        /// </summary>
        public async Task<bool> UpdateSpeechConfigAsync(SpeechConfiguration speechConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("Speech", speechConfig);
        }

        /// <summary>
        /// Updates UI configuration
        /// </summary>
        public async Task<bool> UpdateUIConfigAsync(UIConfiguration uiConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("UI", uiConfig);
        }

        /// <summary>
        /// Updates command configuration
        /// </summary>
        public async Task<bool> UpdateCommandConfigAsync(CommandConfiguration commandConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("Commands", commandConfig);
        }

        /// <summary>
        /// Updates email configuration
        /// </summary>
        public async Task<bool> UpdateEmailConfigAsync(EmailConfiguration emailConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("Email", emailConfig);
        }

        /// <summary>
        /// Updates security configuration
        /// </summary>
        public async Task<bool> UpdateSecurityConfigAsync(SecurityConfiguration securityConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("Security", securityConfig);
        }

        /// <summary>
        /// Updates performance configuration
        /// </summary>
        public async Task<bool> UpdatePerformanceConfigAsync(PerformanceConfiguration performanceConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("Performance", performanceConfig);
        }

        /// <summary>
        /// Updates user configuration
        /// </summary>
        public async Task<bool> UpdateUserConfigAsync(UserConfiguration userConfig)
        {
            return await _configurationManager.UpdateConfigurationAsync("User", userConfig);
        }

        /// <summary>
        /// Updates news preferences within user configuration
        /// </summary>
        public async Task<bool> UpdateNewsPreferencesAsync(NewsPreferences newsPreferences)
        {
            var userConfig = Current.User;
            userConfig.NewsPreferences = newsPreferences;
            return await UpdateUserConfigAsync(userConfig);
        }

        /// <summary>
        /// Resets configuration to defaults
        /// </summary>
        public async Task<bool> ResetToDefaultsAsync()
        {
            return await _configurationManager.ResetToDefaultsAsync();
        }

        /// <summary>
        /// Creates a backup of current configuration
        /// </summary>
        public async Task<bool> BackupAsync(string? backupPath = null)
        {
            return await _configurationManager.BackupConfigurationAsync(backupPath);
        }

        /// <summary>
        /// Validates current configuration
        /// </summary>
        public ConfigurationValidationResult Validate()
        {
            return _configurationManager.ValidateConfiguration();
        }

        /// <summary>
        /// Gets TTS voice name with fallback
        /// </summary>
        public string GetTTSVoiceName()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                return !string.IsNullOrEmpty(Speech.TTS.VoiceName) ? Speech.TTS.VoiceName : "Tolga";
            }, "GetTTSVoiceName", "Tolga");
        }

        /// <summary>
        /// Gets TTS rate with validation
        /// </summary>
        public float GetTTSRate()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var rate = Speech.TTS.Rate;
                return rate >= 0.1f && rate <= 2.0f ? rate : 0.9f;
            }, "GetTTSRate", 0.9f);
        }

        /// <summary>
        /// Gets window AppBar width with validation
        /// </summary>
        public int GetAppBarWidth()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var width = UI.Window.AppBarWidth;
                return width >= 100 && width <= 1000 ? width : 300;
            }, "GetAppBarWidth", 300);
        }

        /// <summary>
        /// Gets maximum command length with validation
        /// </summary>
        public int GetMaxCommandLength()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var maxLength = Security.Validation.MaxCommandLength;
                return maxLength >= 10 && maxLength <= 10000 ? maxLength : 1000;
            }, "GetMaxCommandLength", 1000);
        }

        /// <summary>
        /// Checks if feature is enabled
        /// </summary>
        public bool IsFeatureEnabled(string featureName)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                return featureName.ToLower() switch
                {
                    "tts" => true,
                    "dictation" => true,
                    "commands" => true,
                    "email" => Email.Outlook.EnableOutlookIntegration,
                    "security" => Security.Validation.EnableInputSanitization,
                    "logging" => Performance.Logging.EnableLogging,
                    "learning" => User.Learning.EnableLearning,
                    _ => false
                };
            }, "IsFeatureEnabled", false);
        }

        /// <summary>
        /// Gets user preference with fallback
        /// </summary>
        public T GetUserPreference<T>(string preferenceName, T defaultValue)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                // This could be extended to support dynamic user preferences
                // For now, return the default value
                return defaultValue;
            }, "GetUserPreference", defaultValue);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles configuration change events
        /// </summary>
        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            ErrorHandler.SafeExecute(() =>
            {
                Debug.WriteLine($"[ConfigurationService] Configuration changed: {e.SectionName} ({e.ChangeType})");
                
                // Forward the event - thread safe
                var handler = ConfigurationChanged;
                handler?.Invoke(this, e);
                
                // Perform any necessary actions based on configuration changes
                HandleConfigurationChange(e);
                
            }, "OnConfigurationChanged");
        }

        /// <summary>
        /// Handles specific configuration changes
        /// </summary>
        private void HandleConfigurationChange(ConfigurationChangedEventArgs e)
        {
            switch (e.SectionName.ToLower())
            {
                case "speech":
                    Debug.WriteLine("[ConfigurationService] Speech configuration changed");
                    // Could notify TTS service of changes
                    break;
                    
                case "ui":
                    Debug.WriteLine("[ConfigurationService] UI configuration changed");
                    // Could notify UI components of changes
                    break;
                    
                case "commands":
                    Debug.WriteLine("[ConfigurationService] Command configuration changed");
                    // Could notify command processor of changes
                    break;
                    
                case "security":
                    Debug.WriteLine("[ConfigurationService] Security configuration changed");
                    // Could update security validators
                    break;
                    
                default:
                    Debug.WriteLine($"[ConfigurationService] General configuration change: {e.SectionName}");
                    break;
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Unsubscribe from events
                    if (_configurationManager != null)
                    {
                        _configurationManager.ConfigurationChanged -= OnConfigurationChanged;
                    }
                    
                    _disposed = true;
                    Debug.WriteLine("[ConfigurationService] Disposed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConfigurationService] Dispose error: {ex.Message}");
                }
            }
        }

        #endregion
    }
}