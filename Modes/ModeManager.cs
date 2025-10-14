using QuadroAIPilot.Commands;
using QuadroAIPilot.Modes;
using QuadroAIPilot.State;
using System.Collections.Generic;
using System.Diagnostics;

namespace QuadroAIPilot
{
    public class ModeManager
    {
        private readonly Dictionary<AppState.UserMode, IMode> _modes;
        private IMode _active;

        public ModeManager(CommandProcessor processor)
        {
            _modes = new()
            {
                { AppState.UserMode.Command, new CommandMode(processor) },
                { AppState.UserMode.Writing, new WritingMode() },
                { AppState.UserMode.Reading, new ReadingMode() }
            };
            _active = _modes[AppState.UserMode.Command];
            _active.Enter();
        }

        public void Switch(AppState.UserMode mode)
        {
            if (!_modes.ContainsKey(mode)) return;
            if (AppState.CurrentMode == mode) return;

            _active.Exit();
            _active = _modes[mode];
            _active.Enter();
            AppState.CurrentMode = mode;
        }

        public bool RouteSpeech(string text) => _active.HandleSpeech(text);
    }
}