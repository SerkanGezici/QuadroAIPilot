using QuadroAIPilot.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Helper utilities for configuration management
    /// </summary>
    public static class ConfigurationHelper
    {
        /// <summary>
        /// Default configuration file name
        /// </summary>
        public const string DefaultConfigFileName = "config.json";

        /// <summary>
        /// Configuration file extension
        /// </summary>
        public const string ConfigFileExtension = ".json";

        /// <summary>
        /// Gets the default configuration directory path
        /// </summary>
        public static string GetDefaultConfigDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuadroAIPilot");
        }

        /// <summary>
        /// Gets the default configuration file path
        /// </summary>
        public static string GetDefaultConfigFilePath()
        {
            return Path.Combine(GetDefaultConfigDirectory(), DefaultConfigFileName);
        }

        /// <summary>
        /// Gets the default backup directory path
        /// </summary>
        public static string GetDefaultBackupDirectory()
        {
            return Path.Combine(GetDefaultConfigDirectory(), "Backups");
        }

        /// <summary>
        /// Creates a configuration manager with default settings
        /// </summary>
        public static IConfigurationManager CreateDefaultManager()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                return new ConfigurationManager();
            }, "CreateDefaultManager", new ConfigurationManager());
        }

        /// <summary>
        /// Creates a configuration manager with custom paths
        /// </summary>
        public static IConfigurationManager CreateManager(string configFilePath, string? backupDirectory = null)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                return new ConfigurationManager(configFilePath, backupDirectory);
            }, "CreateManager", new ConfigurationManager());
        }

        /// <summary>
        /// Checks if configuration file exists
        /// </summary>
        public static bool ConfigurationFileExists(string? filePath = null)
        {
            filePath ??= GetDefaultConfigFilePath();
            return File.Exists(filePath);
        }

        /// <summary>
        /// Creates default configuration file if it doesn't exist
        /// </summary>
        public static async Task<bool> EnsureConfigurationFileExistsAsync(string? filePath = null)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                filePath ??= GetDefaultConfigFilePath();

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[ConfigurationHelper] Creating default configuration file at: {filePath}");
                    
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Create default configuration
                    var defaultConfig = new AppConfiguration();
                    var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    await File.WriteAllTextAsync(filePath, json);
                    return true;
                }

                return true;
            }, "EnsureConfigurationFileExists", false);
        }

        /// <summary>
        /// Validates a configuration file format
        /// </summary>
        public static async Task<bool> ValidateConfigurationFileAsync(string filePath)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[ConfigurationHelper] Configuration file not found: {filePath}");
                    return false;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                    return config != null;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[ConfigurationHelper] Invalid JSON in configuration file: {ex.Message}");
                    return false;
                }
            }, "ValidateConfigurationFile", false);
        }

        /// <summary>
        /// Migrates configuration from old version to new version
        /// </summary>
        public static async Task<bool> MigrateConfigurationAsync(string filePath, string currentVersion = "1.0.0")
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<AppConfiguration>(json);

                if (config == null)
                {
                    return false;
                }

                // Check if migration is needed
                if (config.Version == currentVersion)
                {
                    return true; // Already up to date
                }

                Debug.WriteLine($"[ConfigurationHelper] Migrating configuration from version {config.Version} to {currentVersion}");

                // Create backup before migration
                var backupPath = $"{filePath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(filePath, backupPath);

                // Perform migration based on version
                var migrated = await PerformVersionMigrationAsync(config, currentVersion);

                if (migrated)
                {
                    // Save migrated configuration
                    config.Version = currentVersion;
                    config.LastUpdated = DateTime.UtcNow;

                    var migratedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    await File.WriteAllTextAsync(filePath, migratedJson);
                    
                    Debug.WriteLine($"[ConfigurationHelper] Configuration migrated successfully to version {currentVersion}");
                    return true;
                }

                return false;
            }, "MigrateConfiguration", false);
        }

        /// <summary>
        /// Gets configuration file information
        /// </summary>
        public static ConfigurationFileInfo GetConfigurationFileInfo(string? filePath = null)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                filePath ??= GetDefaultConfigFilePath();

                var info = new ConfigurationFileInfo
                {
                    FilePath = filePath,
                    Exists = File.Exists(filePath)
                };

                if (info.Exists)
                {
                    var fileInfo = new FileInfo(filePath);
                    info.Size = fileInfo.Length;
                    info.LastModified = fileInfo.LastWriteTime;
                    info.Created = fileInfo.CreationTime;
                }

                return info;
            }, "GetConfigurationFileInfo", new ConfigurationFileInfo { FilePath = filePath ?? string.Empty });
        }

        /// <summary>
        /// Cleans up old backup files
        /// </summary>
        public static async Task<int> CleanupOldBackupsAsync(string? backupDirectory = null, int maxBackupsToKeep = 10)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                backupDirectory ??= GetDefaultBackupDirectory();

                if (!Directory.Exists(backupDirectory))
                {
                    return 0;
                }

                var backupFiles = Directory.GetFiles(backupDirectory, "*config*.json")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToArray();

                if (backupFiles.Length <= maxBackupsToKeep)
                {
                    return 0;
                }

                var filesToDelete = backupFiles.Skip(maxBackupsToKeep);
                int deletedCount = 0;

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ConfigurationHelper] Failed to delete backup file {file}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[ConfigurationHelper] Cleaned up {deletedCount} old backup files");
                return deletedCount;
            }, "CleanupOldBackups", 0);
        }

        #region Private Methods

        /// <summary>
        /// Performs version-specific migration logic
        /// </summary>
        private static async Task<bool> PerformVersionMigrationAsync(AppConfiguration config, string targetVersion)
        {
            // Add version-specific migration logic here
            // For now, we'll just ensure all sections exist
            
            config.Speech ??= new SpeechConfiguration();
            config.UI ??= new UIConfiguration();
            config.Commands ??= new CommandConfiguration();
            config.Email ??= new EmailConfiguration();
            config.Security ??= new SecurityConfiguration();
            config.Performance ??= new PerformanceConfiguration();
            config.User ??= new UserConfiguration();

            // Future migration logic can be added here based on version comparisons
            // Example:
            // if (Version.Parse(config.Version) < Version.Parse("1.1.0"))
            // {
            //     // Migrate to 1.1.0
            // }

            return await Task.FromResult(true);
        }

        #endregion
    }

    /// <summary>
    /// Configuration file information
    /// </summary>
    public class ConfigurationFileInfo
    {
        /// <summary>
        /// File path
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether file exists
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Formatted file size
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024:F1} KB";
                return $"{Size / (1024 * 1024):F1} MB";
            }
        }
    }
}