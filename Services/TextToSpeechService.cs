using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Python Edge TTS ve Windows Tolga fallback TTS servisi
    /// </summary>
    public static class TextToSpeechService
    {
        /// <summary>
        /// TTS ile seslendirilecek metin üretildiğinde tetiklenir
        /// </summary>
        public static event EventHandler<string> SpeechGenerated;
        
        /// <summary>
        /// OutputArea'ya yazılacak metin üretildiğinde tetiklenir
        /// </summary>
        public static event EventHandler<string> OutputGenerated;
        
        /// <summary>
        /// TTS başladığında tetiklenir
        /// </summary>
        public static event EventHandler SpeechStarted;
        
        /// <summary>
        /// TTS tamamlandığında tetiklenir
        /// </summary>
        public static event EventHandler SpeechCompleted;
        
        /// <summary>
        /// TTS iptal edildiğinde tetiklenir
        /// </summary>
        public static event EventHandler SpeechCancelled;
        
        /// <summary>
        /// Python Edge TTS servisi
        /// </summary>
        private static EdgeTTSPythonBridge _edgeTTSPythonBridge;
        
        /// <summary>
        /// Windows Speech Synthesizer (Tolga için)
        /// </summary>
        private static SpeechSynthesizer _windowsSynthesizer;
        
        /// <summary>
        /// WebViewManager referansı
        /// </summary>
        private static IWebViewManager _webViewManager;
        
        /// <summary>
        /// Aktif TTS sesi (iç kullanım için)
        /// </summary>
        public static string CurrentEdgeVoice { get; set; } = "tr-TR-EmelNeural";
        
        /// <summary>
        /// TTS çalışıyor mu?
        /// </summary>
        private static bool _isSpeaking = false;

        /// <summary>
        /// TTS sessize alınmış mı?
        /// </summary>
        private static bool _isMuted = false;

        /// <summary>
        /// TTS sessize al/aç
        /// </summary>
        public static bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;

                // Mute edildiğinde ve konuşma devam ediyorsa durdur
                if (_isMuted && _isSpeaking)
                {
                    StopSpeaking();
                }

                // WebView mesajı kaldırıldı - artık Settings'ten yönetiliyor
                LogService.LogDebug($"[TextToSpeechService] Mute state changed: {_isMuted}");
            }
        }
        
        /// <summary>
        /// WebViewManager'ı ayarlar
        /// </summary>
        public static void SetWebViewManager(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager;
            LogService.LogDebug("[TextToSpeechService] WebViewManager set");
        }
        
        /// <summary>
        /// Servisi başlatır
        /// </summary>
        static TextToSpeechService()
        {
            try
            {
                // Python Edge TTS bridge'i oluştur
                _edgeTTSPythonBridge = new EdgeTTSPythonBridge();
                
                // Windows Speech Synthesizer'ı oluştur (Tolga için)
                _windowsSynthesizer = new SpeechSynthesizer();
                _windowsSynthesizer.SetOutputToDefaultAudioDevice();
                
                // Tolga sesini bul ve ayarla
                foreach (var voice in _windowsSynthesizer.GetInstalledVoices())
                {
                    if (voice.VoiceInfo.Name.Contains("Tolga", StringComparison.OrdinalIgnoreCase))
                    {
                        _windowsSynthesizer.SelectVoice(voice.VoiceInfo.Name);
                        LogService.LogDebug($"[TextToSpeechService] Tolga sesi bulundu: {voice.VoiceInfo.Name}");
                        break;
                    }
                }
                
                // Windows synthesizer event'leri
                _windowsSynthesizer.SpeakStarted += (s, e) => SpeechStarted?.Invoke(null, EventArgs.Empty);
                _windowsSynthesizer.SpeakCompleted += (s, e) => 
                {
                    _isSpeaking = false;
                    SpeechCompleted?.Invoke(null, EventArgs.Empty);
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"[TextToSpeechService] Başlatma hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Metni seslendirir - Yeni hiyerarşi: 1. Python Edge TTS, 2. WebSpeech API, 3. Windows Tolga
        /// </summary>
        public static async Task SpeakTextAsync(string text, bool useEdgeVoice = true)
        {
            // Mute kontrolü
            if (_isMuted)
            {
                LogService.LogDebug("[TextToSpeechService] TTS muted, seslendirme atlanıyor");
                SpeechCompleted?.Invoke(null, EventArgs.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                LogService.LogDebug("[TextToSpeechService] Boş metin, seslendirme yapılmadı");
                return;
            }

            try
            {
                // Emoji ve özel karakterleri temizle (Edge TTS uyumluluğu için)
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[\p{Cs}\p{So}]", "");
                text = text.Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    LogService.LogDebug("[TextToSpeechService] Temizleme sonrası boş metin");
                    return;
                }

                LogService.LogDebug($"[TextToSpeechService] Seslendirme başlatılıyor - Uzunluk: {text.Length}");
                
                // Eğer konuşma devam ediyorsa iptal et
                if (_isSpeaking)
                {
                    StopSpeaking();
                }
                
                _isSpeaking = true;
                SpeechStarted?.Invoke(null, EventArgs.Empty);
                
                // Settings'ten ses tercihini al
                var voicePreference = Managers.SettingsManager.Instance.Settings.TTSVoice;
                
                // Ses tercihine göre Edge voice'u ayarla
                if (voicePreference == "edge-ahmet")
                {
                    CurrentEdgeVoice = "tr-TR-AhmetNeural";
                }
                else
                {
                    CurrentEdgeVoice = "tr-TR-EmelNeural";
                }
                
                LogService.LogDebug($"[TextToSpeechService] TTS Hiyerarşisi başlatılıyor...");
                
                // TTS event'ini tetikle
                SpeechGenerated?.Invoke(null, text);
                
                bool speechSuccessful = false;
                
                // 1. ÖNCE: Python Edge TTS'i dene
                if (useEdgeVoice && _edgeTTSPythonBridge != null)
                {
                    try
                    {
                        LogService.LogDebug("[TextToSpeechService] [1/3] Python Edge TTS deneniyor...");
                        
                        // Python Edge TTS ile seslendir
                        var audioData = await _edgeTTSPythonBridge.SynthesizeSpeechAsync(text, CurrentEdgeVoice);
                        
                        if (audioData != null && audioData.Length > 0 && _webViewManager != null)
                        {
                            // Audio data'yı WebView'a gönder
                            await _webViewManager.SendAudioStreamAsync(audioData, "webm", text);
                            LogService.LogDebug($"[TextToSpeechService] ✓ Python Edge TTS başarılı - {audioData.Length} bytes");
                            speechSuccessful = true;
                        }
                        else
                        {
                            throw new Exception("Python Edge TTS veri üretemedi");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"[TextToSpeechService] ✗ Python Edge TTS başarısız: {ex.Message}");
                        speechSuccessful = false;
                    }
                }
                
                // 2. İKİNCİ: WebSpeech API (JavaScript speechSynthesis) dene
                if (!speechSuccessful && _webViewManager != null)
                {
                    try
                    {
                        LogService.LogDebug("[TextToSpeechService] [2/3] WebSpeech API deneniyor...");
                        
                        // WebView'a JavaScript ile seslendirme komutu gönder
                        var jsCommand = $@"
                            (async function() {{
                                if (typeof speakWithEdgeVoice === 'function') {{
                                    window.currentEdgeVoiceName = '{CurrentEdgeVoice.Replace("tr-TR-", "").Replace("Neural", "")}';
                                    await speakWithEdgeVoice('{text.Replace("'", "\\'")}');
                                    return true;
                                }}
                                return false;
                            }})();";
                        
                        await _webViewManager.ExecuteScriptAsync(jsCommand);
                        
                        // WebSpeech API'nin tamamlanmasını bekle (yaklaşık süre)
                        var estimatedDuration = Math.Min(text.Length * 50, 10000); // Max 10 saniye
                        await Task.Delay(estimatedDuration);
                        
                        LogService.LogDebug("[TextToSpeechService] ✓ WebSpeech API başarılı");
                        speechSuccessful = true;
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"[TextToSpeechService] ✗ WebSpeech API başarısız: {ex.Message}");
                        speechSuccessful = false;
                    }
                }
                
                // 3. ÜÇÜNCÜ: Windows Tolga TTS (Son çare)
                if (!speechSuccessful)
                {
                    try
                    {
                        LogService.LogDebug("[TextToSpeechService] [3/3] Windows Tolga TTS deneniyor...");
                        await SpeakWithTolgaAsync(text);
                        LogService.LogDebug("[TextToSpeechService] ✓ Windows Tolga TTS başarılı");
                        speechSuccessful = true;
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"[TextToSpeechService] ✗ Windows Tolga TTS de başarısız: {ex.Message}");
                        
                        // Son çare: Varsayılan Windows TTS
                        try
                        {
                            LogService.LogDebug("[TextToSpeechService] [4/4] Varsayılan Windows TTS deneniyor...");
                            _windowsSynthesizer.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.Female);
                            await SpeakWithTolgaAsync(text);
                            LogService.LogDebug("[TextToSpeechService] ✓ Varsayılan Windows TTS başarılı");
                        }
                        catch (Exception finalEx)
                        {
                            LogService.LogError($"[TextToSpeechService] ✗✗✗ Tüm TTS yöntemleri başarısız: {finalEx.Message}");
                        }
                    }
                }

                // Edge TTS başarılı olduysa, SpeechCompleted WebView'dan gelecek (ttsCompleted mesajı)
                // Burada çağırırsak audio henüz çalmadan "tamamlandı" sinyali gider!
                if (speechSuccessful && useEdgeVoice)
                {
                    LogService.LogDebug("[TextToSpeechService] Edge TTS gönderildi, SpeechCompleted WebView'dan beklenecek");
                    // _isSpeaking'i SIFIRLAMIYORUZ - WebView audio bitince sıfırlayacak
                    return;
                }

                // Sadece fallback TTS'ler için (Windows Tolga vb.) burada çağır
                _isSpeaking = false;
                SpeechCompleted?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogService.LogError($"[TextToSpeechService] Seslendirme hatası: {ex.Message}");
                _isSpeaking = false;
                SpeechCompleted?.Invoke(null, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Windows Tolga sesi ile seslendirir
        /// </summary>
        private static async Task SpeakWithTolgaAsync(string text)
        {
            try
            {
                LogService.LogDebug("[TextToSpeechService] Tolga sesi ile seslendirme başlatılıyor");
                
                var tcs = new TaskCompletionSource<bool>();
                
                EventHandler<SpeakCompletedEventArgs> handler = null;
                handler = (s, e) =>
                {
                    _windowsSynthesizer.SpeakCompleted -= handler;
                    tcs.SetResult(true);
                };
                
                _windowsSynthesizer.SpeakCompleted += handler;
                _windowsSynthesizer.SpeakAsync(text);
                
                await tcs.Task;
                
                LogService.LogDebug("[TextToSpeechService] Tolga seslendirmesi tamamlandı");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[TextToSpeechService] Tolga seslendirme hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Konuşmayı durdurur
        /// </summary>
        public static void StopSpeaking()
        {
            try
            {
                LogService.LogDebug("[TextToSpeechService] Konuşma durduruluyor");
                
                // Windows synthesizer'ı durdur
                if (_windowsSynthesizer != null && _windowsSynthesizer.State == SynthesizerState.Speaking)
                {
                    _windowsSynthesizer.SpeakAsyncCancelAll();
                }
                
                // WebView'da çalan sesi durdur
                _webViewManager?.StopTTS();
                
                _isSpeaking = false;
                SpeechCancelled?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogService.LogError($"[TextToSpeechService] Konuşma durdurma hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// TTS konuşuyor mu?
        /// </summary>
        public static bool IsSpeaking => _isSpeaking;
        
        /// <summary>
        /// OutputArea'ya metin gönderir
        /// </summary>
        public static void SendToOutput(string text)
        {
            OutputGenerated?.Invoke(null, text);
        }
        
        /// <summary>
        /// Servisi temizler
        /// </summary>
        public static void Dispose()
        {
            try
            {
                StopSpeaking();
                
                _windowsSynthesizer?.Dispose();
                _edgeTTSPythonBridge?.Dispose();
                
                LogService.LogDebug("[TextToSpeechService] Servis temizlendi");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[TextToSpeechService] Temizleme hatası: {ex.Message}");
            }
        }
    }
}