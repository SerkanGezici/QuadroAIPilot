using System;
using System.Threading.Tasks;
using QuadroAIPilot.Commands;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for command execution
    /// </summary>
    public interface ICommandExecutor
    {
        // Command execution
        Task<bool> ExecuteAsync(ICommand command);
        Task<bool> ExecuteCommandTextAsync(string commandText);
        
        // Configuration
        void SetMainWindowHandle(IntPtr handle);
        
        // Events
        event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
    }

    /// <summary>
    /// Event arguments for command execution
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public ICommand Command { get; set; } = null!;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}