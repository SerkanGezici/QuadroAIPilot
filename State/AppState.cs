using System;

namespace QuadroAIPilot.State
{
    /// <summary>
    /// Uygulamanın çalışma durumunu ve kullanıcı modunu takip eden sınıf
    /// </summary>
    public static class AppState
    {
        // Çalışma durumu (değişmedi)
        public enum ApplicationState
        {
            Idle,
            Listening,
            Processing,
            Executing,
            Speaking,
            Error
        }

        // === YENİ ===  Kullanıcı modu - gelecekte kolayca genişler
        public enum UserMode
        {
            Command,
            Writing
        }

        private static ApplicationState _currentState = ApplicationState.Idle;
        private static UserMode _currentMode = UserMode.Command;

        // Olaylar
        public static event EventHandler<ApplicationState> StateChanged;
        public static event EventHandler<UserMode> ModeChanged;

        public static ApplicationState CurrentState
        {
            get => _currentState;
            set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    // Thread-safe event invocation
                    var handler = StateChanged;
                    handler?.Invoke(null, _currentState);
                }
            }
        }

        public static UserMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    // Thread-safe event invocation
                    var handler = ModeChanged;
                    handler?.Invoke(null, _currentMode);
                }
            }
        }

        public static bool IsIdle => _currentState == ApplicationState.Idle;

        public static void SetError() => CurrentState = ApplicationState.Error;
        public static void Reset() => CurrentState = ApplicationState.Idle;
    }
}
