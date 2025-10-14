using System;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Command processed event arguments
    /// </summary>
    public class CommandProcessedEventArgs : EventArgs
    {
        public string CommandText { get; set; }
        public string DetectedIntent { get; set; }
        public bool Success { get; set; }
        public string ResultMessage { get; set; }
        
        public CommandProcessedEventArgs(string commandText, string detectedIntent, bool success, string resultMessage)
        {
            CommandText = commandText;
            DetectedIntent = detectedIntent;
            Success = success;
            ResultMessage = resultMessage;
        }
    }
}
