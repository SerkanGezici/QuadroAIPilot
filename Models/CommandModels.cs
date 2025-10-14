using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroAIPilot.Models
{
    /// <summary>
    /// Interface for system commands
    /// </summary>
    public interface ISystemCommand
    {
        bool CanHandle(string command);
        Task<CommandResponse> ExecuteAsync(CommandContext context);
    }

    /// <summary>
    /// Command execution context
    /// </summary>
    public class CommandContext
    {
        public string RawCommand { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public object State { get; set; }
    }

    /// <summary>
    /// Command execution response
    /// </summary>
    public class CommandResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string VoiceOutput { get; set; }
        public string HtmlContent { get; set; }
        public CommandActionType ActionType { get; set; } = CommandActionType.None;
        public object Data { get; set; }
        public Exception Error { get; set; }
    }

    /// <summary>
    /// Command action types
    /// </summary>
    public enum CommandActionType
    {
        None,
        ShowHtml,
        OpenUrl,
        PlayAudio,
        ShowNotification,
        ExecuteSystem,
        NavigateApp
    }
}