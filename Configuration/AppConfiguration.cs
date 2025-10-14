using System;
using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Main application configuration model
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Speech and dictation related settings
        /// </summary>
        public SpeechConfiguration Speech { get; set; } = new();

        /// <summary>
        /// UI and window related settings
        /// </summary>
        public UIConfiguration UI { get; set; } = new();

        /// <summary>
        /// Command processing related settings
        /// </summary>
        public CommandConfiguration Commands { get; set; } = new();

        /// <summary>
        /// Email integration settings
        /// </summary>
        public EmailConfiguration Email { get; set; } = new();

        /// <summary>
        /// Security and validation settings
        /// </summary>
        public SecurityConfiguration Security { get; set; } = new();

        /// <summary>
        /// Performance and logging settings
        /// </summary>
        public PerformanceConfiguration Performance { get; set; } = new();

        /// <summary>
        /// Configuration version for migration purposes
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User specific settings
        /// </summary>
        public UserConfiguration User { get; set; } = new();
    }
}