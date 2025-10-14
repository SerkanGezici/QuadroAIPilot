using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Command processing configuration settings
    /// </summary>
    public class CommandConfiguration
    {
        /// <summary>
        /// Command processing behavior settings
        /// </summary>
        public ProcessingConfiguration Processing { get; set; } = new();

        /// <summary>
        /// Command timeout and retry settings
        /// </summary>
        public TimeoutConfiguration Timeouts { get; set; } = new();

        /// <summary>
        /// Command filtering and validation settings
        /// </summary>
        public FilteringConfiguration Filtering { get; set; } = new();

        /// <summary>
        /// Custom command registry paths
        /// </summary>
        public List<string> CustomCommandPaths { get; set; } = new();

        /// <summary>
        /// Application-specific command settings
        /// </summary>
        public Dictionary<string, ApplicationCommandConfig> ApplicationCommands { get; set; } = new();
    }

    /// <summary>
    /// Command processing behavior configuration
    /// </summary>
    public class ProcessingConfiguration
    {
        /// <summary>
        /// Enable command preprocessing (cleanup, normalization)
        /// </summary>
        public bool EnablePreprocessing { get; set; } = true;

        /// <summary>
        /// Enable command context awareness
        /// </summary>
        public bool EnableContextAwareness { get; set; } = true;

        /// <summary>
        /// Enable command learning and adaptation
        /// </summary>
        public bool EnableLearning { get; set; } = true;

        /// <summary>
        /// Enable command suggestion system
        /// </summary>
        public bool EnableSuggestions { get; set; } = true;

        /// <summary>
        /// Maximum concurrent command executions
        /// </summary>
        public int MaxConcurrentCommands { get; set; } = 1;

        /// <summary>
        /// Enable command queuing
        /// </summary>
        public bool EnableCommandQueue { get; set; } = false;

        /// <summary>
        /// Command queue maximum size
        /// </summary>
        public int CommandQueueSize { get; set; } = 10;

        /// <summary>
        /// Enable detailed command logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;
    }

    /// <summary>
    /// Command timeout and retry configuration
    /// </summary>
    public class TimeoutConfiguration
    {
        /// <summary>
        /// Default command execution timeout in milliseconds
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// File search command timeout in milliseconds
        /// </summary>
        public int FileSearchTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Application launch timeout in milliseconds
        /// </summary>
        public int ApplicationLaunchTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// System command timeout in milliseconds
        /// </summary>
        public int SystemCommandTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Maximum retry attempts for failed commands
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 2;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Enable exponential backoff for retries
        /// </summary>
        public bool EnableExponentialBackoff { get; set; } = true;
    }

    /// <summary>
    /// Command filtering and validation configuration
    /// </summary>
    public class FilteringConfiguration
    {
        /// <summary>
        /// Enable security validation for commands
        /// </summary>
        public bool EnableSecurityValidation { get; set; } = true;

        /// <summary>
        /// Enable command sanitization
        /// </summary>
        public bool EnableCommandSanitization { get; set; } = true;

        /// <summary>
        /// Blocked command patterns (regex)
        /// </summary>
        public List<string> BlockedPatterns { get; set; } = new()
        {
            @"rm\s+-rf",
            @"del\s+/[sf]",
            @"format\s+[c-z]:",
            @"shutdown\s+/[sf]"
        };

        /// <summary>
        /// Allowed file extensions for file operations
        /// </summary>
        public List<string> AllowedFileExtensions { get; set; } = new()
        {
            ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".pptx",
            ".jpg", ".png", ".gif", ".mp4", ".mp3", ".zip"
        };

        /// <summary>
        /// Blocked application paths
        /// </summary>
        public List<string> BlockedApplicationPaths { get; set; } = new();

        /// <summary>
        /// Enable command confirmation for dangerous operations
        /// </summary>
        public bool EnableConfirmationForDangerousCommands { get; set; } = true;
    }

    /// <summary>
    /// Application-specific command configuration
    /// </summary>
    public class ApplicationCommandConfig
    {
        /// <summary>
        /// Application name or identifier
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Custom timeout for this application's commands
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Enable TTS feedback for this application
        /// </summary>
        public bool EnableTTSFeedback { get; set; } = true;

        /// <summary>
        /// Auto-restart dictation after commands for this app
        /// </summary>
        public bool AutoRestartDictation { get; set; } = true;

        /// <summary>
        /// Custom command patterns for this application
        /// </summary>
        public List<string> CustomPatterns { get; set; } = new();

        /// <summary>
        /// Application-specific settings
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }
}