using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using System.Diagnostics;
using System.Text.RegularExpressions;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Gelişmiş Türkçe SSML destekli Microsoft Tolga sesi için text-to-speech servisi.
    /// </summary>
    public static class TextToSpeechService
    {
        /// <summary>
        /// TTS ile seslendirilecek metin üretildiğinde tetiklenir (ham metin)
        /// </summary>
        public static event EventHandler<string> SpeechGenerated;
        
        /// <summary>
        /// OutputArea'ya yazılacak metin üretildiğinde tetiklenir
        /// </summary>
        public static event EventHandler<string> OutputGenerated;
        
        
        /// <summary>
        /// Edge TTS kullanılacak mı? (true ise Edge Neural sesler, false ise Windows Tolga)
        /// </summary>
        public static bool UseEdgeTTS { get; set; } = true; // Edge Neural TTS varsayılan olarak açık
        
        /// <summary>
        /// Edge TTS servisi
        /// </summary>
        private static EdgeTTSService _edgeTTSService;
        
        /// <summary>
        /// Aktif ses tipi
        /// </summary>
        public enum VoiceType
        {
            EdgeEmel,
            EdgeAhmet,
            WindowsTolga,
            Automatic // İnternet varsa Edge, yoksa Tolga
        }
        
        /// <summary>
        /// Seçili ses tipi
        /// </summary>
        public static VoiceType SelectedVoice { get; set; } = VoiceType.Automatic;
        
        /// <summary>
        /// WebViewManager referansı (Edge TTS için)
        /// </summary>
        private static IWebViewManager _webViewManager;
        
        /// <summary>
        /// Mevcut Edge sesi
        /// </summary>
        public static string CurrentEdgeVoice { get; set; } = "tr-TR-EmelNeural"; // Varsayılan Emel
        
        /// <summary>
        /// SettingsManager referansı (ses seçimi için)
        /// </summary>
        private static Managers.SettingsManager _settingsManager = Managers.SettingsManager.Instance;
        
        /// <summary>
        /// WebViewManager'ı ayarlar ve Smart TTS'i initialize eder
        /// </summary>
        public static void SetWebViewManager(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager;
            
            // Smart TTS Manager'ı initialize et
            if (UseSmartTTS && _smartTTSManager == null)
            {
                try
                {
                    _smartTTSManager = new SmartTTSManager(_webViewManager);
                    
                    // Smart TTS events'lerini bağla
                    _smartTTSManager.TTSStarted += (_, _) => SpeechStarted?.Invoke(null, EventArgs.Empty);
                    _smartTTSManager.TTSCompleted += (_, _) => SpeechCompleted?.Invoke(null, EventArgs.Empty);
                    _smartTTSManager.TTSInterrupted += (_, _) => SpeechCancelled?.Invoke(null, EventArgs.Empty);
                    
                    // Smart TTS sistemini başlat
                    _ = Task.Run(async () =>
                    {
                        bool initialized = await _smartTTSManager.InitializeAsync();
                        LogService.LogDebug($"[TextToSpeechService] Smart TTS Manager initialized: {initialized}");
                    });
                    
                    LogService.LogDebug("[TextToSpeechService] Smart TTS Manager oluşturuldu");
                }
                catch (Exception)
                {
                    LogService.LogDebug($"[TextToSpeechService] Smart TTS Manager init hatası");
                    _smartTTSManager = null;
                }
            }
        }
        
        /// <summary>
        /// OutputArea'ya metin gönder
        /// </summary>
        public static void SendToOutput(string text)
        {
            // Tüm SendToOutput logları kaldırıldı - gereksiz
            
            OutputGenerated?.Invoke(null, text);
            // Event tetikleme logu gereksiz
        }
        
        /// <summary>
        /// Smart TTS Manager'ın VAD'ini duraklat
        /// </summary>
        public static void PauseSmartTTSVAD()
        {
            if (_smartTTSManager != null)
            {
                _smartTTSManager.PauseVAD();
            }
        }
        
        /// <summary>
        /// Smart TTS Manager'ın VAD'ini devam ettir
        /// </summary>
        public static void ResumeSmartTTSVAD()
        {
            if (_smartTTSManager != null)
            {
                _smartTTSManager.ResumeVAD();
            }
        }
        
        /// <summary>
        /// Şu anda oynatılan TTS metnini alır
        /// </summary>
        public static string GetCurrentTTSText()
        {
            lock (_ttsTextLock)
            {
                return _currentTTSText;
            }
        }
        
        /// <summary>
        /// Son oynatılan TTS metnini alır
        /// </summary>
        public static string GetLastTTSText()
        {
            lock (_ttsTextLock)
            {
                return _lastTTSText;
            }
        }
        
        /// <summary>
        /// TTS'in ne zaman başladığını alır
        /// </summary>
        public static DateTime GetLastTTSStartTime()
        {
            lock (_ttsTextLock)
            {
                return _lastTTSStartTime;
            }
        }
        
        /// <summary>
        /// TTS metnini günceller (internal use)
        /// </summary>
        private static void UpdateTTSText(string text)
        {
            lock (_ttsTextLock)
            {
                _lastTTSText = _currentTTSText;
                _currentTTSText = text;
                _lastTTSStartTime = DateTime.UtcNow;
                LogService.LogDebug($"[TextToSpeechService] TTS Text updated: '{text.Substring(0, Math.Min(50, text.Length))}...'");
            }
        }
        #region Enums ve Olaylar

        // Durum yönetimi için enum
        public enum SpeechState
        {
            Idle,           // Boşta
            Synthesizing,   // Ses sentezleniyor
            Speaking,       // Konuşuyor
            Stopping,       // Durduruluyor
            Failed          // Hata oluştu
        }

        // Konuşma tarzları
        public enum ConusmaTarzi
        {
            Normal,         // Standart konuşma
            Heyecanli,      // Heyecanlı ton
            Sakin,          // Sakin ton
            Resmi,          // Resmi/Profesyonel ton
            Bilgilendirici, // Eğitici/Bilgilendirici ton
            Vurgulu         // Vurgulu anlatım
        }

        // Olay tanımlamaları
        public static event EventHandler SpeechStarted;
        public static event EventHandler SpeechCompleted;
        public static event EventHandler SpeechCancelled;
        public static event EventHandler<SpeechErrorEventArgs> SpeechFailed;

        // Özel hata olayı argümanları
        public class SpeechErrorEventArgs : EventArgs
        {
            public Exception Error { get; }
            public string Context { get; }

            public SpeechErrorEventArgs(Exception error, string context)
            {
                Error = error;
                Context = context;
            }
        }
        #endregion

        #region Özel Alanlar

        private static MediaPlayer _mediaPlayer;
        private static SpeechSynthesizer _synthesizer;
        private static bool _isDisposed = false;
        private static readonly object _lock = new object(); // Thread güvenliği için kilit nesnesi

        // Durum ve iptal mekanizması
        private static SpeechState _currentState = SpeechState.Idle;
        private static CancellationTokenSource _playbackCts;
        private static CancellationToken _currentCancellationToken;
        private static bool _isEdgeTTSPlaying = false; // Edge TTS durumu için yeni flag
        
        // Smart TTS Manager - ChatGPT benzeri interrupt capability
        private static SmartTTSManager _smartTTSManager;
        public static bool UseSmartTTS { get; set; } = true; // Smart TTS varsayılan olarak açık

        // TTS Text Tracking - Feedback loop önleme için
        private static string _currentTTSText = string.Empty;
        private static string _lastTTSText = string.Empty;
        private static DateTime _lastTTSStartTime = DateTime.MinValue;
        private static readonly object _ttsTextLock = new object();

        // SSML ayarları
        private static float _currentSpeakRate = 0.95f;
        private static float _currentVolume = 100.0f;  // Tam ses
        private static string _currentVoiceName = "Microsoft Tolga";
        private const string TURKISH_CULTURE = "tr-TR";
        private static int _retryCount = 3; // Yeniden deneme sayısı
        private static TimeSpan _retryDelay = TimeSpan.FromSeconds(1); // Yeniden deneme aralığı

        // Türkçe kısaltmalar için basit sözlük (en sık kullanılanlar, çakışma olmayanlar)
        private static readonly Dictionary<string, string> _abbreviations = new Dictionary<string, string>()
        {
            // Sık kullanılan semboller
            { "$", "dolar" },
            { "€", "avro" },
            { "£", "sterlin" },
            { "%", "yüzde" },
            { "@", "et" },
            { "KB", "kilobayt" },
            { "MB", "megabayt" },
            { "GB", "gigabayt" },
            { "TB", "terabayt" },
            { "bit", "bit" },
            { "kbps", "kilobit saniye" },
            { "Mbps", "megabit saniye" },
            { "Gbps", "gigabit saniye" },
            { "dk", "dakika" },
            { "°C", "santigrat" },
            { "K", "kelvin" },
            { "°F", "fahrenhayt" },
            { "ml", "mililitre" },
            { "cl", "santilitre" },
            { "l", "litre" },
            { "m²", "metre kare" },
            { "km²", "kilometre kare" },
            { "ha", "hektar" },
            { "mm", "milimetre" },
            { "cm", "santimetre" },
            { "m", "metre" },
            { "km", "kilometre" },
            { "dm", "desimetre" },
            { "ft", "fit" },
            { "in", "inç" },
            { "yd", "yarda" },
            { "mil", "mil" },
            { "nm", "nanometre" },
            { "URL", "u re le" },
            { "WWW", "ve ve ve" },
            { "JPEG", "jipeg" },
            { "PDF", "pi di ef" },
            { "ADSL", "ey di es el" },
            { "GPRS", "ci pi ar es" },
            { "GPS", "ci pi es" },
            { "GSM", "ci es em" },
            { "RAM", "rem" },
            { "ROM", "rom" },
            { "CPU", "işlemci" },
            { "USB", "u es bi" },
            { "AI", "yapay zeka" },
            { "CEO", "si i o" },
            { "CTO", "si ti o" },
            { "API", "a pi" },
            { "sn.", "saniye" },
            { "Tic.", "ticaret" },
            { "San.", "sanayi" },
            { "İnş.", "inşaat" },
            { "Müh.", "mühendis" },
            { "Mim.", "mimar" },
            { "Vet.", "veteriner" },
            { "Ecz.", "eczacı" },
            { "vb.", "ve benzeri" },
            { "vs.", "vesaire" },
            { "Dr.", "doktor" },
            { "Prof.", "profesör" },
            { "Doç.", "doçent" },
            { "TBMM", "Te be be me" },
            { "TC", "Türkiye Cumhuriyeti" },
            { "TL", "Türk Lirası" },
            { "Apt.", "apartman" },
            { "yy.", "yüzyıl" },
            { "MÖ", "milattan önce" },
            { "MS", "milattan sonra" },
            { "Av.", "avukat" },
            { "Hz.", "hazreti" },
            { "Yrd.", "yardımcı" },
            { "Mah.", "mahalle" },
            { "Cad.", "cadde" },
            { "vd.", "ve diğerleri" },
            { "bkz.", "bakınız" },
            { "No:", "numara" },
            { "Ltd.", "limited" },
            { "A.Ş.", "anonim şirket" },
            { "SGK", "Se ge ka" },
            { "STK", "Se te ka" },
            { "TSE", "Te se e" },
            { "ÖSYM", "Ö se ye me" },
            { "AVM", "a ve me" },
            { "YSK", "Ye se ka " },
        };

        // Telaffuz düzeltmeleri için regex kalıpları
        private static readonly Regex _quotePattern = new Regex("\"([^\"]+)\"", RegexOptions.Compiled);

        #endregion

        #region Başlatma ve Özellikler

        // Statik kurucu
        static TextToSpeechService()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_mediaPlayer == null)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            }

            if (_synthesizer == null)
            {
                _synthesizer = new SpeechSynthesizer();
                
                // Microsoft Tolga sesini bul ve seç
                try
                {
                    var allVoices = SpeechSynthesizer.AllVoices;
                    var tolgaVoice = allVoices.FirstOrDefault(voice => 
                        voice.DisplayName.Contains("Tolga") || 
                        (voice.DisplayName.Contains("Microsoft") && voice.DisplayName.Contains("Turkish")));
                    
                    if (tolgaVoice != null)
                    {
                        _synthesizer.Voice = tolgaVoice;
                        _currentVoiceName = tolgaVoice.DisplayName;
                    }
                    else
                    {
                        // Tolga bulunamazsa herhangi bir Türkçe ses seç
                        var turkishVoice = allVoices.FirstOrDefault(voice => 
                            voice.Language.Contains("tr-TR"));
                        
                        if (turkishVoice != null)
                        {
                            _synthesizer.Voice = turkishVoice;
                            _currentVoiceName = turkishVoice.DisplayName;
                        }
                        else
                        {
                            _synthesizer.Voice = SpeechSynthesizer.DefaultVoice;
                            _currentVoiceName = _synthesizer.Voice.DisplayName;
                        }
                    }
                }
                catch (Exception)
                {
                    _synthesizer.Voice = SpeechSynthesizer.DefaultVoice;
                    _currentVoiceName = _synthesizer.Voice.DisplayName;
                }
            }
        }

        // Servisin mevcut durumunu alır
        public static SpeechState CurrentState
        {
            get { lock (_lock) return _currentState; }
            private set
            {
                lock (_lock)
                {
                    if (_currentState != value)
                    {
                        _currentState = value;
                    }
                }
            }
        }

        // Servis kaynaklarını temizleyen metod
        public static async Task DisposeAsync()
        {
            if (_isDisposed) return;

            await StopSpeakingAsync();

            lock (_lock)
            {
                if (!_isDisposed)
                {
                    try
                    {
                        _mediaPlayer?.Dispose();
                        _mediaPlayer = null;
                        
                        _synthesizer?.Dispose();
                        _synthesizer = null;

                        _playbackCts?.Dispose();
                        _playbackCts = null;
                        
                        // Edge TTS servisini temizle
                        _edgeTTSService?.Dispose();
                        _edgeTTSService = null;

                        _isDisposed = true;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        #endregion

        #region Yardımcı Metodlar
        
        /// <summary>
        /// İnternet bağlantısını kontrol eder
        /// </summary>
        public static async Task<bool> IsInternetAvailable()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var response = await client.GetAsync("https://www.bing.com");
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Windows Tolga sesi ile seslendirme
        /// </summary>
        private static async Task<bool> SpeakWithWindowsTolga(string text, float speakRate, bool interruptCurrent, CancellationToken cancellationToken)
        {
            try
            {
                if (interruptCurrent)
                {
                    await StopSpeakingAsync();
                }

                // SSML oluştur
                string ssml = BuildBasicSSML(text, speakRate);

                // Seslendirmeyi başlat
                var stream = await _synthesizer.SynthesizeSsmlToStreamAsync(ssml).AsTask(cancellationToken);
                
                if (stream == null)
                {
                    throw new Exception("Ses akışı oluşturulamadı");
                }

                // MediaSource oluştur ve oynat
                var source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _mediaPlayer.Source = source;
                
                // Ses seviyesini ayarla
                _mediaPlayer.Volume = _currentVolume / 100.0;
                
                _currentState = SpeechState.Speaking;
                _currentCancellationToken = cancellationToken;

                _mediaPlayer.Play();

                // Başarılı olduğunu bildir
                await Task.Run(() => SpeechStarted?.Invoke(null, EventArgs.Empty));
                
                return true;
            }
            catch (Exception ex)
            {
                _currentState = SpeechState.Failed;
                await Task.Run(() => SpeechFailed?.Invoke(null, new SpeechErrorEventArgs(ex, "Tolga TTS başarısız")));
                return false;
            }
        }

        #endregion

        #region Ana Konuşma Fonksiyonları

        /// <summary>
        /// Metni verilen ayarlarla seslendirir
        /// </summary>
        public static async Task<bool> SpeakTextAsync(
            string text,
            float speakRate = 0.95f,
            bool interruptCurrent = true,
            CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"[TextToSpeechService] SpeakTextAsync ÇAĞIRILDI");
            Debug.WriteLine($"[TextToSpeechService] Text null/boş mu: {string.IsNullOrWhiteSpace(text)}");
            Debug.WriteLine($"[TextToSpeechService] Text uzunluğu: {text?.Length ?? 0}");
            if (!string.IsNullOrWhiteSpace(text))
            {
                Debug.WriteLine($"[TextToSpeechService] Text ilk 100 karakter: '{text.Substring(0, Math.Min(100, text.Length))}'");
            }
            
            if (string.IsNullOrWhiteSpace(text)) 
            {
                Debug.WriteLine($"[TextToSpeechService] Text boş - return false");
                return false;
            }

            _currentSpeakRate = speakRate;

            // Ses tipine göre TTS seçimi - Settings'ten gelen değeri kullan
            bool useEdgeVoice = false;
            string voiceName = "";
            
            // Settings'ten ses tercihini al
            var settingsVoice = _settingsManager.Settings.TTSVoice;
            Debug.WriteLine($"[TextToSpeechService] Settings'ten gelen ses tercihi: {settingsVoice}");
            
            switch (settingsVoice)
            {
                case "edge-emel":
                    SelectedVoice = VoiceType.EdgeEmel;
                    useEdgeVoice = true;
                    voiceName = "tr-TR-EmelNeural";
                    Debug.WriteLine("[TextToSpeechService] Edge Emel sesi seçildi");
                    break;
                    
                case "edge-ahmet":
                    SelectedVoice = VoiceType.EdgeAhmet;
                    useEdgeVoice = true;
                    voiceName = "tr-TR-AhmetNeural";
                    Debug.WriteLine("[TextToSpeechService] Edge Ahmet sesi seçildi");
                    break;
                    
                case "windowsTolga":
                    SelectedVoice = VoiceType.WindowsTolga;
                    useEdgeVoice = false;
                    Debug.WriteLine("[TextToSpeechService] Windows Tolga sesi seçildi");
                    break;
                    
                case "automatic":
                default:
                    SelectedVoice = VoiceType.Automatic;
                    // İnternet bağlantısı kontrolü yaparak karar ver
                    useEdgeVoice = await IsInternetAvailable();
                    voiceName = "tr-TR-EmelNeural"; // Varsayılan Edge ses
                    Debug.WriteLine($"[TextToSpeechService] Automatic mod - İnternet: {useEdgeVoice}, Varsayılan ses: {voiceName}");
                    break;
            }

            Debug.WriteLine($"[TextToSpeechService] Edge TLS'e gönderilen ses: {voiceName}");
            
            // Edge TTS kullanılacaksa
            if (useEdgeVoice)
            {
                Debug.WriteLine($"[TextToSpeechService] Edge TTS kullanılacak");
                
                // Edge TTS için ham metni kullan
                UpdateTTSText(text);
                SpeechGenerated?.Invoke(null, text);
                
                
                // ÇÖZÜM: Edge TTS oynatılıyor mu kontrol et
                Debug.WriteLine($"[TextToSpeechService] _isEdgeTTSPlaying kontrolü: {_isEdgeTTSPlaying}");
                if (_isEdgeTTSPlaying)
                {
                    Debug.WriteLine($"[TextToSpeechService] Edge TTS zaten çalışıyor - return false");
                    SendToOutput("⚠️ Bir TTS oynatması devam ediyor");
                    return false;
                }
                
                // Edge TTS servisini başlat
                if (_edgeTTSService == null)
                {
                    _edgeTTSService = new EdgeTTSService();
                }
                
                try
                {
                    // Edge TTS oynatılıyor olarak işaretle
                    _isEdgeTTSPlaying = true;
                    
                    // ÇÖZÜM: Edge TTS başlamadan önce SpeechStarted event'ini tetikle
                    await Task.Run(() => SpeechStarted?.Invoke(null, EventArgs.Empty));
                    
                    // Edge TTS ile seslendir - ham metin kullan
                    Debug.WriteLine($"[TextToSpeechService] Edge TTS'e gönderilen ses: {voiceName}");
                    var audioData = await _edgeTTSService.SynthesizeSpeechAsync(text, voiceName);
                    
                    if (audioData != null && audioData.Length > 0 && _webViewManager != null)
                    {
                        // WebView'a audio stream gönder
                        await _webViewManager.SendAudioStreamAsync(audioData, "webm", text);
                        // SendToOutput($"🔊 Edge TTS ile seslendiriliyor ({(voiceName.Contains("Emel") ? "Emel" : "Ahmet")})");
                        
                        // ÇÖZÜM: Edge TTS için SpeechCompleted event'ini burada tetikleme
                        // JavaScript tarafından audio.onended'de tetiklenecek
                        return true;
                    }
                    else
                    {
                        throw new Exception("Edge TTS ses verisi alınamadı");
                    }
                }
                catch (Exception)
                {
                    
                    // Automatic modda ise Tolga'ya geç
                    if (SelectedVoice == VoiceType.Automatic)
                    {
                        SendToOutput("⚠️ İnternet bağlantısı sorunu, Tolga sesi kullanılıyor");
                        
                        // Tolga için metni işle
                        string processedText = ProcessTextForSpeech(text);
                        UpdateTTSText(processedText);
                        SpeechGenerated?.Invoke(null, processedText);
                        
                        return await SpeakWithWindowsTolga(processedText, speakRate, interruptCurrent, cancellationToken);
                    }
                    else
                    {
                        // Manuel Edge seçiminde hata bildir
                        SendToOutput($"❌ Edge TTS hatası");
                        await Task.Run(() => SpeechFailed?.Invoke(null, new SpeechErrorEventArgs(new Exception("Edge TTS hatası"), "Edge TTS başarısız")));
                        return false;
                    }
                }
                finally
                {
                    // Edge TTS oynatma durumunu sıfırla
                    _isEdgeTTSPlaying = false;
                }
            }
            else
            {
                // Windows Tolga için metni işle
                string processedText = ProcessTextForSpeech(text);
                UpdateTTSText(processedText);
                SpeechGenerated?.Invoke(null, processedText);
                
                // Windows Tolga kullan
                // SendToOutput("🔊 Windows Tolga sesi ile seslendiriliyor");
                return await SpeakWithWindowsTolga(processedText, speakRate, interruptCurrent, cancellationToken);
            }
        }

        /// <summary>
        /// Metni belirtilen konuşma tarzı ve ayarlarla seslendirir
        /// </summary>
        public static async Task<bool> SpeakTextWithStyleAsync(
            string text,
            ConusmaTarzi tarzi = ConusmaTarzi.Normal,
            float speakRate = 0.95f,
            float volume = 100.0f,
            bool interruptCurrent = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // SMART TTS: Eğer Smart TTS aktifse ve Edge TTS kullanılıyorsa Smart TTS kullan
            if (UseSmartTTS && _smartTTSManager != null && UseEdgeTTS)
            {
                try
                {
                    LogService.LogDebug($"[TextToSpeechService] Smart TTS ile seslendiriliyor: '{text}'");
                    
                    // TTS metnini takip et - ham metin kullan
                    UpdateTTSText(text);
                    
                    // Ses tipine göre voice seç - Settings'ten gelen değere göre
                    string smartVoiceName = "tr-TR-EmelNeural";
                    var smartSettingsVoice = _settingsManager.Settings.TTSVoice;
                    Debug.WriteLine($"[TextToSpeechService] Smart TTS - Settings'ten gelen ses tercihi: {smartSettingsVoice}");
                    
                    switch (smartSettingsVoice)
                    {
                        case "edge-emel":
                            smartVoiceName = "tr-TR-EmelNeural";
                            Debug.WriteLine("[TextToSpeechService] Smart TTS - Edge Emel sesi seçildi");
                            break;
                        case "edge-ahmet":
                            smartVoiceName = "tr-TR-AhmetNeural";
                            Debug.WriteLine("[TextToSpeechService] Smart TTS - Edge Ahmet sesi seçildi");
                            break;
                        case "automatic":
                            if (await IsInternetAvailable())
                            {
                                smartVoiceName = "tr-TR-EmelNeural";
                                Debug.WriteLine("[TextToSpeechService] Smart TTS - Automatic mod, internet var, Emel seçildi");
                            }
                            else
                            {
                                // İnternet yoksa fallback to normal TTS
                                LogService.LogDebug("[TextToSpeechService] İnternet yok, normal TTS'e fallback");
                                goto NORMAL_TTS;
                            }
                            break;
                        default:
                            // Smart TTS sadece Edge TTS ile çalışır
                            if (smartSettingsVoice == "windowsTolga")
                            {
                                Debug.WriteLine("[TextToSpeechService] Smart TTS - Windows Tolga seçili, normal TTS'e fallback");
                                goto NORMAL_TTS;
                            }
                            break;
                    }
                    
                    // Edge TTS servisini initialize et
                    if (_edgeTTSService == null)
                    {
                        _edgeTTSService = new EdgeTTSService();
                    }
                    
                    // Audio data oluştur - ham metin kullan
                    Debug.WriteLine($"[TextToSpeechService] Smart TTS - Edge TTS'e gönderilen ses: {smartVoiceName}");
                    var audioData = await _edgeTTSService.SynthesizeSpeechAsync(text, smartVoiceName);
                    
                    if (audioData != null && audioData.Length > 0)
                    {
                        // Smart TTS ile oynat (interrupt capability ile) - ham metin kullan
                        bool completed = await _smartTTSManager.PlayWithInterruptAsync(audioData, text, cancellationToken);
                        
                        LogService.LogDebug($"[TextToSpeechService] Smart TTS tamamlandı: {completed}");
                        return completed; // ÇÖZÜM: Smart TTS ile seslendirme yaptıktan sonra normal TTS'e geçmeden direkt return et
                    }
                    else
                    {
                        LogService.LogDebug("[TextToSpeechService] Edge TTS audio data alınamadı, normal TTS'e fallback");
                        // Audio data alınamazsa normal TTS'e fallback
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"[TextToSpeechService] Smart TTS hatası: {ex.Message}, normal TTS'e fallback");
                    // Smart TTS hatası varsa normal TTS'e fallback
                }
            }
            
            // Smart TTS başarılı olmuşsa burada olmamalıyız
            // Smart TTS devre dışı veya başarısızsa normal TTS kullan
            NORMAL_TTS:
            _currentSpeakRate = speakRate;
            _currentVolume = volume;

            // Ses tipine göre TTS seçimi - Settings'ten gelen değeri kullan
            bool useEdgeVoice = false;
            string voiceName = "";
            
            // Settings'ten ses tercihini al
            var settingsVoice = _settingsManager.Settings.TTSVoice;
            Debug.WriteLine($"[TextToSpeechService] Settings'ten gelen ses tercihi: {settingsVoice}");
            
            switch (settingsVoice)
            {
                case "edge-emel":
                    SelectedVoice = VoiceType.EdgeEmel;
                    useEdgeVoice = true;
                    voiceName = "tr-TR-EmelNeural";
                    Debug.WriteLine("[TextToSpeechService] Edge Emel sesi seçildi");
                    break;
                    
                case "edge-ahmet":
                    SelectedVoice = VoiceType.EdgeAhmet;
                    useEdgeVoice = true;
                    voiceName = "tr-TR-AhmetNeural";
                    Debug.WriteLine("[TextToSpeechService] Edge Ahmet sesi seçildi");
                    break;
                    
                case "windowsTolga":
                    SelectedVoice = VoiceType.WindowsTolga;
                    useEdgeVoice = false;
                    Debug.WriteLine("[TextToSpeechService] Windows Tolga sesi seçildi");
                    break;
                    
                case "automatic":
                default:
                    SelectedVoice = VoiceType.Automatic;
                    // İnternet bağlantısı kontrolü yaparak karar ver
                    useEdgeVoice = await IsInternetAvailable();
                    voiceName = "tr-TR-EmelNeural"; // Varsayılan Edge ses
                    Debug.WriteLine($"[TextToSpeechService] Automatic mod - İnternet: {useEdgeVoice}, Varsayılan ses: {voiceName}");
                    break;
            }

            // Edge TTS kullanılacaksa
            if (useEdgeVoice)
            {
                // Edge TTS için ham metni kullan
                UpdateTTSText(text);
                SpeechGenerated?.Invoke(null, text);
                
                
                // Edge TTS servisini başlat
                if (_edgeTTSService == null)
                {
                    _edgeTTSService = new EdgeTTSService();
                }
                
                try
                {
                    // Edge TTS oynatma durumunu ayarla
                    _isEdgeTTSPlaying = true;
                    
                    // ÇÖZÜM: Edge TTS başlamadan önce SpeechStarted event'ini tetikle
                    await Task.Run(() => SpeechStarted?.Invoke(null, EventArgs.Empty));
                    
                    // Edge TTS ile seslendir - ham metin kullan
                    Debug.WriteLine($"[TextToSpeechService] Edge TTS'e gönderilen ses: {voiceName}");
                    var audioData = await _edgeTTSService.SynthesizeSpeechAsync(text, voiceName);
                    
                    if (audioData != null && audioData.Length > 0 && _webViewManager != null)
                    {
                        // WebView'a audio stream gönder
                        await _webViewManager.SendAudioStreamAsync(audioData, "webm", text);
                        // SendToOutput($"🔊 Edge TTS ile seslendiriliyor ({(voiceName.Contains("Emel") ? "Emel" : "Ahmet")}) - Tarz: {tarzi}");
                        
                        // ÇÖZÜM: Edge TTS için SpeechCompleted event'ini burada tetikleme
                        // JavaScript tarafından audio.onended'de tetiklenecek
                        return true;
                    }
                    else
                    {
                        throw new Exception("Edge TTS ses verisi alınamadı");
                    }
                }
                catch (Exception)
                {
                    _isEdgeTTSPlaying = false; // Hata durumunda flag'i sıfırla
                    
                    // Automatic modda ise Tolga'ya geç
                    if (SelectedVoice == VoiceType.Automatic)
                    {
                        SendToOutput("⚠️ İnternet bağlantısı sorunu, Tolga sesi kullanılıyor");
                        // Windows Tolga için metni işle
                        string processedTextForTolga = ProcessTextForSpeech(text);
                        UpdateTTSText(processedTextForTolga);
                        SpeechGenerated?.Invoke(null, processedTextForTolga);
                        return await SpeakWithWindowsTolga(processedTextForTolga, speakRate, interruptCurrent, cancellationToken);
                    }
                    else
                    {
                        // Manuel Edge seçiminde hata bildir
                        SendToOutput($"❌ Edge TTS hatası");
                        await Task.Run(() => SpeechFailed?.Invoke(null, new SpeechErrorEventArgs(new Exception("Edge TTS hatası"), "Edge TTS başarısız")));
                        return false;
                    }
                }
                finally
                {
                    // Edge TTS oynatma durumunu sıfırla
                    _isEdgeTTSPlaying = false;
                }
            }
            else
            {
                // Windows Tolga için metni işle
                string processedText = ProcessTextForSpeech(text);
                UpdateTTSText(processedText);
                SpeechGenerated?.Invoke(null, processedText);
                
                // SendToOutput($"🔊 Windows Tolga sesi ile seslendiriliyor - Tarz: {tarzi}");
                return await SpeakWithWindowsTolga(processedText, speakRate, interruptCurrent, cancellationToken);
            }
        }


        /// <summary>
        /// Seslendirmeyi durdurur (senkron metod - geriye uyumluluk için)
        /// </summary>
        public static void StopSpeaking()
        {
            // Deadlock riskini önlemek için Task.Run kullan
            Task.Run(async () => await StopSpeakingAsync()).Wait();
        }

        #endregion

        #region Metin İşleme ve SSML Oluşturma

        /// <summary>
        /// Seslendirme için metni işler
        /// </summary>
        public static string ProcessTextForSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 1. Emojileri ve diğer problemli karakterleri temizle
            text = CleanTextForSpeech(text);

            // 2. Alıntıları metne virgül ekleyerek işle
            text = _quotePattern.Replace(text, match =>
            {
                // Alıntı işaretlerinin içinde kalacak şekilde virgül ekle
                return $", \"{match.Groups[1].Value}\", ";
            });

            // 3. Kısaltmaları işle
            foreach (var abbr in _abbreviations)
            {
                // Nokta içeren kısaltmalar için özel işleme (vb., Tic. gibi)
                if (abbr.Key.EndsWith("."))
                {
                    // Noktasız kısaltmayı al (vb. -> vb)
                    string abbrWithoutDot = abbr.Key.Substring(0, abbr.Key.Length - 1);

                    // Nokta içeren kısaltmalar için daha kesin bir pattern kullan
                    // Bu pattern kısaltmanın etrafındaki boşlukları veya satır başı/sonu olmasını kontrol eder
                    text = Regex.Replace(text,
                        $@"(^|\s){Regex.Escape(abbrWithoutDot)}\.(\s|$|[,;:])",
                        $"$1{abbr.Value}$2",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    // Diğer kısaltmalar için kelime sınırı kullan
                    text = Regex.Replace(text,
                        $@"\b{Regex.Escape(abbr.Key)}\b",
                        abbr.Value,
                        RegexOptions.IgnoreCase);
                }
            }

            return text;
        }

        /// <summary>
        /// Basit SSML oluşturur
        /// </summary>
        private static string BuildBasicSSML(string text, float speakRate)
        {
            // Konuşma hızını ayarla
            string rate = speakRate > 1.0f ? $"+{(int)((speakRate - 1.0f) * 100)}%" : 
                         speakRate < 1.0f ? $"-{(int)((1.0f - speakRate) * 100)}%" : "+0%";
            
            return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{TURKISH_CULTURE}'>
                <voice name='{_currentVoiceName}'>
                    <prosody rate='{rate}' volume='{_currentVolume:F0}'>
                        {System.Security.SecurityElement.Escape(text)}
                    </prosody>
                </voice>
            </speak>";
        }

        /// <summary>
        /// Emojileri ve TTS için problemli karakterleri temizler
        /// </summary>
        private static string CleanTextForSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;


            // Emoji ve sembol temizleme sözlüğü - TTS için sorunlu olanları kaldır
            var cleanupMap = new Dictionary<string, string>
            {
                // Yaygın emojiler - çoğunu kaldır, sadece önemli olanları çevir
                { "🔴", "" },          // Kırmızı daire - KALDIRA
                { "🟢", "" },          // Yeşil daire  - KALDIR
                { "🟡", "" },          // Sarı daire - KALDIRA
                { "⚫", "" },          // Siyah daire - KALDIRA
                { "⚪", "" },          // Beyaz daire - KALDIRA
                { "🔵", "" },          // Mavi daire - KALDIRA
                { "📧", "" },          // Mail emoji - KALDIRA (çok kullanılıyor)
                { "📩", "" },          // Gelen mail - KALDIRA
                { "📨", "" },          // Giden mail - KALDIRA
                { "✅", "" },          // Tik işareti - KALDIRA
                { "❌", "" },          // X işareti - KALDIRA
                { "⭐", "" },          // Yıldız - KALDIRA
                { "💡", "" },          // Ampül - KALDIRA
                { "🎯", "" },          // Hedef - KALDIRA
                { "🚀", "" },          // Roket - KALDIRA
                { "⚡", "" },          // Şimşek - KALDIRA
                { "🔥", "" },          // Ateş - KALDIRA
                { "💰", "" },          // Para - KALDIRA
                { "📊", "" },          // Grafik - KALDIRA
                { "📈", "" },          // Artış grafiği - KALDIRA
                { "📉", "" },          // Azalış grafiği - KALDIRA
                
                // Oklar ve yön işaretleri - HEPSINI KALDIRA
                { "↩️", "" },           // Geri ok - KALDIRA
                { "↪️", "" },           // İleri ok - KALDIRA
                { "⬆️", "" },           // Yukarı ok - KALDIRA
                { "⬇️", "" },           // Aşağı ok - KALDIRA
                { "➡️", "" },           // Sağ ok - KALDIRA
                { "⬅️", "" },           // Sol ok - KALDIRA
                { "🔄", "" },           // Yenile - KALDIRA
                { "🔃", "" },           // Döngü - KALDIRA
                { "↩", "" },            // Geri ok (variation) - KALDIRA
                { "⬅", "" },            // Sol ok (variation) - KALDIRA
                { "➡", "" },            // Sağ ok (variation) - KALDIRA
                { "⬆", "" },            // Yukarı ok (variation) - KALDIRA
                { "⬇", "" },            // Aşağı ok (variation) - KALDIRA

                // Problemli özel karakterler - daha konservatif
                { "•", "" },               // Bullet point
                { "◦", "" },               // Hollow bullet  
                { "‣", "" },               // Triangular bullet
                { "▪", "" },               // Black square
                { "▫", "" },               // White square
                // Şunları KALDIR - normal metinde olabilir:
                // { "★", "yıldız" },         // ← KALDIRILDI - normal metinde kullanılabilir
                // { "☆", "yıldız" },         // ← KALDIRILDI - normal metinde kullanılabilir  
                // { "♦", "karo" },           // ← KALDIRILDI - oyun kartı simgesi
                // { "♠", "maça" },           // ← KALDIRILDI - oyun kartı simgesi
                // { "♥", "kupa" },           // ← KALDIRILDI - oyun kartı simgesi
                // { "♣", "sinek" },          // ← KALDIRILDI - oyun kartı simgesi
                
                // Sadece gerçekten sorunlu karakterler - normal noktalama işaretlerini bırak
                { "™", "" },
                { "®", "" },
                { "©", "" }
                // Şunları KALDIR - normal metni bozuyor:
                // { "§", "paragraf" },              // ← KALDIRILDI
                // { "¶", "paragraf" },              // ← KALDIRILDI  
                // { "†", "" },                      // ← KALDIRILDI
                // { "‡", "" },                      // ← KALDIRILDI
                // { "…", "nokta nokta nokta" },     // ← KALDIRILDI - çok uzun
                // { "–", "tire" },                  // ← KALDIRILDI - normal tire
                // { "—", "tire" },                  // ← KALDIRILDI - em dash
                // { "\u2018", "'" },                // ← KALDIRILDI - unicode tırnak
                // { "\u2019", "'" },                // ← KALDIRILDI - unicode tırnak
                // { "\u201C", "\"" },               // ← KALDIRILDI - unicode çift tırnak
                // { "\u201D", "\"" },               // ← KALDIRILDI - unicode çift tırnak
            };

            // Temizleme işlemini gerçekleştir
            foreach (var cleanup in cleanupMap)
            {
                text = text.Replace(cleanup.Key, cleanup.Value);
            }

            // TÜUM REGEX'LERİ GEÇİCİ OLARAK KALDIR - normal metni bozuyorlar
            // text = Regex.Replace(text, @"[\u1F600-\u1F64F]", "");  // ← KALDIRILDI
            // text = Regex.Replace(text, @"[\u1F680-\u1F6FF]", "");  // ← KALDIRILDI  
            // text = Regex.Replace(text, @"[\u1F300-\u1F5FF]", "");  // ← KALDIRILDI
            // text = Regex.Replace(text, @"[\u200D]", "");           // ← KALDIRILDI

            // Çoklu boşlukları tek boşluğa çevir
            text = Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
        }

        /// <summary>
        /// Temel SSML oluşturur - basit format
        /// </summary>
        private static string GenerateBasicSSML(string text, float speakRate)
        {
            // Metni güvenli hale getir (XML kaçış karakterleri)
            // Not: Burada metin zaten ProcessTextForSpeech() tarafından ön işlenmişti,
            // ama hala XML kaçış karakterlerine ihtiyaç var
            string safeText = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");

            // Eğer SSML etiketleri varsa onları koru
            safeText = PreserveSSMLTags(safeText);

            // SSML oluştur - basit format
            var ssmlBuilder = new StringBuilder();
            ssmlBuilder.Append("<speak version=\"1.0\" ");
            ssmlBuilder.Append("xmlns=\"http://www.w3.org/2001/10/synthesis\" ");
            ssmlBuilder.Append($"xml:lang=\"{TURKISH_CULTURE}\">");

            // Ondalık ayıracı için InvariantCulture kullan (nokta işareti ile)
            ssmlBuilder.Append($"<prosody rate=\"{speakRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" ");
            ssmlBuilder.Append($"volume=\"{_currentVolume.ToString(System.Globalization.CultureInfo.InvariantCulture)}%\">");
            ssmlBuilder.Append(safeText);
            ssmlBuilder.Append("</prosody>");
            ssmlBuilder.Append("</speak>");

            return ssmlBuilder.ToString();
        }

        /// <summary>
        /// Gelişmiş SSML oluşturur - konuşma tarzı ve ek özelliklerle
        /// </summary>
        private static string GenerateAdvancedSSML(string text, ConusmaTarzi tarzi, float speakRate, float volume)
        {
            // Metni güvenli hale getir (XML kaçış karakterleri)
            string safeText = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");

            // Eğer SSML etiketleri varsa onları koru
            safeText = PreserveSSMLTags(safeText);

            // SSML oluştur - gelişmiş format
            var ssmlBuilder = new StringBuilder();
            ssmlBuilder.Append("<speak version=\"1.0\" ");
            ssmlBuilder.Append("xmlns=\"http://www.w3.org/2001/10/synthesis\" ");
            ssmlBuilder.Append($"xml:lang=\"{TURKISH_CULTURE}\">");

            // Seçilen tarza göre SSML parametreleri belirle
            string style = "";
            float pitch = 0.0f;

            switch (tarzi)
            {
                case ConusmaTarzi.Heyecanli:
                    style = "excited";
                    pitch = 1.2f;
                    break;
                case ConusmaTarzi.Sakin:
                    style = "calm";
                    pitch = 0.8f;
                    break;
                case ConusmaTarzi.Resmi:
                    style = "formal";
                    pitch = 1.0f;
                    break;
                case ConusmaTarzi.Bilgilendirici:
                    style = "expressive";
                    pitch = 1.1f;
                    break;
                case ConusmaTarzi.Vurgulu:
                    style = "emphasis";
                    pitch = 1.3f;
                    break;
                default:
                    style = "general";
                    pitch = 1.0f;
                    break;
            }

            // Konuşma tarzını belirt (Microsoft SSML uyumlu)
            ssmlBuilder.Append($"<prosody rate=\"{speakRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" ");
            ssmlBuilder.Append($"pitch=\"{pitch.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" ");
            ssmlBuilder.Append($"volume=\"{volume.ToString(System.Globalization.CultureInfo.InvariantCulture)}%\">");

            // Konuşma tarzını belirt
            ssmlBuilder.Append($"<mstts:express-as style=\"{style}\" xmlns:mstts=\"http://www.w3.org/2001/mstts\">");

            ssmlBuilder.Append(safeText);

            ssmlBuilder.Append("</mstts:express-as>");
            ssmlBuilder.Append("</prosody>");
            ssmlBuilder.Append("</speak>");

            return ssmlBuilder.ToString();
        }

        /// <summary>
        /// Metindeki SSML etiketlerini korur
        /// </summary>
        private static string PreserveSSMLTags(string text)
        {
            // Bu kısaltmalar özel işlemesiz SSML etiketlerini korumak için
            // Örneğin, <break> etiketlerini koruyalım
            text = text.Replace("&lt;break", "<break")
                       .Replace("time=&quot;", "time=\"")
                       .Replace("&quot;/&gt;", "\"/>")
                       .Replace("&lt;/break&gt;", "</break>");

            return text;
        }

        #endregion

        #region SSML içeriğini seslendirir (dahili kullanım)

        /// <summary>
        /// SSML içeriğini seslendirir (dahili kullanım)
        /// </summary>
        private static async Task<bool> InternalSpeakAsync(
            string ssml,
            bool interruptCurrent,
            CancellationToken cancellationToken)
        {
            SpeechSynthesisStream stream = null;
            bool lockAcquired = false;
            int currentRetryCount = 0;

            while (currentRetryCount <= _retryCount)
            {
                try
                {
                    // Thread-safe işlem için lock al
                    Monitor.Enter(_lock, ref lockAcquired);

                    // İptal isteği varsa çık
                    if (cancellationToken.IsCancellationRequested)
                    {
                        RaiseSpeechCancelled();
                        return false;
                    }

                    // Mevcut konuşma varsa ve kesme isteği yoksa çık
                    if ((CurrentState == SpeechState.Speaking || CurrentState == SpeechState.Synthesizing) && !interruptCurrent)
                    {
                        return false;
                    }

                    // Mevcut konuşma varsa durdur
                    if ((CurrentState == SpeechState.Speaking || CurrentState == SpeechState.Synthesizing) && interruptCurrent)
                    {
                        _playbackCts?.Cancel();
                        _playbackCts?.Dispose();
                        _playbackCts = null;
                        _mediaPlayer.Pause();
                        _mediaPlayer.Source = null;
                    }

                    // Durumu güncelle
                    CurrentState = SpeechState.Synthesizing;

                    // İptal için yeni token oluştur
                    _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // SSML işlemi uzun sürebileceği için lock'u bırak
                    Monitor.Exit(_lock);
                    lockAcquired = false;

                    // Dispose kontrolü
                    if (_isDisposed || _synthesizer == null)
                    {
                        return false;
                    }

                    // SSML içeriğini sentezle
                    stream = await _synthesizer.SynthesizeSsmlToStreamAsync(ssml);

                    // Sentezleme sonrası iptal kontrolü
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (stream != null)
                        {
                            stream.Dispose();
                            stream = null;
                        }
                        RaiseSpeechCancelled();
                        return false;
                    }

                    // Lock'u tekrar al
                    Monitor.Enter(_lock, ref lockAcquired);

                    // Oynatma öncesi son kontroller
                    if (_playbackCts.IsCancellationRequested)
                    {
                        CurrentState = SpeechState.Idle;
                        RaiseSpeechCancelled();
                        return false;
                    }

                    // Dispose kontrolü
                    if (_isDisposed || _mediaPlayer == null)
                    {
                        stream?.Dispose();
                        return false;
                    }

                    // Medya kaynağını oluştur ve oynat
                    _mediaPlayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                    _mediaPlayer.Play();

                    // Durumu güncelle
                    CurrentState = SpeechState.Speaking;

                    // Olayı tetikle
                    RaiseSpeechStarted();
                    return true;
                }
                catch (OperationCanceledException)
                {
                    RaiseSpeechCancelled();
                    return false;
                }
                catch (Exception ex)
                {
                    currentRetryCount++;

                    if (currentRetryCount <= _retryCount)
                    {

                        // Lock'ı bırak ve yeniden dene
                        if (lockAcquired)
                        {
                            Monitor.Exit(_lock);
                            lockAcquired = false;
                        }

                        // Stream henüz dispose edilmediyse temizle
                        if (stream != null)
                        {
                            stream.Dispose();
                            stream = null;
                        }

                        // Bekle ve yeniden dene
                        await Task.Delay(_retryDelay);
                    }
                    else
                    {
                        CurrentState = SpeechState.Failed;
                        RaiseSpeechFailed(ex, ssml);
                        return false;
                    }
                }
                finally
                {
                    // Lock hala alınmışsa bırak
                    if (lockAcquired)
                    {
                        Monitor.Exit(_lock);
                    }

                    // Stream henüz dispose edilmediyse ve son deneme başarısızsa temizle
                    if (stream != null && CurrentState != SpeechState.Speaking && currentRetryCount > _retryCount)
                    {
                        stream.Dispose();
                    }
                }
            }

            return false; // Tüm denemeler başarısız olduysa false dön
        }

        #endregion

        #region Konuşma Durdurma Metodları

        /// <summary>
        /// Mevcut konuşmayı durdurur
        /// </summary>
        public static async Task<bool> StopSpeakingAsync()
        {
            bool lockAcquired = false;
            try
            {
                Monitor.Enter(_lock, ref lockAcquired);

                if (CurrentState == SpeechState.Idle)
                {
                    return true;
                }

                // Debug.WriteLine("[TTS] Mevcut konuşma durduruluyor.");
                CurrentState = SpeechState.Stopping;

                // MediaPlayer'ı durdur
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Source = null;
                    _mediaPlayer.Pause();
                }

                // CancellationToken'ı iptal et
                _playbackCts?.Cancel();
                _playbackCts?.Dispose();
                _playbackCts = null;

                // Durumu güncelle
                CurrentState = SpeechState.Idle;
                
                RaiseSpeechCancelled();
                
                return true;
            }
            catch (Exception)
            {
                CurrentState = SpeechState.Failed;
                return false;
            }
            finally
            {
                if (lockAcquired)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        /// <summary>
        /// Mevcut konuşmayı zorla durdurur (senkron)
        /// </summary>
        public static void ForceStopSpeaking()
        {
            try
            {
                lock (_lock)
                {
                    if (CurrentState == SpeechState.Idle)
                    {
                        return;
                    }

                    CurrentState = SpeechState.Stopping;

                    // MediaPlayer'ı anında durdur
                    if (_mediaPlayer != null)
                    {
                        _mediaPlayer.Source = null;
                        _mediaPlayer.Pause();
                    }

                    // CancellationToken'ı iptal et
                    _playbackCts?.Cancel();
                    _playbackCts?.Dispose();
                    _playbackCts = null;

                    // Durumu güncelle
                    CurrentState = SpeechState.Idle;
                    
                    RaiseSpeechCancelled();
                }
            }
            catch (Exception)
            {
                CurrentState = SpeechState.Failed;
            }
        }

        /// <summary>
        /// Konuşma aktif mi kontrol eder
        /// </summary>
        public static bool IsSpeaking()
        {
            lock (_lock)
            {
                return CurrentState == SpeechState.Speaking || CurrentState == SpeechState.Synthesizing;
            }
        }
        
        /// <summary>
        /// Edge TTS oynatma durumunu sıfırlar (WebView'dan TTS bittiğinde çağrılır)
        /// </summary>
        public static void ResetEdgeTTSState()
        {
            lock (_lock)
            {
                _isEdgeTTSPlaying = false;
            }
        }

        #endregion

        #region Olay Yönetimi ve Yardımcı Metodlar

        /// <summary>
        /// MediaPlayer.MediaEnded olayı işleyicisi
        /// </summary>
        private static void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            lock (_lock)
            {
                if (CurrentState == SpeechState.Speaking)
                {
                    CurrentState = SpeechState.Idle;
                    _mediaPlayer.Source = null;
                    _playbackCts?.Dispose();
                    _playbackCts = null;
                    RaiseSpeechCompleted();
                }
            }
        }

        /// <summary>
        /// MediaPlayer.MediaFailed olayı işleyicisi
        /// </summary>
        private static void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            lock (_lock)
            {
                CurrentState = SpeechState.Failed;
                _mediaPlayer.Source = null;
                _playbackCts?.Dispose();
                _playbackCts = null;

                var exception = new Exception($"MediaPlayer Hatası: {args.ErrorMessage}", args.ExtendedErrorCode);
                RaiseSpeechFailed(exception, "Medya Oynatma");
            }
        }

        /// <summary>
        /// Konuşma başladı olayını tetikler
        /// </summary>
        private static void RaiseSpeechStarted()
        {
            try 
            { 
                // ÇÖZÜM: TTS başladığında sistem mikrofonunu geçici olarak sustur
                SpeechStarted?.Invoke(null, EventArgs.Empty); 
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Konuşma tamamlandı olayını tetikler
        /// </summary>
        private static void RaiseSpeechCompleted()
        {
            try 
            { 
                // ÇÖZÜM: TTS bittiğinde mikrofon tekrar aktif
                SpeechCompleted?.Invoke(null, EventArgs.Empty); 
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Konuşma iptal edildi olayını tetikler
        /// </summary>
        private static void RaiseSpeechCancelled()
        {
            try { SpeechCancelled?.Invoke(null, EventArgs.Empty); }
            catch (Exception) { }
        }

        /// <summary>
        /// Konuşma hatası olayını tetikler
        /// </summary>
        private static void RaiseSpeechFailed(Exception error, string context)
        {
            try { SpeechFailed?.Invoke(null, new SpeechErrorEventArgs(error, context)); }
            catch (Exception) { }
        }

        #endregion
    }
}