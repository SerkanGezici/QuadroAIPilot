using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Interface for configuration management service
    /// </summary>
    public interface IConfigurationManager : IDisposable
    {
        /// <summary>
        /// Gets the current application configuration
        /// </summary>
        AppConfiguration Current { get; }

        /// <summary>
        /// Event fired when configuration changes
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        /// <summary>
        /// Loads configuration from file
        /// </summary>
        Task<bool> LoadConfigurationAsync();

        /// <summary>
        /// Saves current configuration to file
        /// </summary>
        Task<bool> SaveConfigurationAsync();

        /// <summary>
        /// Resets configuration to default values
        /// </summary>
        Task<bool> ResetToDefaultsAsync();

        /// <summary>
        /// Updates a specific configuration section
        /// </summary>
        Task<bool> UpdateConfigurationAsync<T>(string sectionName, T sectionData) where T : class;

        /// <summary>
        /// Gets a specific configuration section
        /// </summary>
        T GetSection<T>(string sectionName) where T : class, new();

        /// <summary>
        /// Validates current configuration
        /// </summary>
        ConfigurationValidationResult ValidateConfiguration();

        /// <summary>
        /// Creates a backup of current configuration
        /// </summary>
        Task<bool> BackupConfigurationAsync(string? backupPath = null);

        /// <summary>
        /// Restores configuration from backup
        /// </summary>
        Task<bool> RestoreConfigurationAsync(string backupPath);

        /// <summary>
        /// Imports configuration from file
        /// </summary>
        Task<bool> ImportConfigurationAsync(string filePath);

        /// <summary>
        /// Exports configuration to file
        /// </summary>
        Task<bool> ExportConfigurationAsync(string filePath);

        /// <summary>
        /// Watches for external configuration file changes
        /// </summary>
        void StartWatching();

        /// <summary>
        /// Stops watching for configuration file changes
        /// </summary>
        void StopWatching();
    }

    /// <summary>
    /// Configuration changed event arguments
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Section that changed
        /// </summary>
        public string SectionName { get; set; } = string.Empty;

        /// <summary>
        /// Type of change
        /// </summary>
        public ConfigurationChangeType ChangeType { get; set; }

        /// <summary>
        /// Old configuration value (for updates)
        /// </summary>
        public object? OldValue { get; set; }

        /// <summary>
        /// New configuration value
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// Timestamp of the change
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of configuration changes
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>
        /// Configuration was loaded
        /// </summary>
        Loaded,

        /// <summary>
        /// Configuration was saved
        /// </summary>
        Saved,

        /// <summary>
        /// Configuration section was updated
        /// </summary>
        Updated,

        /// <summary>
        /// Configuration was reset to defaults
        /// </summary>
        Reset,

        /// <summary>
        /// Configuration was imported
        /// </summary>
        Imported,

        /// <summary>
        /// Configuration file was changed externally
        /// </summary>
        ExternalChange
    }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// Whether configuration is valid
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Sections that failed validation
        /// </summary>
        public List<string> FailedSections { get; set; } = new();
    }
}