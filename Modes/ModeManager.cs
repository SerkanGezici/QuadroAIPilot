using QuadroAIPilot.Commands;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Modes;
using QuadroAIPilot.Services;
using QuadroAIPilot.State;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QuadroAIPilot
{
    public class ModeManager
    {
        private readonly Dictionary<AppState.UserMode, IMode> _modes;
        private IMode _active;
        private IDictationManager _dictationManager;

        public ModeManager(CommandProcessor processor)
        {
            _modes = new()
            {
                { AppState.UserMode.Command, new CommandMode(processor) },
                { AppState.UserMode.Writing, new WritingMode() },
                { AppState.UserMode.AI, new AIMode() }
            };
            _active = _modes[AppState.UserMode.Command];
            _active.Enter();
        }
        
        /// <summary>
        /// Sets the DictationManager instance
        /// </summary>
        public void SetDictationManager(IDictationManager dictationManager)
        {
            _dictationManager = dictationManager;
        }

        public void Switch(AppState.UserMode mode)
        {
            if (!_modes.ContainsKey(mode)) return;
            if (AppState.CurrentMode == mode) return;

            _active.Exit();
            _active = _modes[mode];
            _active.Enter();
            AppState.CurrentMode = mode;
            
            // Mod değişikliğinde DictationManager state'ini temizle
            if (_dictationManager != null)
            {
                _dictationManager.ResetStateForModeChange();
                LogService.LogInfo($"[ModeManager] DictationManager state temizlendi");
            }
            
            LogService.LogInfo($"[ModeManager] Switched to {mode} mode");
            
            // WebView'a mod değişikliğini bildir
            var webViewManager = ServiceLocator.GetWebViewManager();
            if (webViewManager != null)
            {
                var modeString = mode.ToString().ToLower();
                LogService.LogInfo($"[ModeManager] WebViewManager alındı, mod değişikliği bildiriliyor: {mode}");
                
                // Hemen mesaj gönder (Task.Run olmadan)
                try
                {
                    // WebView mesajı olarak gönder
                    var message = new
                    {
                        action = "modeChanged",
                        mode = mode.ToString()
                    };
                    
                    LogService.LogInfo($"[ModeManager] Sending mode change message to WebView: {mode}");
                    webViewManager.SendMessage(message);
                    LogService.LogInfo($"[ModeManager] Mode change message sent to WebView successfully");
                }
                catch (Exception ex)
                {
                    LogService.LogError($"[ModeManager] Failed to send mode change message: {ex.Message}");
                }
                
                // Script metodunu async olarak çalıştır
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // WebView'ın hazır olmasını bekle
                        await Task.Delay(100);
                        
                        // Eski script metodunu da çalıştır (geriye uyumluluk için)
                        var script = $@"
                            try {{
                                if (typeof setCurrentMode === 'function') {{ 
                                    setCurrentMode('{modeString}'); 
                                    console.log('[ModeManager] Mode set to: {modeString}'); 
                                    return 'success';
                                }} else {{ 
                                    console.error('[ModeManager] setCurrentMode function not found!'); 
                                    return 'error';
                                }}
                            }} catch (e) {{
                                console.error('[ModeManager] Script error:', e);
                                return 'script_error';
                            }}
                        ";
                        
                        var result = await webViewManager.ExecuteScriptAsync(script);
                        LogService.LogInfo($"[ModeManager] JavaScript mode update result: {result}");
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"[ModeManager] Failed to update JavaScript mode: {ex.Message}");
                    }
                });
            }
            else
            {
                LogService.LogError("[ModeManager] WebViewManager is null, cannot send mode change message!");
            }
        }

        public bool RouteSpeech(string text)
        {
            // Önce mod geçiş komutlarını kontrol et
            var lowerText = text.ToLowerInvariant().TrimEnd('.');

            // AI modu geçiş komutları
            if (lowerText.Contains("ai modu") ||
                lowerText.Contains("yapay zeka modu") ||
                lowerText.Contains("asistan modu"))
            {
                LogService.LogInfo($"[ModeManager] AI mode switch command detected: '{text}'");
                Switch(AppState.UserMode.AI);

                // TTS ile bildirim
                _ = Task.Run(async () =>
                {
                    await TextToSpeechService.SpeakTextAsync("Yapay Zeka Asistan Moduna geçildi.");
                });

                return true;
            }
            // Yazı modu geçiş komutları
            else if (lowerText.Contains("yazı modu") ||
                     lowerText.Contains("yazma modu") ||
                     lowerText.Contains("yazım modu") ||
                     lowerText.Contains("yazın modu"))
            {
                LogService.LogInfo($"[ModeManager] Writing mode switch command detected: '{text}'");
                Switch(AppState.UserMode.Writing);

                // TTS ile bildirim
                _ = Task.Run(async () =>
                {
                    await TextToSpeechService.SpeakTextAsync("Yazı moduna geçildi.");
                });

                return true;
            }
            // Komut modu geçiş komutları
            else if (lowerText.Contains("komut modu"))
            {
                LogService.LogInfo($"[ModeManager] Command mode switch command detected: '{text}'");
                Switch(AppState.UserMode.Command);

                // TTS ile bildirim
                _ = Task.Run(async () =>
                {
                    await TextToSpeechService.SpeakTextAsync("Komut moduna geçildi.");
                });

                return true;
            }

            // Mod geçiş komutu değilse aktif moda gönder
            return _active.HandleSpeech(text);
        }
    }
}