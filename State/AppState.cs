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
            Writing,
            AI
        }

        // AI Provider (ChatGPT, Claude, ilerde Gemini/Codex eklenebilir)
        public enum AIProvider
        {
            ChatGPT,    // Python browser automation (default)
            Claude,     // Claude CLI (fallback)
            Gemini,     // İleride eklenebilir
            Codex       // İleride eklenebilir
        }

        private static ApplicationState _currentState = ApplicationState.Idle;
        private static UserMode _currentMode = UserMode.Command;
        private static AIProvider _currentAIProvider = AIProvider.ChatGPT;  // Default: ChatGPT
        private static AIProvider _defaultAIProvider = AIProvider.ChatGPT;  // Settings'den yüklenir

        // Olaylar
        public static event EventHandler<ApplicationState> StateChanged;
        public static event EventHandler<UserMode> ModeChanged;
        public static event EventHandler<AIProvider> AIProviderChanged;

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

        /// <summary>
        /// Aktif AI Provider (sesli komutla geçici değiştirilebilir)
        /// </summary>
        public static AIProvider CurrentAIProvider
        {
            get => _currentAIProvider;
            set
            {
                if (_currentAIProvider != value)
                {
                    _currentAIProvider = value;
                    var handler = AIProviderChanged;
                    handler?.Invoke(null, _currentAIProvider);
                }
            }
        }

        /// <summary>
        /// Varsayılan AI Provider (Settings'den yüklenir, uygulama başlangıcında set edilir)
        /// </summary>
        public static AIProvider DefaultAIProvider
        {
            get => _defaultAIProvider;
            set
            {
                _defaultAIProvider = value;
                // Default değişince current'i de HER ZAMAN güncelle
                // (Kullanıcı Settings'ten seçtiyse hemen uygulanmalı)
                CurrentAIProvider = value;
            }
        }

        /// <summary>
        /// AI Provider'ı default'a sıfırla (mod değiştirmeden sonra)
        /// </summary>
        public static void ResetAIProviderToDefault()
        {
            CurrentAIProvider = _defaultAIProvider;
        }
    }
}
