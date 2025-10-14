using System;
using System.Threading.Tasks;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for command processing
    /// </summary>
    public interface ICommandProcessor
    {
        // Command processing
        Task<bool> ProcessCommandAsync(string rawCommand);
        
        // WebView management
        void SetWebViewManager(IWebViewManager webViewManager);
        
        // Events
        event EventHandler<CommandProcessResult>? CommandProcessed;
    }

    /// <summary>
    /// Command processing result
    /// </summary>
    public class CommandProcessResult
    {
        public string CommandText { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
        public string DetectedIntent { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
    }
}