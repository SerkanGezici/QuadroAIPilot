using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Voice Activity Detection (VAD) - Gerçek zamanlı konuşma algılama
    /// ChatGPT benzeri voice conversation için kullanıcı konuşmasını detect eder
    /// Singleton pattern - sadece bir instance olacak (mikrofon çakışmasını önler)
    /// </summary>
    public class VoiceActivityDetector : IDisposable
    {
        #region Singleton Pattern
        
        private static VoiceActivityDetector _instance;
        private static readonly object _singletonLock = new object();
        
        /// <summary>
        /// Singleton instance'ını al
        /// </summary>
        public static VoiceActivityDetector Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VoiceActivityDetector();
                        }
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Private Fields
        
        private WaveInEvent _waveIn;
        private volatile bool _isListening = false;
        private volatile bool _isVoiceDetected = false;
        private volatile bool _isPaused = false; // VAD pause state
        private readonly object _lockObject = new object();
        private CancellationTokenSource _detectionCts;
        
        // VAD parametreleri
        private const int SAMPLE_RATE = 16000; // 16kHz optimal for voice detection
        private const int BUFFER_MILLISECONDS = 20; // 20ms buffer
        private const float SILENCE_THRESHOLD = 0.03f; // Sessizlik eşiği (daha da artırıldı)
        private const float VOICE_THRESHOLD = 0.10f; // Konuşma eşiği (uzak sesler için artırıldı)
        private const int MIN_VOICE_DURATION_MS = 400; // Minimum konuşma süresi (uzak sesler için artırıldı)
        private const int SILENCE_TIMEOUT_MS = 500; // Sessizlik timeout'u
        
        // Audio analysis
        private readonly Queue<float> _recentSamples = new Queue<float>();
        private const int ANALYSIS_WINDOW_SIZE = 10; // 10 frame analiz penceresi
        private DateTime _voiceStartTime = DateTime.MinValue;
        private DateTime _lastVoiceTime = DateTime.MinValue;
        
        // Adaptive threshold
        private float _adaptiveThreshold = VOICE_THRESHOLD;
        private float _backgroundNoise = 0.005f;
        private readonly Queue<float> _noiseHistory = new Queue<float>();
        private const int NOISE_HISTORY_SIZE = 50;
        
        // Ambient noise rejection
        private float _lastRMS = 0.0f;
        private const float SPIKE_THRESHOLD = 2.5f; // RMS değeri öncekinin 2.5 katından fazla olmalı (uzak sesler için artırıldı)
        private const int CONSECUTIVE_FRAMES_NEEDED = 5; // Ardışık 5 frame yüksek ses olmalı (uzak sesler için artırıldı)
        private int _consecutiveHighFrames = 0;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Konuşma başladığında tetiklenir
        /// </summary>
        public event EventHandler VoiceActivityStarted;
        
        /// <summary>
        /// Konuşma bittiğinde tetiklenir
        /// </summary>
        public event EventHandler VoiceActivityStopped;
        
        /// <summary>
        /// Ses seviyesi değiştiğinde tetiklenir (real-time monitoring için)
        /// </summary>
        public event EventHandler<VoiceActivityEventArgs> VoiceActivityChanged;
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// VAD aktif mi?
        /// </summary>
        public bool IsListening
        {
            get
            {
                lock (_lockObject)
                {
                    return _isListening;
                }
            }
        }
        
        /// <summary>
        /// Şu anda konuşma algılanıyor mu?
        /// </summary>
        public bool IsVoiceDetected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isVoiceDetected;
                }
            }
        }
        
        /// <summary>
        /// Mevcut adaptif eşik değeri
        /// </summary>
        public float CurrentThreshold => _adaptiveThreshold;
        
        /// <summary>
        /// Arka plan gürültü seviyesi
        /// </summary>
        public float BackgroundNoise => _backgroundNoise;
        
        #endregion
        
        #region Constructor
        
        private VoiceActivityDetector()
        {
            InitializeAudioCapture();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Voice Activity Detection'ı başlat
        /// </summary>
        /// <returns>Başlatma başarılı mı?</returns>
        public async Task<bool> StartDetectionAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isListening)
                    {
                        LogService.LogDebug("[VAD] Zaten dinleme aktif");
                        return true;
                    }
                }
                
                LogService.LogDebug("[VAD] Voice Activity Detection başlatılıyor...");
                
                // Mikrofon cihazını kontrol et
                if (!await CheckMicrophoneAvailability())
                {
                    LogService.LogDebug("[VAD] Mikrofon erişilemiyor");
                    return false;
                }
                
                // Audio capture'ı başlat
                _detectionCts = new CancellationTokenSource();
                
                lock (_lockObject)
                {
                    _isListening = true;
                    _isVoiceDetected = false;
                }
                
                _waveIn?.StartRecording();
                
                LogService.LogDebug("[VAD] Voice Activity Detection başlatıldı");
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[VAD] Başlatma hatası: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Voice Activity Detection'ı durdur
        /// </summary>
        public void StopDetection()
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_isListening)
                    {
                        return;
                    }
                    
                    _isListening = false;
                    _isVoiceDetected = false;
                }
                
                _waveIn?.StopRecording();
                _detectionCts?.Cancel();
                
                // State'i temizle
                _recentSamples.Clear();
                _noiseHistory.Clear();
                
                LogService.LogDebug("[VAD] Voice Activity Detection durduruldu");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[VAD] Durdurma hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// VAD parametrelerini ayarla
        /// </summary>
        /// <param name="voiceThreshold">Konuşma eşiği (0.0-1.0)</param>
        /// <param name="silenceThreshold">Sessizlik eşiği (0.0-1.0)</param>
        public void ConfigureThresholds(float voiceThreshold, float silenceThreshold)
        {
            _adaptiveThreshold = Math.Max(0.001f, Math.Min(1.0f, voiceThreshold));
            
            LogService.LogDebug($"[VAD] Threshold güncellendi: Voice={_adaptiveThreshold:F3}, Silence={silenceThreshold:F3}");
        }
        
        /// <summary>
        /// VAD istatistiklerini al
        /// </summary>
        /// <returns>VAD durumu ve istatistikleri</returns>
        public VoiceActivityStats GetStats()
        {
            lock (_lockObject)
            {
                return new VoiceActivityStats
                {
                    IsListening = _isListening,
                    IsVoiceDetected = _isVoiceDetected,
                    CurrentThreshold = _adaptiveThreshold,
                    BackgroundNoise = _backgroundNoise,
                    LastVoiceTime = _lastVoiceTime,
                    VoiceStartTime = _voiceStartTime
                };
            }
        }
        
        /// <summary>
        /// VAD'i geçici olarak duraklat (TTS sırasında kullanılır)
        /// </summary>
        public void Pause()
        {
            lock (_lockObject)
            {
                if (_isListening && !_isPaused)
                {
                    _isPaused = true;
                    LogService.LogDebug("[VAD] Voice Activity Detection paused");
                }
            }
        }
        
        /// <summary>
        /// VAD'i devam ettir
        /// </summary>
        public void Resume()
        {
            lock (_lockObject)
            {
                if (_isListening && _isPaused)
                {
                    _isPaused = false;
                    // Voice state'i sıfırla
                    _isVoiceDetected = false;
                    _voiceStartTime = DateTime.MinValue;
                    _lastVoiceTime = DateTime.MinValue;
                    _consecutiveHighFrames = 0; // Reset counter
                    _lastRMS = 0.0f; // Reset last RMS
                    LogService.LogDebug("[VAD] Voice Activity Detection resumed");
                }
            }
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Audio capture'ı initialize et
        /// </summary>
        private void InitializeAudioCapture()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SAMPLE_RATE, 1), // 16kHz mono
                    BufferMilliseconds = BUFFER_MILLISECONDS
                };
                
                _waveIn.DataAvailable += OnAudioDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                LogService.LogDebug($"[VAD] Audio capture initialized: {SAMPLE_RATE}Hz, {BUFFER_MILLISECONDS}ms buffer");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[VAD] Audio capture init hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mikrofon erişilebilirliğini kontrol et
        /// </summary>
        private async Task<bool> CheckMicrophoneAvailability()
        {
            try
            {
                await Task.Delay(1); // Async method için
                
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                    return defaultDevice?.State == DeviceState.Active;
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[VAD] Mikrofon kontrolü hatası: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Audio data geldiğinde çağrılır
        /// </summary>
        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_isListening || _detectionCts?.Token.IsCancellationRequested == true)
                return;
            
            // Paused durumunda audio işleme yapma
            if (_isPaused)
                return;
            
            try
            {
                // Audio data'yı float'a çevir ve analiz et
                var samples = ConvertToFloatSamples(e.Buffer, e.BytesRecorded);
                AnalyzeAudioSamples(samples);
            }
            catch (Exception ex)
            {
                LogService.LogVerbose($"[VAD] Audio analiz hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Recording durduğunda çağrılır
        /// </summary>
        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                LogService.LogDebug($"[VAD] Recording durduruldu (hata): {e.Exception.Message}");
            }
        }
        
        /// <summary>
        /// Byte array'i float samples'a çevir
        /// </summary>
        private float[] ConvertToFloatSamples(byte[] buffer, int bytesRecorded)
        {
            var sampleCount = bytesRecorded / 2; // 16-bit samples
            var samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample / 32768.0f; // Normalize to -1.0 to 1.0
            }
            
            return samples;
        }
        
        /// <summary>
        /// Audio samples'ları analiz et ve VAD kararı ver
        /// </summary>
        private void AnalyzeAudioSamples(float[] samples)
        {
            // RMS (Root Mean Square) hesapla
            float rms = CalculateRMS(samples);
            
            // Adaptive threshold ve background noise güncelle
            UpdateAdaptiveThreshold(rms);
            
            // Recent samples'a ekle
            _recentSamples.Enqueue(rms);
            if (_recentSamples.Count > ANALYSIS_WINDOW_SIZE)
            {
                _recentSamples.Dequeue();
            }
            
            // VAD kararını ver
            bool voiceDetected = DetermineVoiceActivity(rms);
            
            // State değişikliğini işle
            ProcessVoiceStateChange(voiceDetected, rms);
            
            // Real-time event gönder
            VoiceActivityChanged?.Invoke(this, new VoiceActivityEventArgs
            {
                RMSLevel = rms,
                Threshold = _adaptiveThreshold,
                IsVoiceDetected = voiceDetected,
                BackgroundNoise = _backgroundNoise
            });
            
            // Son RMS değerini sakla (spike detection için)
            _lastRMS = rms;
        }
        
        /// <summary>
        /// RMS (Root Mean Square) hesapla
        /// </summary>
        private float CalculateRMS(float[] samples)
        {
            float sum = 0;
            foreach (float sample in samples)
            {
                sum += sample * sample;
            }
            return (float)Math.Sqrt(sum / samples.Length);
        }
        
        /// <summary>
        /// Adaptive threshold ve background noise güncelle
        /// </summary>
        private void UpdateAdaptiveThreshold(float currentRMS)
        {
            // Background noise tracking
            if (!_isVoiceDetected)
            {
                _noiseHistory.Enqueue(currentRMS);
                if (_noiseHistory.Count > NOISE_HISTORY_SIZE)
                {
                    _noiseHistory.Dequeue();
                }
                
                // Background noise'u güncelle (median filtresi)
                if (_noiseHistory.Count >= 10)
                {
                    var sortedNoise = _noiseHistory.OrderBy(x => x).ToArray();
                    _backgroundNoise = sortedNoise[sortedNoise.Length / 2];
                    
                    // Adaptive threshold'u background noise'a göre ayarla
                    _adaptiveThreshold = Math.Max(VOICE_THRESHOLD, _backgroundNoise * 6.0f); // Uzak sesler için daha yüksek çarpan
                }
            }
        }
        
        /// <summary>
        /// Voice activity kararını ver
        /// </summary>
        private bool DetermineVoiceActivity(float currentRMS)
        {
            // Basit threshold kontrolü
            if (currentRMS < SILENCE_THRESHOLD)
            {
                _consecutiveHighFrames = 0; // Reset consecutive counter
                return false;
            }
            
            // Ambient noise rejection - Ani spike kontrolü
            bool isSpike = false;
            if (_lastRMS > 0 && currentRMS > _lastRMS * SPIKE_THRESHOLD)
            {
                LogService.LogVerbose($"[VAD] Spike detected: {currentRMS:F3} > {_lastRMS:F3} * {SPIKE_THRESHOLD}");
                isSpike = true;
            }
            
            // Eğer spike ise veya threshold'ı geçiyorsa
            if (isSpike || currentRMS > _adaptiveThreshold)
            {
                _consecutiveHighFrames++;
                
                // Yeterli sayıda ardışık frame yüksek sese sahipse
                if (_consecutiveHighFrames >= CONSECUTIVE_FRAMES_NEEDED)
                {
                    return true;
                }
                return false; // Henüz yeterli ardışık frame yok
            }
            
            // Recent samples ortalamasını kontrol et (smoothing)
            if (_recentSamples.Count >= 3)
            {
                float avgRMS = _recentSamples.Average();
                if (avgRMS > _adaptiveThreshold * 0.85f)
                {
                    _consecutiveHighFrames++;
                    return _consecutiveHighFrames >= CONSECUTIVE_FRAMES_NEEDED;
                }
            }
            
            _consecutiveHighFrames = 0; // Reset counter
            return false;
        }
        
        /// <summary>
        /// Voice state değişikliğini işle
        /// </summary>
        private void ProcessVoiceStateChange(bool voiceDetected, float currentRMS)
        {
            DateTime now = DateTime.UtcNow;
            
            if (voiceDetected && !_isVoiceDetected)
            {
                // Voice activity başladı
                _voiceStartTime = now;
                _lastVoiceTime = now;
                
                lock (_lockObject)
                {
                    _isVoiceDetected = true;
                }
                
                LogService.LogDebug($"[VAD] Konuşma başladı (RMS: {currentRMS:F3}, Threshold: {_adaptiveThreshold:F3})");
                VoiceActivityStarted?.Invoke(this, EventArgs.Empty);
            }
            else if (voiceDetected && _isVoiceDetected)
            {
                // Voice activity devam ediyor
                _lastVoiceTime = now;
            }
            else if (!voiceDetected && _isVoiceDetected)
            {
                // Potential voice activity sonu - timeout kontrolü
                var silenceDuration = now - _lastVoiceTime;
                var voiceDuration = _lastVoiceTime - _voiceStartTime;
                
                if (silenceDuration.TotalMilliseconds > SILENCE_TIMEOUT_MS &&
                    voiceDuration.TotalMilliseconds > MIN_VOICE_DURATION_MS)
                {
                    // Voice activity bitti
                    lock (_lockObject)
                    {
                        _isVoiceDetected = false;
                    }
                    
                    LogService.LogDebug($"[VAD] Konuşma bitti (Süre: {voiceDuration.TotalMilliseconds:F0}ms)");
                    VoiceActivityStopped?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            StopDetection();
            _detectionCts?.Dispose();
            _waveIn?.Dispose();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Voice Activity event argümanları
    /// </summary>
    public class VoiceActivityEventArgs : EventArgs
    {
        public float RMSLevel { get; set; }
        public float Threshold { get; set; }
        public bool IsVoiceDetected { get; set; }
        public float BackgroundNoise { get; set; }
    }
    
    /// <summary>
    /// VAD istatistikleri
    /// </summary>
    public class VoiceActivityStats
    {
        public bool IsListening { get; set; }
        public bool IsVoiceDetected { get; set; }
        public float CurrentThreshold { get; set; }
        public float BackgroundNoise { get; set; }
        public DateTime LastVoiceTime { get; set; }
        public DateTime VoiceStartTime { get; set; }
    }
}