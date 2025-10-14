using System;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for dictation management
    /// </summary>
    public interface IDictationManager : IDisposable
    {
        // Properties
        bool IsActive { get; }
        bool IsProcessing { get; }
        bool IsRestarting { get; }
        
        // Configuration
        void SetWebViewManager(IWebViewManager webViewManager);
        void SetWritingModeEngine(string engine);

        // Control methods
        void StartDictation();
        void StopDictation();
        void Stop();  // WindowController için eklendi
        Task StartAsync(bool forceRestart = false);
        Task RestartDictationAsync();
        Task<bool> ToggleDictation();

        // Text processing
        void ProcessTextChanged(string text);
        void HandleTextChanged(string text);
        void ProcessText(string text);
        
        // State management
        void SetProcessingComplete();
        void UpdateTtsResponse(string response);

        // Events
        event EventHandler<string>? TextRecognized;
        event EventHandler<DictationStateChangedEventArgs>? StateChanged;
    }

    /// <summary>
    /// Dictation state change event arguments
    /// </summary>
    public class DictationStateChangedEventArgs : EventArgs
    {
        public bool IsActive { get; set; }
        public bool IsProcessing { get; set; }
        public bool IsRestarting { get; set; }
    }
}