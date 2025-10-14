using System;
using System.Threading.Tasks;
using QuadroAIPilot.Managers;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for WebView management
    /// </summary>
    public interface IWebViewManager : IDisposable
    {
        // Initialization
        Task InitializeAsync();
        
        // Content management
        Task ExecuteScript(string script);
        Task<string> ExecuteScriptAsync(string script);
        Task AppendFeedback(string message);
        Task AppendOutput(string message, bool isError = false);
        Task ClearContent();
        Task ClearTextForce();
        void UpdateText(string text);
        void UpdateDictationState(bool isActive);
        void SendWidgetUpdate(string widgetType, object data);
        Task LoadHtmlContentAsync(string htmlContent);
        
        // Edge TTS support
        Task SpeakWithEdgeTTS(string text);
        Task SendAudioStreamAsync(byte[] audioData, string format = "webm", string text = null);
        
        // Events
        event EventHandler<string>? MessageReceived;
        event EventHandler<TextareaPositionEventArgs>? TextareaPositionChanged;
        event EventHandler? OnSettingsRequested;
    }
}