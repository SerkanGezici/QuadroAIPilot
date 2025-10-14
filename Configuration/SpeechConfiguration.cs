using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Speech and dictation configuration settings
    /// </summary>
    public class SpeechConfiguration
    {
        /// <summary>
        /// Text-to-Speech settings
        /// </summary>
        public TTSConfiguration TTS { get; set; } = new();

        /// <summary>
        /// Speech recognition settings
        /// </summary>
        public RecognitionConfiguration Recognition { get; set; } = new();

        /// <summary>
        /// Dictation behavior settings
        /// </summary>
        public DictationConfiguration Dictation { get; set; } = new();
    }

    /// <summary>
    /// Text-to-Speech configuration
    /// </summary>
    public class TTSConfiguration
    {
        /// <summary>
        /// Default TTS voice name
        /// </summary>
        public string VoiceName { get; set; } = "Tolga";

        /// <summary>
        /// Speech rate (0.1 to 2.0)
        /// </summary>
        public float Rate { get; set; } = 0.9f;

        /// <summary>
        /// Speech volume (0.0 to 1.0)
        /// </summary>
        public float Volume { get; set; } = 1.0f;

        /// <summary>
        /// Whether to use SSML for speech synthesis
        /// </summary>
        public bool UseSSML { get; set; } = false;

        /// <summary>
        /// Auto-stop TTS when new dictation starts
        /// </summary>
        public bool AutoStopOnDictation { get; set; } = true;

        /// <summary>
        /// TTS timeout in milliseconds
        /// </summary>
        public int TimeoutMs { get; set; } = 10000;
    }

    /// <summary>
    /// Speech recognition configuration
    /// </summary>
    public class RecognitionConfiguration
    {
        /// <summary>
        /// Recognition language (e.g., "tr-TR", "en-US")
        /// </summary>
        public string Language { get; set; } = "tr-TR";

        /// <summary>
        /// Confidence threshold for accepting recognition results
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.7f;

        /// <summary>
        /// Maximum recognition timeout in milliseconds
        /// </summary>
        public int TimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Auto-restart recognition after timeout
        /// </summary>
        public bool AutoRestart { get; set; } = true;

        /// <summary>
        /// Enable continuous recognition
        /// </summary>
        public bool ContinuousRecognition { get; set; } = true;
    }

    /// <summary>
    /// Dictation behavior configuration
    /// </summary>
    public class DictationConfiguration
    {
        /// <summary>
        /// Maximum retry attempts for starting dictation
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 150;

        /// <summary>
        /// Auto-clear text after successful command execution
        /// </summary>
        public bool AutoClearAfterCommand { get; set; } = true;

        /// <summary>
        /// Auto-restart dictation after command execution
        /// </summary>
        public bool AutoRestartAfterCommand { get; set; } = true;

        /// <summary>
        /// Delay before restarting dictation in milliseconds
        /// </summary>
        public int RestartDelayMs { get; set; } = 2000;

        /// <summary>
        /// Force focus on textarea before starting dictation
        /// </summary>
        public bool ForceFocusBeforeStart { get; set; } = true;
    }
}