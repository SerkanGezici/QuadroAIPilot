using System;
using System.Threading.Tasks;
using QuadroAIPilot.Models;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Wrapper class to adapt ISystemCommand to ICommand interface
    /// </summary>
    public class CommandWrapper : ICommand
    {
        private readonly ISystemCommand _systemCommand;
        private readonly CommandContext _context;

        public CommandWrapper(ISystemCommand systemCommand, CommandContext context)
        {
            _systemCommand = systemCommand ?? throw new ArgumentNullException(nameof(systemCommand));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string CommandText => _context.RawCommand;

        public bool CanHandle(string command)
        {
            return _systemCommand.CanHandle(command);
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                var response = await _systemCommand.ExecuteAsync(_context);
                return response?.IsSuccess ?? false;
            }
            catch (Exception)
            {
                // Error is handled in the command execution layer
                return false;
            }
        }

        public void SetCommandText(string text)
        {
            _context.RawCommand = text;
        }
    }
}