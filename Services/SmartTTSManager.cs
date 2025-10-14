using System;
using System.Threading;
using System.Threading.Tasks;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Smart TTS Manager - VAD ile ChunkedTTSPlayer'ı entegre eder
    /// ChatGPT benzeri interrupt capability sağlar
    /// </summary>
    public class SmartTTSManager : IDisposable
    {
        #region Private Fields
        
        private readonly ChunkedTTSPlayer _chunkedPlayer;
        private readonly VoiceActivityDetector _voiceDetector;
        private readonly IWebViewManager _webViewManager;
        private readonly object _lockObject = new object();
        private readonly bool _ownsVAD; // VAD'i biz oluşturduk mu?
        
        private volatile bool _isPlaying = false;
        private volatile bool _isInterrupted = false;
        private CancellationTokenSource _playbackCts;
        private string _currentText = string.Empty;
        
        // Configuration
        private const int INTERRUPT_REACTION_TIME_MS = 100; // Interrupt reaction time
        private const float VAD_VOICE_THRESHOLD = 0.10f; // Voice detection threshold (uzak sesler için daha da artırıldı)
        private const float VAD_SILENCE_THRESHOLD = 0.03f; // Silence threshold (uzak sesler için daha da artırıldı)
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Smart TTS başladığında tetiklenir
        /// </summary>
        public event EventHandler TTSStarted;
        
        /// <summary>
        /// Smart TTS tamamlandığında tetiklenir
        /// </summary>
        public event EventHandler TTSCompleted;
        
        /// <summary>
        /// Smart TTS kullanıcı tarafından kesildiğinde tetiklenir
        /// </summary>
        public event EventHandler TTSInterrupted;
        
        /// <summary>
        /// Voice activity algılandığında tetiklenir
        /// </summary>
        public event EventHandler<VoiceInterruptEventArgs> VoiceInterruptDetected;
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// Smart TTS şu anda oynatılıyor mu?
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                lock (_lockObject)
                {
                    return _isPlaying;
                }
            }
        }
        
        /// <summary>
        /// Son oynatma kesintiye uğradı mı?
        /// </summary>
        public bool WasInterrupted
        {
            get
            {
                lock (_lockObject)
                {
                    return _isInterrupted;
                }
            }
        }
        
        /// <summary>
        /// VAD aktif mi?
        /// </summary>
        public bool IsVADActive => _voiceDetector?.IsListening ?? false;
        
        /// <summary>
        /// Şu anda konuşma algılanıyor mu?
        /// </summary>
        public bool IsVoiceDetected => _voiceDetector?.IsVoiceDetected ?? false;
        
        /// <summary>
        /// Mevcut oynatılan metin
        /// </summary>
        public string CurrentText
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentText;
                }
            }
        }
        
        #endregion
        
        #region Constructor
        
        public SmartTTSManager(IWebViewManager webViewManager, VoiceActivityDetector externalVAD = null)
        {
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            
            // ChunkedTTSPlayer'ı initialize et
            _chunkedPlayer = new ChunkedTTSPlayer(_webViewManager);
            _chunkedPlayer.PlaybackStarted += OnChunkedPlaybackStarted;
            _chunkedPlayer.PlaybackCompleted += OnChunkedPlaybackCompleted;
            _chunkedPlayer.PlaybackInterrupted += OnChunkedPlaybackInterrupted;
            
            // VoiceActivityDetector'ı initialize et (external veya yeni)
            if (externalVAD != null)
            {
                _voiceDetector = externalVAD;
                _ownsVAD = false; // External VAD, biz dispose etmeyeceğiz
                LogService.LogDebug("[SmartTTSManager] External VAD kullanılıyor");
            }
            else
            {
                _voiceDetector = VoiceActivityDetector.Instance;
                _ownsVAD = false; // Singleton VAD, dispose etmeyeceğiz
                LogService.LogDebug("[SmartTTSManager] Singleton VAD kullanılıyor");
            }
            
            _voiceDetector.VoiceActivityStarted += OnVoiceActivityStarted;
            _voiceDetector.VoiceActivityStopped += OnVoiceActivityStopped;
            
            // VAD threshold'larını ayarla
            _voiceDetector.ConfigureThresholds(VAD_VOICE_THRESHOLD, VAD_SILENCE_THRESHOLD);
            
            LogService.LogDebug("[SmartTTSManager] Smart TTS Manager initialized");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Smart TTS sistemi başlat (VAD enabled)
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                LogService.LogDebug("[SmartTTSManager] Smart TTS sistemi başlatılıyor...");
                
                // VAD'i başlat
                bool vadStarted = await _voiceDetector.StartDetectionAsync();
                if (!vadStarted)
                {
                    LogService.LogDebug("[SmartTTSManager] VAD başlatılamadı");
                    return false;
                }
                
                LogService.LogDebug("[SmartTTSManager] Smart TTS sistemi başarıyla başlatıldı");
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[SmartTTSManager] Initialize hatası: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Metni smart TTS ile oynat (interrupt capability ile)
        /// </summary>
        /// <param name="audioData">Oynatılacak audio data</param>
        /// <param name="text">Oynatılan metin (logging için)</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Oynatma tamamlandı mı (true) yoksa kesildi mi (false)?</returns>
        public async Task<bool> PlayWithInterruptAsync(byte[] audioData, string text = "", CancellationToken cancellationToken = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                LogService.LogDebug("[SmartTTSManager] Boş audio data");
                return false;
            }
            
            lock (_lockObject)
            {
                if (_isPlaying)
                {
                    LogService.LogDebug("[SmartTTSManager] Zaten oynatılıyor, yeni istek iptal edildi");
                    return false;
                }
                
                _isPlaying = true;
                _isInterrupted = false;
                _currentText = text;
            }
            
            try
            {
                LogService.LogDebug($"[SmartTTSManager] Smart TTS başlatılıyor: '{text}' ({audioData.Length} bytes)");
                
                // Playback token oluştur
                _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                // TTS started event
                TTSStarted?.Invoke(this, EventArgs.Empty);
                
                // Chunked playback başlat
                bool completed = await _chunkedPlayer.PlayChunkedAudioAsync(audioData, _playbackCts.Token);
                
                if (completed && !_isInterrupted)
                {
                    LogService.LogDebug($"[SmartTTSManager] Smart TTS tamamlandı: '{text}'");
                    TTSCompleted?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                else
                {
                    LogService.LogDebug($"[SmartTTSManager] Smart TTS kesildi: '{text}'");
                    TTSInterrupted?.Invoke(this, EventArgs.Empty);
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                LogService.LogDebug($"[SmartTTSManager] Smart TTS iptal edildi: '{text}'");
                TTSInterrupted?.Invoke(this, EventArgs.Empty);
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[SmartTTSManager] Smart TTS hatası: {ex.Message}");
                return false;
            }
            finally
            {
                lock (_lockObject)
                {
                    _isPlaying = false;
                    _currentText = string.Empty;
                }
                
                _playbackCts?.Dispose();
                _playbackCts = null;
            }
        }
        
        /// <summary>
        /// Şu anda oynatılan TTS'i anında kes
        /// </summary>
        public void InterruptCurrentPlayback()
        {
            lock (_lockObject)
            {
                if (!_isPlaying)
                {
                    LogService.LogDebug("[SmartTTSManager] Zaten oynatılmıyor, interrupt gerekmez");
                    return;
                }
                
                _isInterrupted = true;
                LogService.LogDebug($"[SmartTTSManager] Manuel interrupt: '{_currentText}'");
            }
            
            // Playback'i iptal et
            _playbackCts?.Cancel();
            _chunkedPlayer?.InterruptPlayback();
        }
        
        /// <summary>
        /// VAD hassasiyetini ayarla
        /// </summary>
        /// <param name="voiceThreshold">Voice detection threshold (0.0-1.0)</param>
        /// <param name="silenceThreshold">Silence threshold (0.0-1.0)</param>
        public void ConfigureVAD(float voiceThreshold, float silenceThreshold)
        {
            _voiceDetector?.ConfigureThresholds(voiceThreshold, silenceThreshold);
            LogService.LogDebug($"[SmartTTSManager] VAD configured: Voice={voiceThreshold:F3}, Silence={silenceThreshold:F3}");
        }
        
        /// <summary>
        /// Smart TTS istatistiklerini al
        /// </summary>
        public SmartTTSStats GetStats()
        {
            var vadStats = _voiceDetector?.GetStats();
            
            lock (_lockObject)
            {
                return new SmartTTSStats
                {
                    IsPlaying = _isPlaying,
                    WasInterrupted = _isInterrupted,
                    CurrentText = _currentText,
                    VADStats = vadStats,
                    ChunkProgress = _chunkedPlayer?.GetPlaybackProgress() ?? 0.0f
                };
            }
        }
        
        /// <summary>
        /// Smart TTS sistemi kapat
        /// </summary>
        public void Shutdown()
        {
            try
            {
                LogService.LogDebug("[SmartTTSManager] Smart TTS sistemi kapatılıyor...");
                
                // Aktif oynatmayı kes
                InterruptCurrentPlayback();
                
                // VAD'i durdur
                _voiceDetector?.StopDetection();
                
                LogService.LogDebug("[SmartTTSManager] Smart TTS sistemi kapatıldı");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[SmartTTSManager] Shutdown hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// VAD'i geçici olarak duraklat (External TTS oynatırken kullanılır)
        /// </summary>
        public void PauseVAD()
        {
            _voiceDetector?.Pause();
            LogService.LogDebug("[SmartTTSManager] VAD paused for external TTS");
        }
        
        /// <summary>
        /// VAD'i devam ettir
        /// </summary>
        public void ResumeVAD()
        {
            _voiceDetector?.Resume();
            LogService.LogDebug("[SmartTTSManager] VAD resumed after external TTS");
        }
        
        #endregion
        
        #region Private Event Handlers
        
        /// <summary>
        /// ChunkedTTSPlayer oynatma başladığında
        /// </summary>
        private void OnChunkedPlaybackStarted(object sender, EventArgs e)
        {
            LogService.LogVerbose("[SmartTTSManager] Chunked playback başladı");
        }
        
        /// <summary>
        /// ChunkedTTSPlayer oynatma tamamlandığında
        /// </summary>
        private void OnChunkedPlaybackCompleted(object sender, EventArgs e)
        {
            LogService.LogVerbose("[SmartTTSManager] Chunked playback tamamlandı");
        }
        
        /// <summary>
        /// ChunkedTTSPlayer oynatma kesildiğinde
        /// </summary>
        private void OnChunkedPlaybackInterrupted(object sender, EventArgs e)
        {
            LogService.LogVerbose("[SmartTTSManager] Chunked playback kesildi");
        }
        
        /// <summary>
        /// VAD konuşma algıladığında - CRITICAL: TTS'i kes!
        /// </summary>
        private void OnVoiceActivityStarted(object sender, EventArgs e)
        {
            LogService.LogDebug("[SmartTTSManager] VOICE DETECTED - TTS kesiliyor!");
            
            // Voice interrupt event tetikle
            VoiceInterruptDetected?.Invoke(this, new VoiceInterruptEventArgs
            {
                InterruptTime = DateTime.UtcNow,
                CurrentText = _currentText,
                PlaybackProgress = _chunkedPlayer?.GetPlaybackProgress() ?? 0.0f
            });
            
            // TTS'i anında kes
            if (_isPlaying)
            {
                _ = Task.Run(async () =>
                {
                    // Kısa delay (reaction time simulation)
                    await Task.Delay(INTERRUPT_REACTION_TIME_MS);
                    InterruptCurrentPlayback();
                });
            }
        }
        
        /// <summary>
        /// VAD konuşma bittiğinde
        /// </summary>
        private void OnVoiceActivityStopped(object sender, EventArgs e)
        {
            LogService.LogDebug("[SmartTTSManager] Voice activity bitti");
            // TODO: Gelecekte TTS'i kaldığı yerden devam ettirme özelliği
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            Shutdown();
            _playbackCts?.Dispose();
            _chunkedPlayer?.Dispose();
            
            // Sadece kendi VAD'imizsa dispose et
            if (_ownsVAD && _voiceDetector != null)
            {
                _voiceDetector.Dispose();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Voice interrupt event argümanları
    /// </summary>
    public class VoiceInterruptEventArgs : EventArgs
    {
        public DateTime InterruptTime { get; set; }
        public string CurrentText { get; set; }
        public float PlaybackProgress { get; set; }
    }
    
    /// <summary>
    /// Smart TTS istatistikleri
    /// </summary>
    public class SmartTTSStats
    {
        public bool IsPlaying { get; set; }
        public bool WasInterrupted { get; set; }
        public string CurrentText { get; set; }
        public VoiceActivityStats VADStats { get; set; }
        public float ChunkProgress { get; set; }
    }
}