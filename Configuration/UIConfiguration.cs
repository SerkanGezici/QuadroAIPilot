using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// UI and window configuration settings
    /// </summary>
    public class UIConfiguration
    {
        /// <summary>
        /// Window positioning and behavior settings
        /// </summary>
        public WindowConfiguration Window { get; set; } = new();

        /// <summary>
        /// WebView related settings
        /// </summary>
        public WebViewConfiguration WebView { get; set; } = new();

        /// <summary>
        /// Feedback and notification settings
        /// </summary>
        public FeedbackConfiguration Feedback { get; set; } = new();

        /// <summary>
        /// Theme and appearance settings
        /// </summary>
        public ThemeConfiguration Theme { get; set; } = new();
    }

    /// <summary>
    /// Window configuration
    /// </summary>
    public class WindowConfiguration
    {
        /// <summary>
        /// AppBar width in pixels
        /// </summary>
        public int AppBarWidth { get; set; } = 300;

        /// <summary>
        /// Always keep window on top
        /// </summary>
        public bool AlwaysOnTop { get; set; } = false;

        /// <summary>
        /// Auto-hide window when not in use
        /// </summary>
        public bool AutoHide { get; set; } = false;

        /// <summary>
        /// Window transparency (0.0 to 1.0)
        /// </summary>
        public float Transparency { get; set; } = 1.0f;

        /// <summary>
        /// Snap to screen edges
        /// </summary>
        public bool SnapToEdges { get; set; } = true;

        /// <summary>
        /// Remember window position on startup
        /// </summary>
        public bool RememberPosition { get; set; } = true;

        /// <summary>
        /// Last saved window position X
        /// </summary>
        public int LastPositionX { get; set; } = -1;

        /// <summary>
        /// Last saved window position Y
        /// </summary>
        public int LastPositionY { get; set; } = -1;
    }

    /// <summary>
    /// WebView configuration
    /// </summary>
    public class WebViewConfiguration
    {
        /// <summary>
        /// Enable developer tools in WebView
        /// </summary>
        public bool EnableDevTools { get; set; } = false;

        /// <summary>
        /// WebView zoom factor (0.25 to 4.0)
        /// </summary>
        public float ZoomFactor { get; set; } = 1.0f;

        /// <summary>
        /// Enable WebView context menu
        /// </summary>
        public bool EnableContextMenu { get; set; } = false;

        /// <summary>
        /// Auto-scroll to bottom on new content
        /// </summary>
        public bool AutoScrollToBottom { get; set; } = true;

        /// <summary>
        /// Maximum content history lines
        /// </summary>
        public int MaxHistoryLines { get; set; } = 1000;

        /// <summary>
        /// Enable JavaScript console logging
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = false;
    }

    /// <summary>
    /// Feedback and notification configuration
    /// </summary>
    public class FeedbackConfiguration
    {
        /// <summary>
        /// Show success notifications
        /// </summary>
        public bool ShowSuccessNotifications { get; set; } = true;

        /// <summary>
        /// Show error notifications
        /// </summary>
        public bool ShowErrorNotifications { get; set; } = true;

        /// <summary>
        /// Success notification duration in milliseconds
        /// </summary>
        public int SuccessNotificationDurationMs { get; set; } = 2000;

        /// <summary>
        /// Error notification duration in milliseconds
        /// </summary>
        public int ErrorNotificationDurationMs { get; set; } = 3000;

        /// <summary>
        /// Enable visual feedback (icons and colors)
        /// </summary>
        public bool EnableVisualFeedback { get; set; } = true;

        /// <summary>
        /// Enable audio feedback (beeps and sounds)
        /// </summary>
        public bool EnableAudioFeedback { get; set; } = false;

        /// <summary>
        /// Show processing indicators
        /// </summary>
        public bool ShowProcessingIndicators { get; set; } = true;
    }

    /// <summary>
    /// Theme and appearance configuration
    /// </summary>
    public class ThemeConfiguration
    {
        /// <summary>
        /// Application theme (Light, Dark, Auto)
        /// </summary>
        public string Theme { get; set; } = "Auto";

        /// <summary>
        /// Custom accent color (hex format)
        /// </summary>
        public string AccentColor { get; set; } = "#0078D4";

        /// <summary>
        /// Font family for UI elements
        /// </summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Base font size in pixels
        /// </summary>
        public int FontSize { get; set; } = 14;

        /// <summary>
        /// Enable animations and transitions
        /// </summary>
        public bool EnableAnimations { get; set; } = true;

        /// <summary>
        /// Reduce motion for accessibility
        /// </summary>
        public bool ReduceMotion { get; set; } = false;
    }
}