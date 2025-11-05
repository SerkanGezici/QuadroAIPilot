using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using QuadroAIPilot.Services;
using QuadroAIPilot.State;
using QuadroAIPilot.Constants;
using QuadroAIPilot.Interfaces;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Commands;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// TTS çıktısını filtrelemek için gelişmiş yardımcı sınıf
    /// </summary>
    internal class TTSOutputFilter
    {
        private readonly object _lockObject = new object();
        private string _currentTTSText = string.Empty;
        private DateTime _ttsStartTime = DateTime.MinValue;
        private List<string> _ttsTextHistory = new List<string>(); // Son TTS metinleri
        private const int TEXT_SIMILARITY_THRESHOLD = 70; // %70 benzerlik
        private const int TIME_WINDOW_MS = 5000; // TTS başladıktan sonra 5 saniye
        private const int HISTORY_SIZE = 5; // Son 5 TTS metnini sakla
        
        /// <summary>
        /// TTS metnini güncelle
        /// </summary>
        public void UpdateTTSText(string text)
        {
            lock (_lockObject)
            {
                _currentTTSText = text?.ToLowerInvariant() ?? string.Empty;
                _ttsStartTime = DateTime.UtcNow;
                
                // Metni history'ye ekle
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _ttsTextHistory.Add(_currentTTSText);
                    
                    // History boyutunu sınırla
                    if (_ttsTextHistory.Count > HISTORY_SIZE)
                    {
                        _ttsTextHistory.RemoveAt(0);
                    }
                }
                
                string shortText = text != null && text.Length > 50 ? text.Substring(0, 50) + "..." : text ?? "";
                LogService.LogDebug($"[TTSOutputFilter] TTS text updated: '{shortText}'");
            }
        }
        
        /// <summary>
        /// TTS içeriğini güncelle (yeni metod adı)
        /// </summary>
        public void UpdateTtsContent(string text)
        {
            UpdateTTSText(text);
        }
        
        /// <summary>
        /// Gelişmiş TTS çıktısı kontrolü
        /// </summary>
        public bool IsTTSOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            lock (_lockObject)
            {
                // TTS metni yoksa ve history boşsa
                if (string.IsNullOrWhiteSpace(_currentTTSText) && _ttsTextHistory.Count == 0) 
                    return false;
                
                string normalizedInput = text.ToLowerInvariant().Trim();
                
                // Zaman penceresi kontrolü
                var elapsed = DateTime.UtcNow - _ttsStartTime;
                bool inTimeWindow = elapsed.TotalMilliseconds <= TIME_WINDOW_MS;
                
                // 1. Mevcut TTS metni kontrolü (zaman penceresi içinde)
                if (inTimeWindow && !string.IsNullOrWhiteSpace(_currentTTSText))
                {
                    // Tam eşleşme
                    if (normalizedInput == _currentTTSText)
                    {
                        LogService.LogDebug("[TTSOutputFilter] Exact match with current TTS - filtered");
                        return true;
                    }
                    
                    // Kısmi eşleşme (başlangıç veya parça)
                    if (IsPartialMatch(normalizedInput, _currentTTSText))
                    {
                        LogService.LogDebug("[TTSOutputFilter] Partial match with current TTS - filtered");
                        return true;
                    }
                    
                    // Benzerlik kontrolü
                    int similarity = CalculateSimilarity(normalizedInput, _currentTTSText);
                    if (similarity >= TEXT_SIMILARITY_THRESHOLD)
                    {
                        LogService.LogDebug($"[TTSOutputFilter] High similarity ({similarity}%) with current TTS - filtered");
                        return true;
                    }
                }
                
                // 2. History kontrolü (gecikmiş veya parçalanmış TTS metinleri için)
                foreach (string historicalTTS in _ttsTextHistory)
                {
                    if (normalizedInput == historicalTTS)
                    {
                        LogService.LogDebug("[TTSOutputFilter] Exact match in history - filtered");
                        return true;
                    }
                    
                    if (IsPartialMatch(normalizedInput, historicalTTS))
                    {
                        LogService.LogDebug("[TTSOutputFilter] Partial match in history - filtered");
                        return true;
                    }
                    
                    int historySimilarity = CalculateSimilarity(normalizedInput, historicalTTS);
                    if (historySimilarity >= TEXT_SIMILARITY_THRESHOLD)
                    {
                        LogService.LogDebug($"[TTSOutputFilter] High similarity ({historySimilarity}%) in history - filtered");
                        return true;
                    }
                }
                
                // 3. Kelime bazlı kontrol (TTS metni parçalanmış olabilir)
                if (IsWordBasedMatch(normalizedInput, _currentTTSText) || 
                    _ttsTextHistory.Any(h => IsWordBasedMatch(normalizedInput, h)))
                {
                    LogService.LogDebug("[TTSOutputFilter] Word-based match detected - filtered");
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Kısmi eşleşme kontrolü
        /// </summary>
        private bool IsPartialMatch(string input, string ttsText)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(ttsText))
                return false;
                
            // Giriş TTS'in başlangıcı mı?
            if (ttsText.StartsWith(input) && input.Length > 3)
                return true;
                
            // Giriş TTS'in bir parçası mı? (en az 5 karakter)
            if (input.Length >= 5 && ttsText.Contains(input))
                return true;
                
            // TTS girişin bir parçası mı? (TTS kısa, giriş uzunsa)
            if (ttsText.Length >= 5 && input.Contains(ttsText))
                return true;
                
            return false;
        }
        
        /// <summary>
        /// Kelime bazlı eşleşme kontrolü
        /// </summary>
        private bool IsWordBasedMatch(string input, string ttsText)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(ttsText))
                return false;
                
            var inputWords = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ttsWords = ttsText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Giriş çok kısa ise
            if (inputWords.Length < 3)
                return false;
                
            // Ortak kelime sayısı
            int commonWords = inputWords.Count(iw => ttsWords.Contains(iw));
            
            // En az %60 kelime eşleşmesi
            float matchRatio = (float)commonWords / Math.Min(inputWords.Length, ttsWords.Length);
            return matchRatio >= 0.6f;
        }
        
        /// <summary>
        /// İki metin arasındaki benzerlik yüzdesini hesapla
        /// </summary>
        private int CalculateSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0;
            
            int distance = LevenshteinDistance(text1, text2);
            int maxLength = Math.Max(text1.Length, text2.Length);
            
            if (maxLength == 0) return 100;
            
            return (int)((1.0 - (double)distance / maxLength) * 100);
        }
        
        /// <summary>
        /// Levenshtein mesafesi algoritması
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;
            
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            
            // İlk satır ve sütunu doldur
            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;
            
            // Mesafe matrisini hesapla
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            
            return d[s1.Length, s2.Length];
        }
        
        /// <summary>
        /// TTS filtresini temizle
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _currentTTSText = string.Empty;
                _ttsStartTime = DateTime.MinValue;
                _ttsTextHistory.Clear();
                LogService.LogDebug("[TTSOutputFilter] Filter cleared");
            }
        }
    }

    /// <summary>
    /// Dikte (ses tanıma) yönetimi için ayrılmış sınıf - Sadece Web Speech API
    /// </summary>
    public class DictationManager : IDictationManager
    {
        private readonly System.Timers.Timer _debounceTimer;
        private readonly ModeManager _modeManager;
        private IWebViewManager _webViewManager;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        
        // State
        private volatile bool _processingDictation;
        private volatile bool _dictationActive;
        private volatile bool _isRestartingDictation;
        private volatile bool _assistantIsSpeaking;
        private string _pendingText = string.Empty;
        private string _lastProcessedText = string.Empty;
        private string _lastTtsResponse = string.Empty;
        private string _lastModeChangeCommand = string.Empty;
        private DateTime _lastModeChangeTime = DateTime.MinValue;
        
        // TTS Output Filtering
        private readonly TTSOutputFilter _ttsFilter = new TTSOutputFilter();
        
        // VAD tabanlı mikrofon kontrolü
        private VoiceActivityDetector _vad;
        private SmartMicrophoneAdjuster _microphoneAdjuster;
        
        // Web Speech API desteği
        private WebSpeechBridge _webSpeechBridge;
        private static WebSpeechBridge _staticWebSpeechBridge; // ServiceContainer yerine static referans

        // Events
        // TextRecognized event'i kaldırıldı - artık ModeManager üzerinden işleniyor
        public event EventHandler<DictationStateChangedEventArgs>? StateChanged;

        // Properties
        public bool IsActive => _dictationActive;
        public bool IsProcessing => _processingDictation;
        public bool IsRestarting => _isRestartingDictation;
        
        /// <summary>
        /// Sets the WebViewManager instance
        /// </summary>
        public void SetWebViewManager(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            
            // Web Speech Bridge'i oluştur
            _webSpeechBridge = new WebSpeechBridge(_webViewManager, this);
            _webSpeechBridge.StateChanged += OnWebSpeechStateChanged;
            
            // Static referansı güncelle (ServiceContainer alternatifi)
            _staticWebSpeechBridge = _webSpeechBridge;
            LogService.LogDebug("[DictationManager] WebSpeechBridge static referansı güncellendi");
        }
        
        /// <summary>
        /// WebSpeechBridge'e static erişim (ServiceContainer alternatifi)
        /// </summary>
        public static WebSpeechBridge GetWebSpeechBridge()
        {
            return _staticWebSpeechBridge;
        }

        public DictationManager(ModeManager modeManager)
        {
            _modeManager = modeManager ?? throw new ArgumentNullException(nameof(modeManager));
            
            _debounceTimer = new System.Timers.Timer(AppConstants.DefaultDebounceDelayMs) { AutoReset = false };
            _debounceTimer.Elapsed += OnDebounceTimerElapsed;
            
            // VAD tabanlı mikrofon kontrolünü initialize et
            InitializeMicrophoneControl();

            // TTS events'larını dinle
            TextToSpeechService.SpeechStarted += (_, _) => 
            {
                _assistantIsSpeaking = true;
                
                // TTS başladı
                LogService.LogInfo($"[DictationManager] TTS başladı - _assistantIsSpeaking = true, Smart Filter aktif");
            };
            
            TextToSpeechService.SpeechCompleted += (_, _) => 
            {
                _assistantIsSpeaking = false;
                LogService.LogInfo("[DictationManager] TTS tamamlandı - _assistantIsSpeaking = false");
                
                // Filtreyi biraz gecikmeli temizle (TTS'in yankıları için)
                Task.Delay(1000).ContinueWith(_ => 
                {
                    _ttsFilter.Clear();
                    LogService.LogDebug("[DictationManager] TTS filtresi temizlendi (1 saniye gecikme ile)");
                });
            };
            
            
            TextToSpeechService.SpeechCancelled += (_, _) => 
            {
                _assistantIsSpeaking = false;
                LogService.LogDebug("[DictationManager] TTS iptal edildi");
                _ttsFilter.Clear();
            };
        }

        public void ProcessTextChanged(string text)
        {
            text = text.Trim();
            if (string.IsNullOrWhiteSpace(text)) 
            {
                return;
            }
            
            LogService.LogInfo($"[DictationManager] Metin alındı: '{text}'");
            
            // YAZIM MODUNDA - doğrudan aktif pencereye gönder
            if (AppState.CurrentMode == AppState.UserMode.Writing)
            {
                // Sadece "komut moduna geç" kontrolü
                if (text.ToLowerInvariant().Contains("komut moduna geç"))
                {
                    LogService.LogInfo($"[DictationManager] Yazı modundan komut moduna geçiş komutu: '{text}'");
                    _lastModeChangeCommand = text;
                    _lastModeChangeTime = DateTime.UtcNow;
                    
                    // Direkt mod değişikliği yap
                    _modeManager.Switch(AppState.UserMode.Command);
                    _ = TextToSpeechService.SpeakTextAsync("Komut moduna geçildi");
                    return;
                }
                
                // Son mod değişikliği komutunu tekrar yazmayı önle
                if (!string.IsNullOrEmpty(_lastModeChangeCommand) && 
                    text.Equals(_lastModeChangeCommand, StringComparison.OrdinalIgnoreCase) &&
                    (DateTime.UtcNow - _lastModeChangeTime).TotalSeconds < 3)
                {
                    LogService.LogInfo($"[DictationManager] Mod değişikliği komutu tekrarı engellendi: '{text}'");
                    return;
                }
                
                // Diğer tüm metinleri WritingMode'a gönder
                LogService.LogInfo($"[DictationManager] Yazı modunda - metin WritingMode'a gönderiliyor: '{text}'");
                bool handled = _modeManager.RouteSpeech(text);
                LogService.LogDebug($"[DictationManager] WritingMode.HandleSpeech sonucu: {handled}");
                return;
            }
            
            // KOMUT MODUNDA - mevcut mantık devam eder
            // TTS çıktısı mı kontrol et - ama interrupt komutlarına izin ver
            if (_assistantIsSpeaking)
            {
                bool isTTSOutput = _ttsFilter.IsTTSOutput(text);
                bool isInterrupt = IsInterruptCommand(text);

                LogService.LogInfo($"[DictationManager] ProcessTextChanged - TTS konuşuyor. Text: '{text}', IsTTSOutput: {isTTSOutput}, IsInterrupt: {isInterrupt}");

                if (isTTSOutput && !isInterrupt)
                {
                    LogService.LogInfo($"[DictationManager] TTS output FILTERED (blocked): '{text}'");
                    return;
                }
                else if (!isTTSOutput)
                {
                    LogService.LogInfo($"[DictationManager] TTS konuşurken farklı metin algılandı, işleme devam: '{text}'");
                }
            }
            else
            {
                LogService.LogDebug($"[DictationManager] _assistantIsSpeaking = false, normal işleme: '{text}'");
            }
            
            if (_processingDictation) 
            {
                return;
            }
            if (text == _lastProcessedText) 
            {
                return;
            }
            if (!string.IsNullOrEmpty(_lastTtsResponse) && text.Contains(_lastTtsResponse)) 
            {
                return;
            }

            // Wake word kontrolü ÖNCE yapılmalı
            string lowerText = text.ToLowerInvariant().TrimEnd('.', ',', '!', '?');
            if (lowerText == "hey quadro" || lowerText == "hey cuadro" || lowerText == "hey kuadro")
            {
                LogService.LogInfo($"[DictationManager] Wake word algılandı: '{text}'");
                StartProcessing(text);
                return;
            }

            // Mod geçiş komutlarını hemen kontrol et
            if (AppConstants.ModeCommands.Any(cmd => lowerText.Contains(cmd)))
            {
                _lastModeChangeCommand = text;
                _lastModeChangeTime = DateTime.UtcNow;
                StartProcessing(text);
                return;
            }

            // AI MODUNDA TÜM METİNLER İŞLENİR (FİLTRE YOK!)
            if (AppState.CurrentMode == AppState.UserMode.AI)
            {
                LogService.LogInfo($"[DictationManager] AI modunda, tüm metinler işlenir: '{text}'");
                StartProcessing(text);
                return;
            }

            // KOMUT VE YAZIM MODUNDA: Komut algılama
            bool shouldProcess = ShouldProcessText(text);
            LogService.LogInfo($"[DictationManager] ShouldProcessText('{text}') = {shouldProcess}");

            if (shouldProcess)
            {
                LogService.LogInfo($"[DictationManager] Komut algılandı, StartProcessing çağrılıyor: '{text}'");
                StartProcessing(text);
                return;
            }

            _pendingText = text;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_processingDictation) return;

            string text = _pendingText;
            _pendingText = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;

            if (ShouldProcessText(text))
            {
                StartProcessing(text);
            }
        }

        private bool ShouldProcessText(string text)
        {
            string singleWord = text.Trim().ToLowerInvariant();
            
            // 0. Sayfa navigasyon komutlarını kontrol et (tam metin eşleşme)
            string lowerText = text.ToLowerInvariant().TrimEnd('.');
            if (lowerText == "sayfa başına git" || lowerText == "sayfa sonuna git")
            {
                LogService.LogInfo($"[DictationManager] Sayfa navigasyon komutu algılandı: {text}");
                return true;
            }

            // 1. Ses komutlarını kontrol et
            if (AppConstants.VolumeRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Ses komutu algılandı: {text}");
                return true;
            }

            // 1.5. Mail komutlarını kontrol et
            if (AppConstants.MailRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Mail komutu algılandı: {text}");
                return true;
            }

            // 1.6. MAPI komutlarını kontrol et
            if (AppConstants.MAPIRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] MAPI komutu algılandı: {text}");
                return true;
            }
            
            // 1.7. Practical MAPI komutlarını kontrol et
            if (AppConstants.PracticalMAPIRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Practical MAPI komutu algılandı: {text}");
                return true;
            }

            // 1.8. Takvim/Toplantı komutlarını kontrol et
            if (AppConstants.CalendarRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Takvim komutu algılandı: {text}");
                return true;
            }

            // 1.9. Not komutlarını kontrol et
            if (AppConstants.NoteRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Not komutu algılandı: {text}");
                return true;
            }

            // 1.10. Görev komutlarını kontrol et
            if (AppConstants.TaskRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Görev komutu algılandı: {text}");
                return true;
            }

            // 1.11. Wikipedia komutlarını kontrol et
            if (AppConstants.WikipediaRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Wikipedia komutu algılandı: {text}");
                return true;
            }

            // 1.12. Haber komutlarını kontrol et
            if (AppConstants.NewsRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Haber komutu algılandı: {text}");
                return true;
            }

            // 1.13. Twitter komutlarını kontrol et
            if (AppConstants.TwitterRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Twitter komutu algılandı: {text}");
                return true;
            }

            // 1.14. Hava durumu komutlarını kontrol et
            if (AppConstants.WeatherRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Hava durumu komutu algılandı: {text}");
                return true;
            }

            // 1.15. Edge TTS komutlarını kontrol et
            if (AppConstants.EdgeTTSRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Edge TTS komutu algılandı: {text}");
                return true;
            }
            
            // 1.16. Test ses komutlarını kontrol et
            if (AppConstants.TestAudioRegex.IsMatch(text))
            {
                LogService.LogInfo($"[DictationManager] Test ses komutu algılandı: {text}");
                return true;
            }

            // 2. Tek kelimelik özel komutları kontrol et
            if (AppConstants.SingleWordCommands.Contains(singleWord))
            {
                LogService.LogInfo($"[DictationManager] Tek kelimelik komut algılandı: {singleWord}");
                return true;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int wordCount = words.Length;

            // 3. Kısa komutları kontrol et
            if (wordCount <= 2)
            {
                bool hasVerb = AppConstants.VerbRegex.IsMatch(text);
                bool isSpecialCommand = IsSpecialShortCommand(text);
                bool isNewsCommand = text.ToLowerInvariant().Contains("haberleri") || text.ToLowerInvariant().Contains("haberlerde") || text.ToLowerInvariant().Contains("haberlerini");
                
                if (!hasVerb && !isSpecialCommand && !isNewsCommand)
                {
                    LogService.LogInfo($"[DictationManager] Ara dikte (fiilsiz ≤2 kelime) atlandı: {text}");
                    return false;
                }
            }

            // 4. Geniş komut listesi kontrolü
            if (wordCount >= 2 && !HasCommandVerb(text))
            {
                return false;
            }

            return true;
        }

        private bool IsSpecialShortCommand(string text)
        {
            string normalizedText = text.ToLowerInvariant();
            
            // Tam eşleşme kontrolü
            if (AppConstants.SpecialShortCommands.Contains(normalizedText))
                return true;
            
            // İçerme kontrolü
            return AppConstants.SpecialShortCommands.Any(cmd => normalizedText.Contains(cmd));
        }

        private bool HasCommandVerb(string text)
        {
            return AppConstants.CommandVerbs.Any(verb => 
                text.Contains(verb, StringComparison.OrdinalIgnoreCase)) ||
                   AppConstants.SystemFolders.Any(folder => 
                text.Contains(folder, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// TTS'i kesmeye izin verilen komutlar
        /// </summary>
        private bool IsInterruptCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            string normalized = text.ToLowerInvariant();
            
            // Interrupt komutları whitelist
            string[] interruptCommands = new[]
            {
                "dur", "stop", "sus", "kes", "tamam", "yeter",
                "teşekkür", "teşekkürler", "sağol", "sağ ol",
                "komut modu", "yazı modu",
                "eposta", "e-posta", "mail",
                "haber", "wikipedia", "twitter",
                "ses yükselt", "ses azalt", "ses kapat",
                "mikrofon aç", "mikrofon kapat"
            };
            
            // Komutlardan herhangi biri varsa interrupt'a izin ver
            return interruptCommands.Any(cmd => normalized.Contains(cmd));
        }

        private void StartProcessing(string text)
        {
            lock (_lockObject)
            {
                if (_processingDictation)
                {
                    LogService.LogWarning($"[DictationManager] Processing already in progress, skipping: '{text}'");
                    return; // Prevent race condition
                }
                _processingDictation = true;
            }

            _lastProcessedText = text;
            NotifyStateChanged();

            LogService.LogInfo($"[DictationManager] StartProcessing - ModeManager.RouteSpeech çağrılıyor: '{text}'");

            // Sadece ModeManager üzerinden yönlendir (duplicate processing önlenir)
            bool routeResult = _modeManager.RouteSpeech(text);
            LogService.LogInfo($"[DictationManager] ModeManager.RouteSpeech('{text}') = {routeResult}");

            // AI Mode async olarak çalıştığı için hemen reset et
            // Komut mode'da ise CommandProcessor tamamlandığında reset eder
            if (AppState.CurrentMode == AppState.UserMode.AI)
            {
                // AI mode kendi queue sistemini kullanır, flag'i hemen temizle
                Task.Run(async () =>
                {
                    await Task.Delay(100); // Küçük delay ile queue'ya girmeyi sağla
                    lock (_lockObject)
                    {
                        _processingDictation = false;
                    }
                    LogService.LogInfo("[DictationManager] AI mode processing flag reset");
                });
            }
        }

        public void Stop()
        {
            if (_dictationActive)
            {
                // Web Speech API'yi durdur
                if (_webSpeechBridge != null)
                {
                    _ = Task.Run(async () => await _webSpeechBridge.StopWebSpeechAsync());
                }
                
                _dictationActive = false;
                
                // VAD tabanlı mikrofon kontrolünü durdur
                StopMicrophoneControl();
                
                NotifyStateChanged();
            }
        }

        public async Task StartAsync(bool forceRestart = false)
        {
            if (_isRestartingDictation && !forceRestart)
            {
                return;
            }

            Stop();
            await Task.Delay(100);
            
            // Filter'ı temizle
            _ttsFilter.Clear();
            LogService.LogDebug("[DictationManager] TTS filter temizlendi");

            _isRestartingDictation = true;
            NotifyStateChanged();

            try
            {
                if (_webSpeechBridge != null)
                {
                    // Web Speech API'yi başlat
                    LogService.LogDebug("[DictationManager] Web Speech API başlatılıyor...");
                    
                    await _webSpeechBridge.StartWebSpeechAsync();
                    _dictationActive = true;
                    
                    // VAD tabanlı mikrofon kontrolünü başlat
                    await StartMicrophoneControl();
                }
                else
                {
                    LogService.LogDebug("[DictationManager] WebSpeechBridge henüz hazır değil");
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[DictationManager] Dikte başlatma hatası: {ex.Message}");
            }
            finally
            {
                _isRestartingDictation = false;
                NotifyStateChanged();
            }
        }

        // Public interface metodları
        public void StartDictation()
        {
            _ = StartAsync();
        }

        public void StopDictation()
        {
            Stop();
        }
        
        /// <summary>
        /// Diksiyon durumunu toggle eder - sadece Web Speech API
        /// </summary>
        public async Task<bool> ToggleDictation()
        {
            try
            {
                LogService.LogDebug($"[DictationManager] ToggleDictation - Mevcut durum: {(_dictationActive ? "Aktif" : "Pasif")}");
                
                if (_dictationActive)
                {
                    // Web Speech API'yi durdur
                    await _webSpeechBridge.StopWebSpeechAsync();
                    _dictationActive = false;
                    StopMicrophoneControl();
                    LogService.LogDebug("[DictationManager] ToggleDictation - Web Speech API durduruldu");
                }
                else
                {
                    // Web Speech API'yi başlat
                    await StartAsync();
                    LogService.LogDebug("[DictationManager] ToggleDictation - Web Speech API başlatıldı");
                }
                
                // State değişikliğini notify et
                NotifyStateChanged();
                
                return _dictationActive;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[DictationManager] ToggleDictation hatası: {ex.Message}");
                return _dictationActive;
            }
        }

        public async Task RestartDictationAsync()
        {
            // Filter'ı temizle
            _ttsFilter.Clear();
            LogService.LogDebug("[DictationManager] RestartDictationAsync - TTS filter temizlendi");
            
            await StartAsync(true);
        }

        public void HandleTextChanged(string text)
        {
            ProcessTextChanged(text);
        }

        public void ProcessText(string text)
        {
            StartProcessing(text);
        }

        public void SetProcessingComplete()
        {
            lock (_lockObject)
            {
                _processingDictation = false;
                // Komut tamamlandıktan sonra aynı komutu tekrar çalıştırabilmek için text'i temizle
                _lastProcessedText = string.Empty;
            }
            NotifyStateChanged();
        }

        public void UpdateTtsResponse(string response)
        {
            _lastTtsResponse = response;
            
            LogService.LogDebug($"[DictationManager] TTS Response kaydedildi: '{response}'");
            
            // TTS'in kullandığı kelime öbeklerini de blokla
            var ttsWords = response.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ttsWords.Length > 3)
            {
                // İlk 5 kelimeyi blokla (feedback loop önleme)
                var blockPhrase = string.Join(" ", ttsWords.Take(5));
                LogService.LogDebug($"[DictationManager] TTS Blok frazi: '{blockPhrase}'");
                _lastTtsResponse = blockPhrase;
            }
        }
        
        /// <summary>
        /// TTS içeriğini günceller (filtreleme için)
        /// </summary>
        public void UpdateTtsContent(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                _ttsFilter.UpdateTtsContent(content);
                LogService.LogDebug($"[DictationManager] TTS içeriği filtreleme için güncellendi: {content.Substring(0, Math.Min(50, content.Length))}...");
            }
        }
        
        /// <summary>
        /// Mod değişikliğinde state'leri temizler
        /// </summary>
        public void ResetStateForModeChange()
        {
            lock (_lockObject)
            {
                _processingDictation = false;
                _lastProcessedText = string.Empty;
                _pendingText = string.Empty;
                _lastModeChangeCommand = string.Empty;
                _lastModeChangeTime = DateTime.MinValue;
                _debounceTimer.Stop();
            }
            
            LogService.LogInfo("[DictationManager] State sıfırlandı (mod değişikliği)");
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke(this, new DictationStateChangedEventArgs
            {
                IsActive = _dictationActive,
                IsProcessing = _processingDictation,
                IsRestarting = _isRestartingDictation
            });
            
            // WebView'a dikte durumunu bildir
            if (_webViewManager != null)
            {
                _webViewManager.UpdateDictationState(_dictationActive);
            }
        }

        /// <summary>
        /// VAD tabanlı mikrofon kontrolünü initialize et
        /// </summary>
        private void InitializeMicrophoneControl()
        {
            try
            {
                // VAD Singleton instance'ını al
                _vad = VoiceActivityDetector.Instance;
                
                // SmartMicrophoneAdjuster'ı oluştur
                _microphoneAdjuster = new SmartMicrophoneAdjuster(_vad);
                
                // Mikrofon ayarlama eventlerini dinle
                _microphoneAdjuster.MicrophoneAdjusted += OnMicrophoneAdjusted;
                
                LogService.LogDebug("[DictationManager] VAD tabanlı mikrofon kontrolü initialize edildi");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[DictationManager] Mikrofon kontrol initialization hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mikrofon seviyesi ayarlandığında çağrılır
        /// </summary>
        private void OnMicrophoneAdjusted(object sender, MicrophoneAdjustmentEventArgs e)
        {
            string speakingStatus = e.IsUserSpeaking ? "Kullanıcı konuşuyor" : "Arka plan";
            LogService.LogVerbose($"[DictationManager] Mikrofon ayarlandı: {e.CurrentLevel:F0}% (Hedef: {e.TargetLevel:F0}%) - {speakingStatus}");
        }
        
        /// <summary>
        /// VAD tabanlı mikrofon kontrolünü başlat
        /// </summary>
        private async Task StartMicrophoneControl()
        {
            if (_vad == null || _microphoneAdjuster == null) return;
            
            try
            {
                // VAD'i başlat
                bool vadStarted = await _vad.StartDetectionAsync();
                if (vadStarted)
                {
                    // Mikrofon kontrolünü etkinleştir
                    _microphoneAdjuster.Enable();
                    LogService.LogDebug("[DictationManager] VAD ve mikrofon kontrolü başlatıldı");
                }
                else
                {
                    LogService.LogDebug("[DictationManager] VAD başlatılamadı, mikrofon kontrolü devre dışı");
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[DictationManager] Mikrofon kontrol başlatma hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// VAD tabanlı mikrofon kontrolünü durdur
        /// </summary>
        private void StopMicrophoneControl()
        {
            try
            {
                _microphoneAdjuster?.Disable();
                _vad?.StopDetection();
                LogService.LogDebug("[DictationManager] VAD ve mikrofon kontrolü durduruldu");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[DictationManager] Mikrofon kontrol durdurma hatası: {ex.Message}");
            }
        }
        
        // Web Speech API yardımcı metodları
        private void OnWebSpeechStateChanged(object sender, bool isActive)
        {
            LogService.LogDebug($"[DictationManager] Web Speech durumu değişti: {isActive}");
            if (!isActive && _dictationActive)
            {
                // Web Speech durdu ama dikte aktif, yeniden başlat
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    if (_dictationActive)
                    {
                        await StartAsync();
                    }
                });
            }
        }
        
        public void SetDictationEngine(string engine)
        {
            // Artık kullanılmıyor - sadece Web Speech API var
            LogService.LogDebug($"[DictationManager] SetDictationEngine çağrıldı ama sadece Web Speech API destekleniyor");
        }

        // IsWakeWordOnly() ve IsValidCommand() metodları kaldırıldı
        // Wake word ve sleep command kontrolü artık JavaScript (index.html) tarafında yapılıyor
        // JavaScript bu durumları C#'a bildirim olarak gönderiyor (wakeWordDetected, sleepCommandDetected)

        /// <summary>
        /// Levenshtein mesafesi hesaplama
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[s1.Length, s2.Length];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _debounceTimer?.Stop();
                    _debounceTimer?.Dispose();
                    
                    // VAD ve mikrofon kontrolünü temizle
                    if (_microphoneAdjuster != null)
                    {
                        _microphoneAdjuster.MicrophoneAdjusted -= OnMicrophoneAdjusted;
                        _microphoneAdjuster?.Dispose();
                        _microphoneAdjuster = null;
                    }
                    
                    // VAD Singleton'ı dispose etme, sadece referansı temizle
                    _vad = null;
                    
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"[DictationManager] Dispose error: {ex.Message}");
                }
            }
        }
    }
}