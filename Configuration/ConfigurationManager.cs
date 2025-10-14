using QuadroAIPilot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Configuration management service implementation
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        #region Fields

        private readonly string _configurationFilePath;
        private readonly string _backupDirectoryPath;
        private readonly FileSystemWatcher? _fileWatcher;
        private AppConfiguration _currentConfiguration;
        private volatile bool _disposed = false;
        private volatile bool _isWatching = false;
        private readonly object _configLock = new object();

        private readonly JsonSerializerOptions _jsonOptions;

        #endregion

        #region Events

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        #endregion

        #region Properties

        public AppConfiguration Current 
        { 
            get 
            { 
                lock (_configLock) 
                { 
                    return _currentConfiguration; 
                } 
            } 
        }

        #endregion

        #region Constructor

        public ConfigurationManager(string? configurationFilePath = null, string? backupDirectoryPath = null)
        {
            _configurationFilePath = configurationFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuadroAIPilot",
                "config.json");

            _backupDirectoryPath = backupDirectoryPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuadroAIPilot",
                "Backups");

            _currentConfiguration = new AppConfiguration();

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            // Ensure directories exist
            Directory.CreateDirectory(Path.GetDirectoryName(_configurationFilePath)!);
            Directory.CreateDirectory(_backupDirectoryPath);

            // Setup file watcher
            var configDirectory = Path.GetDirectoryName(_configurationFilePath);
            if (!string.IsNullOrEmpty(configDirectory) && Directory.Exists(configDirectory))
            {
                _fileWatcher = new FileSystemWatcher(configDirectory, Path.GetFileName(_configurationFilePath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _fileWatcher.Changed += OnConfigurationFileChanged;
            }

            Debug.WriteLine($"[ConfigurationManager] Initialized with config path: {_configurationFilePath}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads configuration from file
        /// </summary>
        public async Task<bool> LoadConfigurationAsync()
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(_configurationFilePath))
                {
                    Debug.WriteLine("[ConfigurationManager] Configuration file not found, creating default configuration");
                    _currentConfiguration = new AppConfiguration();
                    await SaveConfigurationAsync();
                    return true;
                }

                var json = await File.ReadAllTextAsync(_configurationFilePath);
                var loadedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);

                if (loadedConfig != null)
                {
                    lock (_configLock)
                    {
                        _currentConfiguration = loadedConfig;
                    }
                    Debug.WriteLine("[ConfigurationManager] Configuration loaded successfully");

                    // Validate and migrate if necessary
                    var validationResult = ValidateConfiguration();
                    if (!validationResult.IsValid)
                    {
                        Debug.WriteLine($"[ConfigurationManager] Configuration validation failed: {string.Join(", ", validationResult.Errors)}");
                        
                        // Try to fix common issues
                        await FixCommonConfigurationIssuesAsync();
                    }

                    FireConfigurationChanged("Global", ConfigurationChangeType.Loaded, null, _currentConfiguration);
                    return true;
                }

                Debug.WriteLine("[ConfigurationManager] Failed to deserialize configuration");
                return false;

            }, "LoadConfiguration", false);
        }

        /// <summary>
        /// Saves current configuration to file
        /// </summary>
        public async Task<bool> SaveConfigurationAsync()
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                // Update timestamp
                _currentConfiguration.LastUpdated = DateTime.UtcNow;

                // Create backup before saving
                if (File.Exists(_configurationFilePath))
                {
                    await BackupConfigurationAsync();
                }

                var json = JsonSerializer.Serialize(_currentConfiguration, _jsonOptions);
                await File.WriteAllTextAsync(_configurationFilePath, json);

                Debug.WriteLine("[ConfigurationManager] Configuration saved successfully");
                FireConfigurationChanged("Global", ConfigurationChangeType.Saved, null, _currentConfiguration);
                return true;

            }, "SaveConfiguration", false);
        }

        /// <summary>
        /// Resets configuration to default values
        /// </summary>
        public async Task<bool> ResetToDefaultsAsync()
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                var oldConfig = _currentConfiguration;
                _currentConfiguration = new AppConfiguration();

                await SaveConfigurationAsync();

                Debug.WriteLine("[ConfigurationManager] Configuration reset to defaults");
                FireConfigurationChanged("Global", ConfigurationChangeType.Reset, oldConfig, _currentConfiguration);
                return true;

            }, "ResetToDefaults", false);
        }

        /// <summary>
        /// Updates a specific configuration section
        /// </summary>
        public async Task<bool> UpdateConfigurationAsync<T>(string sectionName, T sectionData) where T : class
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                var oldValue = GetSectionValue(sectionName);
                
                if (SetSectionValue(sectionName, sectionData))
                {
                    await SaveConfigurationAsync();
                    
                    Debug.WriteLine($"[ConfigurationManager] Section '{sectionName}' updated");
                    FireConfigurationChanged(sectionName, ConfigurationChangeType.Updated, oldValue, sectionData);
                    return true;
                }

                return false;

            }, "UpdateConfiguration", false);
        }

        /// <summary>
        /// Gets a specific configuration section
        /// </summary>
        public T GetSection<T>(string sectionName) where T : class, new()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var sectionValue = GetSectionValue(sectionName);
                
                if (sectionValue is T typedSection)
                {
                    return typedSection;
                }

                // If section doesn't exist or is wrong type, return default
                Debug.WriteLine($"[ConfigurationManager] Section '{sectionName}' not found or wrong type, returning default");
                return new T();

            }, "GetSection", new T());
        }

        /// <summary>
        /// Validates current configuration
        /// </summary>
        public ConfigurationValidationResult ValidateConfiguration()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var result = new ConfigurationValidationResult();

                // Validate Speech configuration
                ValidateSpeechConfiguration(result);

                // Validate UI configuration
                ValidateUIConfiguration(result);

                // Validate Command configuration
                ValidateCommandConfiguration(result);

                // Validate Email configuration
                ValidateEmailConfiguration(result);

                // Validate Security configuration
                ValidateSecurityConfiguration(result);

                // Validate Performance configuration
                ValidatePerformanceConfiguration(result);

                result.IsValid = result.Errors.Count == 0;
                return result;

            }, "ValidateConfiguration", new ConfigurationValidationResult { IsValid = false });
        }

        /// <summary>
        /// Creates a backup of current configuration
        /// </summary>
        public async Task<bool> BackupConfigurationAsync(string? backupPath = null)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(_configurationFilePath))
                {
                    Debug.WriteLine("[ConfigurationManager] No configuration file to backup");
                    return false;
                }

                backupPath ??= Path.Combine(_backupDirectoryPath, $"config_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(_configurationFilePath, backupPath, true);

                Debug.WriteLine($"[ConfigurationManager] Configuration backed up to: {backupPath}");
                return true;

            }, "BackupConfiguration", false);
        }

        /// <summary>
        /// Restores configuration from backup
        /// </summary>
        public async Task<bool> RestoreConfigurationAsync(string backupPath)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(backupPath))
                {
                    Debug.WriteLine($"[ConfigurationManager] Backup file not found: {backupPath}");
                    return false;
                }

                // Create backup of current config before restoring
                await BackupConfigurationAsync();

                File.Copy(backupPath, _configurationFilePath, true);
                await LoadConfigurationAsync();

                Debug.WriteLine($"[ConfigurationManager] Configuration restored from: {backupPath}");
                return true;

            }, "RestoreConfiguration", false);
        }

        /// <summary>
        /// Imports configuration from file
        /// </summary>
        public async Task<bool> ImportConfigurationAsync(string filePath)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[ConfigurationManager] Import file not found: {filePath}");
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var importedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);

                if (importedConfig != null)
                {
                    var oldConfig = _currentConfiguration;
                    _currentConfiguration = importedConfig;
                    await SaveConfigurationAsync();

                    Debug.WriteLine($"[ConfigurationManager] Configuration imported from: {filePath}");
                    FireConfigurationChanged("Global", ConfigurationChangeType.Imported, oldConfig, _currentConfiguration);
                    return true;
                }

                return false;

            }, "ImportConfiguration", false);
        }

        /// <summary>
        /// Exports configuration to file
        /// </summary>
        public async Task<bool> ExportConfigurationAsync(string filePath)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                
                var json = JsonSerializer.Serialize(_currentConfiguration, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                Debug.WriteLine($"[ConfigurationManager] Configuration exported to: {filePath}");
                return true;

            }, "ExportConfiguration", false);
        }

        /// <summary>
        /// Starts watching for external configuration file changes
        /// </summary>
        public void StartWatching()
        {
            ErrorHandler.SafeExecute(() =>
            {
                if (_fileWatcher != null && !_isWatching)
                {
                    _fileWatcher.EnableRaisingEvents = true;
                    _isWatching = true;
                    Debug.WriteLine("[ConfigurationManager] File watching started");
                }
            }, "StartWatching");
        }

        /// <summary>
        /// Stops watching for configuration file changes
        /// </summary>
        public void StopWatching()
        {
            ErrorHandler.SafeExecute(() =>
            {
                if (_fileWatcher != null && _isWatching)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _isWatching = false;
                    Debug.WriteLine("[ConfigurationManager] File watching stopped");
                }
            }, "StopWatching");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles external configuration file changes
        /// </summary>
        private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            // Use Task.Run to handle async work safely in event handler
            _ = Task.Run(async () =>
            {
                await ErrorHandler.SafeExecuteAsync(async () =>
                {
                    // Debounce file changes (wait a bit to avoid multiple events)
                    await Task.Delay(500);

                    AppConfiguration oldConfig;
                    lock (_configLock)
                    {
                        oldConfig = _currentConfiguration;
                    }
                    
                    await LoadConfigurationAsync();

                    Debug.WriteLine("[ConfigurationManager] Configuration file changed externally, reloaded");
                    
                    AppConfiguration newConfig;
                    lock (_configLock)
                    {
                        newConfig = _currentConfiguration;
                    }
                    
                    FireConfigurationChanged("Global", ConfigurationChangeType.ExternalChange, oldConfig, newConfig);

                }, "OnConfigurationFileChanged");
            });
        }

        /// <summary>
        /// Fires configuration changed event
        /// </summary>
        private void FireConfigurationChanged(string sectionName, ConfigurationChangeType changeType, object? oldValue, object? newValue)
        {
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                SectionName = sectionName,
                ChangeType = changeType,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        /// <summary>
        /// Gets section value by name using reflection
        /// </summary>
        private object? GetSectionValue(string sectionName)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var property = typeof(AppConfiguration).GetProperty(sectionName);
                if (property == null) return null;
                
                lock (_configLock)
                {
                    return property.GetValue(_currentConfiguration);
                }
            }, "GetSectionValue", null);
        }

        /// <summary>
        /// Sets section value by name using reflection
        /// </summary>
        private bool SetSectionValue(string sectionName, object value)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                var property = typeof(AppConfiguration).GetProperty(sectionName);
                if (property?.CanWrite == true)
                {
                    lock (_configLock)
                    {
                        property.SetValue(_currentConfiguration, value);
                    }
                    return true;
                }
                return false;
            }, "SetSectionValue", false);
        }

        /// <summary>
        /// Fixes common configuration issues
        /// </summary>
        private async Task FixCommonConfigurationIssuesAsync()
        {
            // Fix null sections
            if (_currentConfiguration.Speech == null)
                _currentConfiguration.Speech = new SpeechConfiguration();
            
            if (_currentConfiguration.UI == null)
                _currentConfiguration.UI = new UIConfiguration();
            
            if (_currentConfiguration.Commands == null)
                _currentConfiguration.Commands = new CommandConfiguration();
                
            if (_currentConfiguration.Email == null)
                _currentConfiguration.Email = new EmailConfiguration();
                
            if (_currentConfiguration.Security == null)
                _currentConfiguration.Security = new SecurityConfiguration();
                
            if (_currentConfiguration.Performance == null)
                _currentConfiguration.Performance = new PerformanceConfiguration();
                
            if (_currentConfiguration.User == null)
                _currentConfiguration.User = new UserConfiguration();

            await SaveConfigurationAsync();
        }

        #region Validation Methods

        private void ValidateSpeechConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.Speech?.TTS != null)
            {
                var tts = _currentConfiguration.Speech.TTS;
                
                if (tts.Rate < 0.1f || tts.Rate > 2.0f)
                {
                    result.Errors.Add("TTS Rate must be between 0.1 and 2.0");
                    result.FailedSections.Add("Speech.TTS");
                }
                
                if (tts.Volume < 0.0f || tts.Volume > 1.0f)
                {
                    result.Errors.Add("TTS Volume must be between 0.0 and 1.0");
                    result.FailedSections.Add("Speech.TTS");
                }
            }
        }

        private void ValidateUIConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.UI?.Window != null)
            {
                var window = _currentConfiguration.UI.Window;
                
                if (window.AppBarWidth < 100 || window.AppBarWidth > 1000)
                {
                    result.Warnings.Add("AppBar width should be between 100 and 1000 pixels");
                }
                
                if (window.Transparency < 0.0f || window.Transparency > 1.0f)
                {
                    result.Errors.Add("Window transparency must be between 0.0 and 1.0");
                    result.FailedSections.Add("UI.Window");
                }
            }
        }

        private void ValidateCommandConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.Commands?.Processing != null)
            {
                var processing = _currentConfiguration.Commands.Processing;
                
                if (processing.MaxConcurrentCommands < 1 || processing.MaxConcurrentCommands > 10)
                {
                    result.Warnings.Add("Max concurrent commands should be between 1 and 10");
                }
            }
        }

        private void ValidateEmailConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.Email?.AccountManagement != null)
            {
                var accountMgmt = _currentConfiguration.Email.AccountManagement;
                
                if (accountMgmt.MaxAccountsToLoad < 1 || accountMgmt.MaxAccountsToLoad > 50)
                {
                    result.Warnings.Add("Max accounts to load should be between 1 and 50");
                }
            }
        }

        private void ValidateSecurityConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.Security?.Validation != null)
            {
                var validation = _currentConfiguration.Security.Validation;
                
                if (validation.MaxCommandLength < 10 || validation.MaxCommandLength > 10000)
                {
                    result.Warnings.Add("Max command length should be between 10 and 10000");
                }
            }
        }

        private void ValidatePerformanceConfiguration(ConfigurationValidationResult result)
        {
            if (_currentConfiguration.Performance?.Memory != null)
            {
                var memory = _currentConfiguration.Performance.Memory;
                
                if (memory.MaxMemoryUsageMB < 100 || memory.MaxMemoryUsageMB > 2048)
                {
                    result.Warnings.Add("Max memory usage should be between 100MB and 2GB");
                }
            }
        }

        #endregion

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
                    StopWatching();
                    _fileWatcher?.Dispose();
                    _disposed = true;
                    Debug.WriteLine("[ConfigurationManager] Disposed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConfigurationManager] Dispose error: {ex.Message}");
                }
            }
        }

        #endregion
    }
}