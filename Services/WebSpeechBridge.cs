using System;
using System.Threading.Tasks;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.State;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Web Speech API ile C# arasında köprü görevi gören servis
    /// </summary>
    public class WebSpeechBridge
    {
        private readonly IWebViewManager _webViewManager;
        private readonly IDictationManager _dictationManager;
        private readonly ICommandProcessor _commandProcessor;
        
        private bool _isWebSpeechActive = false;
        
        public event EventHandler<bool>? StateChanged;
        
        public bool IsActive => _isWebSpeechActive;
        
        public WebSpeechBridge(IWebViewManager webViewManager, IDictationManager dictationManager)
        {
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            _dictationManager = dictationManager ?? throw new ArgumentNullException(nameof(dictationManager));
            _commandProcessor = ServiceContainer.GetService<ICommandProcessor>();
        }
        
        /// <summary>
        /// Web Speech API'yi başlat
        /// </summary>
        public async Task StartWebSpeechAsync()
        {
            try
            {
                LogService.LogDebug("[WebSpeechBridge] Web Speech API başlatılıyor...");
                
                // Önce mikrofon iznini kontrol et
                bool hasMicPermission = await CheckMicrophonePermission();
                if (!hasMicPermission)
                {
                    LogService.LogDebug("[WebSpeechBridge] Mikrofon izni yok");
                    // Kullanıcıya bildirim göster
                    await _webViewManager.ExecuteScriptAsync(
                        "showNotification('Mikrofon izni gerekli. Lütfen tarayıcı ayarlarından mikrofon iznini verin.', 'error')");
                    return;
                }
                
                // JavaScript'e başlatma komutu gönder
                await _webViewManager.ExecuteScriptAsync("startWebSpeechRecognition()");
                
                _isWebSpeechActive = true;
                StateChanged?.Invoke(this, true);
                
                LogService.LogDebug("[WebSpeechBridge] Web Speech API başlatıldı");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[WebSpeechBridge] Web Speech API başlatma hatası: {ex.Message}");
                // Hata bildirimi göster
                await _webViewManager.ExecuteScriptAsync(
                    $"showNotification('Ses tanıma başlatılamadı: {ex.Message}', 'error')");
            }
        }
        
        /// <summary>
        /// Web Speech API'yi durdur
        /// </summary>
        public async Task StopWebSpeechAsync()
        {
            try
            {
                LogService.LogDebug("[WebSpeechBridge] Web Speech API durduruluyor...");
                
                // JavaScript'e durdurma komutu gönder
                await _webViewManager.ExecuteScriptAsync("stopWebSpeechRecognition()");
                
                _isWebSpeechActive = false;
                StateChanged?.Invoke(this, false);
                
                LogService.LogDebug("[WebSpeechBridge] Web Speech API durduruldu");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[WebSpeechBridge] Web Speech API durdurma hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Web Speech API'den gelen sonuçları işle
        /// </summary>
        public async Task HandleWebSpeechResult(string text, bool isFinal)
        {
            LogService.LogDebug($"[WebSpeechBridge] HandleWebSpeechResult çağrıldı - Text: '{text}', IsFinal: {isFinal}");
            
            if (!isFinal)
            {
                LogService.LogDebug("[WebSpeechBridge] Final olmayan sonuç, işlenmiyor");
                return;
            }
            
            // TTS filtreleme kontrolü
            bool isTTSPlaying = TextToSpeechService.IsSpeaking;
            if (isTTSPlaying)
            {
                LogService.LogDebug($"[WebSpeechBridge] TTS çalışıyor, metin filtreleniyor: '{text}'");
                return;
            }
            
            // DictationManager'a gönder
            LogService.LogDebug($"[WebSpeechBridge] Final sonuç alındı: '{text}' - DictationManager'a gönderiliyor");
            _dictationManager.ProcessTextChanged(text);
        }
        
        /// <summary>
        /// Komutu işle
        /// </summary>
        private async Task ProcessCommand(string command)
        {
            LogService.LogDebug($"[WebSpeechBridge] ProcessCommand çağrıldı - Command: '{command}'");
            
            if (_commandProcessor != null)
            {
                if (!string.IsNullOrWhiteSpace(command))
                {
                    LogService.LogDebug("[WebSpeechBridge] CommandProcessor'a gönderiliyor");
                    await _commandProcessor.ProcessCommandAsync(command);
                    LogService.LogDebug("[WebSpeechBridge] CommandProcessor işlemi tamamlandı");
                }
                else
                {
                    LogService.LogDebug("[WebSpeechBridge] Boş komut, işlenmiyor");
                }
            }
            else
            {
                LogService.LogDebug("[WebSpeechBridge] CommandProcessor servisi bulunamadı!");
            }
        }
        
        /// <summary>
        /// Mikrofon iznini kontrol et
        /// </summary>
        public async Task<bool> CheckMicrophonePermission()
        {
            try
            {
                string result = await _webViewManager.ExecuteScriptAsync(@"
                    (async function() {
                        try {
                            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                            stream.getTracks().forEach(track => track.stop());
                            return 'granted';
                        } catch (e) {
                            return e.name === 'NotAllowedError' ? 'denied' : 'error';
                        }
                    })()
                ");
                
                return result != null && result.Contains("granted");
            }
            catch
            {
                return false;
            }
        }
    }
}