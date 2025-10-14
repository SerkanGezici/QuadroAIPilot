using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// User-specific configuration settings
    /// </summary>
    public class UserConfiguration
    {
        /// <summary>
        /// User preferences and personalization
        /// </summary>
        public PersonalizationConfiguration Personalization { get; set; } = new();

        /// <summary>
        /// User habits and learning data
        /// </summary>
        public LearningConfiguration Learning { get; set; } = new();

        /// <summary>
        /// Accessibility settings
        /// </summary>
        public AccessibilityConfiguration Accessibility { get; set; } = new();

        /// <summary>
        /// User workflow and shortcuts
        /// </summary>
        public WorkflowConfiguration Workflow { get; set; } = new();

        /// <summary>
        /// News preferences and category selections
        /// </summary>
        public NewsPreferences NewsPreferences { get; set; } = new();

        /// <summary>
        /// Weather API key for OpenWeatherMap
        /// </summary>
        public string WeatherApiKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// User personalization configuration
    /// </summary>
    public class PersonalizationConfiguration
    {
        /// <summary>
        /// User's display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// User's preferred language
        /// </summary>
        public string PreferredLanguage { get; set; } = "tr-TR";

        /// <summary>
        /// User's time zone
        /// </summary>
        public string TimeZone { get; set; } = "Turkey Standard Time";

        /// <summary>
        /// Date format preference
        /// </summary>
        public string DateFormat { get; set; } = "dd.MM.yyyy";

        /// <summary>
        /// Time format preference
        /// </summary>
        public string TimeFormat { get; set; } = "HH:mm";

        /// <summary>
        /// Number format preference
        /// </summary>
        public string NumberFormat { get; set; } = "tr-TR";

        /// <summary>
        /// Custom greeting message
        /// </summary>
        public string CustomGreeting { get; set; } = string.Empty;

        /// <summary>
        /// User's favorite commands
        /// </summary>
        public List<string> FavoriteCommands { get; set; } = new();

        /// <summary>
        /// Recently used commands history
        /// </summary>
        public List<string> RecentCommands { get; set; } = new();

        /// <summary>
        /// Maximum recent commands to remember
        /// </summary>
        public int MaxRecentCommands { get; set; } = 20;
    }

    /// <summary>
    /// Learning and adaptation configuration
    /// </summary>
    public class LearningConfiguration
    {
        /// <summary>
        /// Enable user behavior learning
        /// </summary>
        public bool EnableLearning { get; set; } = true;

        /// <summary>
        /// Command usage statistics
        /// </summary>
        public Dictionary<string, CommandUsageStats> CommandUsage { get; set; } = new();

        /// <summary>
        /// Application usage patterns
        /// </summary>
        public Dictionary<string, ApplicationUsageStats> ApplicationUsage { get; set; } = new();

        /// <summary>
        /// Custom command aliases
        /// </summary>
        public Dictionary<string, string> CommandAliases { get; set; } = new();

        /// <summary>
        /// Voice command adaptations
        /// </summary>
        public Dictionary<string, string> VoiceAdaptations { get; set; } = new();

        /// <summary>
        /// Learning data retention period in days
        /// </summary>
        public int LearningDataRetentionDays { get; set; } = 365;

        /// <summary>
        /// Minimum usage count for pattern recognition
        /// </summary>
        public int MinUsageCountForPattern { get; set; } = 5;

        /// <summary>
        /// Enable suggestion system based on patterns
        /// </summary>
        public bool EnableSuggestions { get; set; } = true;
    }

    /// <summary>
    /// Accessibility configuration
    /// </summary>
    public class AccessibilityConfiguration
    {
        /// <summary>
        /// Enable high contrast mode
        /// </summary>
        public bool EnableHighContrast { get; set; } = false;

        /// <summary>
        /// Enable large fonts
        /// </summary>
        public bool EnableLargeFonts { get; set; } = false;

        /// <summary>
        /// Font size multiplier
        /// </summary>
        public float FontSizeMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Enable screen reader support
        /// </summary>
        public bool EnableScreenReaderSupport { get; set; } = false;

        /// <summary>
        /// Enable keyboard navigation
        /// </summary>
        public bool EnableKeyboardNavigation { get; set; } = true;

        /// <summary>
        /// Enable voice feedback
        /// </summary>
        public bool EnableVoiceFeedback { get; set; } = true;

        /// <summary>
        /// Voice feedback speed (0.5 to 2.0)
        /// </summary>
        public float VoiceFeedbackSpeed { get; set; } = 1.0f;

        /// <summary>
        /// Enable visual feedback for audio alerts
        /// </summary>
        public bool EnableVisualAudioFeedback { get; set; } = false;

        /// <summary>
        /// Reduce motion and animations
        /// </summary>
        public bool ReduceMotion { get; set; } = false;

        /// <summary>
        /// Enable sticky keys simulation
        /// </summary>
        public bool EnableStickyKeys { get; set; } = false;

        /// <summary>
        /// Keyboard repeat delay in milliseconds
        /// </summary>
        public int KeyboardRepeatDelay { get; set; } = 500;
    }

    /// <summary>
    /// Workflow and shortcuts configuration
    /// </summary>
    public class WorkflowConfiguration
    {
        /// <summary>
        /// Custom keyboard shortcuts
        /// </summary>
        public Dictionary<string, string> CustomShortcuts { get; set; } = new();

        /// <summary>
        /// Workflow templates
        /// </summary>
        public List<WorkflowTemplate> WorkflowTemplates { get; set; } = new();

        /// <summary>
        /// Quick action commands
        /// </summary>
        public List<string> QuickActions { get; set; } = new();

        /// <summary>
        /// Auto-complete suggestions
        /// </summary>
        public List<string> AutoCompleteSuggestions { get; set; } = new();

        /// <summary>
        /// Default workspace settings
        /// </summary>
        public WorkspaceSettings DefaultWorkspace { get; set; } = new();

        /// <summary>
        /// Enable workflow automation
        /// </summary>
        public bool EnableWorkflowAutomation { get; set; } = false;

        /// <summary>
        /// Automation rules
        /// </summary>
        public List<AutomationRule> AutomationRules { get; set; } = new();
    }

    /// <summary>
    /// Command usage statistics
    /// </summary>
    public class CommandUsageStats
    {
        /// <summary>
        /// Command name or pattern
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Number of times used
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Last used timestamp
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Average execution time in milliseconds
        /// </summary>
        public double AverageExecutionTimeMs { get; set; } = 0;

        /// <summary>
        /// Success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate { get; set; } = 0;

        /// <summary>
        /// User rating for this command (1-5)
        /// </summary>
        public int UserRating { get; set; } = 0;
    }

    /// <summary>
    /// Application usage statistics
    /// </summary>
    public class ApplicationUsageStats
    {
        /// <summary>
        /// Application name
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Number of times launched
        /// </summary>
        public int LaunchCount { get; set; } = 0;

        /// <summary>
        /// Total usage time in minutes
        /// </summary>
        public long TotalUsageMinutes { get; set; } = 0;

        /// <summary>
        /// Last launched timestamp
        /// </summary>
        public DateTime LastLaunched { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Preferred launch method
        /// </summary>
        public string PreferredLaunchMethod { get; set; } = string.Empty;

        /// <summary>
        /// Application-specific settings
        /// </summary>
        public Dictionary<string, object> ApplicationSettings { get; set; } = new();
    }

    /// <summary>
    /// Workflow template
    /// </summary>
    public class WorkflowTemplate
    {
        /// <summary>
        /// Template name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Template description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Sequence of commands in this workflow
        /// </summary>
        public List<string> Commands { get; set; } = new();

        /// <summary>
        /// Trigger conditions for this workflow
        /// </summary>
        public List<string> Triggers { get; set; } = new();

        /// <summary>
        /// Whether this template is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Workspace settings
    /// </summary>
    public class WorkspaceSettings
    {
        /// <summary>
        /// Default applications to launch on startup
        /// </summary>
        public List<string> StartupApplications { get; set; } = new();

        /// <summary>
        /// Preferred window arrangement
        /// </summary>
        public string WindowArrangement { get; set; } = "Default";

        /// <summary>
        /// Default folders for file operations
        /// </summary>
        public Dictionary<string, string> DefaultFolders { get; set; } = new();

        /// <summary>
        /// Environment variables
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    }

    /// <summary>
    /// Automation rule
    /// </summary>
    public class AutomationRule
    {
        /// <summary>
        /// Rule name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Trigger condition
        /// </summary>
        public string Trigger { get; set; } = string.Empty;

        /// <summary>
        /// Action to execute
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Whether this rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Rule priority (higher number = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// News preferences configuration
    /// </summary>
    public class NewsPreferences
    {
        /// <summary>
        /// Selected news categories (11 fixed categories)
        /// </summary>
        public List<string> SelectedCategories { get; set; } = new List<string>
        {
            "Genel", // Varsayılan olarak Genel haberleri seçili
            "Spor",
            "Ekonomi"
        };

        /// <summary>
        /// Custom 12th category selected from dropdown
        /// Available options: Otomobil, Yerel, Bilim, Sanat, Eğitim, Astroloji
        /// </summary>
        public string CustomCategory { get; set; } = "Otomobil";

        /// <summary>
        /// Selected news sources by user
        /// </summary>
        public List<string> SelectedNewsSources { get; set; } = new List<string>
        {
            // Varsayılan olarak bazı popüler kaynaklar seçili
            "CNN Türk",
            "Hürriyet",
            "Milliyet",
            "Sabah",
            "NTV"
        };

        /// <summary>
        /// Show all sources or only selected ones
        /// </summary>
        public bool ShowAllSources { get; set; } = true;

        /// <summary>
        /// Automatically translate English sources to Turkish
        /// </summary>
        public bool AutoTranslateEnglishSources { get; set; } = true;

        /// <summary>
        /// Maximum number of news items per category
        /// </summary>
        public int MaxNewsPerCategory { get; set; } = 5;

        /// <summary>
        /// Enable automatic news refresh
        /// </summary>
        public bool EnableAutoRefresh { get; set; } = false;

        /// <summary>
        /// Auto refresh interval in minutes
        /// </summary>
        public int AutoRefreshIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Show news source in headlines
        /// </summary>
        public bool ShowNewsSource { get; set; } = true;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }
}