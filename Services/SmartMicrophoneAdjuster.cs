using System;
using System.Threading;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// VAD tabanlı akıllı mikrofon seviyesi ayarlayıcısı
    /// Arka plan konuşmalarını engellemek için mikrofon hassasiyetini dinamik olarak kontrol eder
    /// </summary>
    public class SmartMicrophoneAdjuster : IDisposable
    {
        #region Private Fields
        
        private readonly VoiceActivityDetector _vad;
        private readonly object _lockObject = new object();
        private Timer _adjustmentTimer;
        
        // Mikrofon seviye ayarları
        private const float NORMAL_MIC_LEVEL = 100f;      // Normal mikrofon seviyesi
        private const float CLOSE_MIC_LEVEL = 100f;       // Yakın mesafe (0-1m)
        private const float MEDIUM_MIC_LEVEL = 70f;       // Orta mesafe (1-2m)
        private const float FAR_MIC_LEVEL = 40f;          // Uzak mesafe (2-3m)
        private const float BACKGROUND_MIC_LEVEL = 20f;   // Arka plan (3m+)
        
        // Mesafe tahmini parametreleri
        private const float CLOSE_DISTANCE_THRESHOLD = 1.0f;
        private const float MEDIUM_DISTANCE_THRESHOLD = 2.0f;
        private const float FAR_DISTANCE_THRESHOLD = 3.0f;
        
        // Smooth transition parametreleri
        private const int ADJUSTMENT_INTERVAL_MS = 200;   // Ayarlama sıklığı
        private const float TRANSITION_SMOOTHNESS = 0.3f; // Geçiş yumuşaklığı (0-1)
        
        // Durum takibi
        private float _currentMicLevel = NORMAL_MIC_LEVEL;
        private float _targetMicLevel = NORMAL_MIC_LEVEL;
        private DateTime _lastVoiceTime = DateTime.MinValue;
        private bool _isUserSpeaking = false;
        private bool _isEnabled = false;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Mikrofon seviyesi değiştiğinde tetiklenir
        /// </summary>
        public event EventHandler<MicrophoneAdjustmentEventArgs> MicrophoneAdjusted;
        
        #endregion
        
        #region Constructor
        
        public SmartMicrophoneAdjuster(VoiceActivityDetector vad)
        {
            _vad = vad ?? throw new ArgumentNullException(nameof(vad));
            
            // VAD eventlerini dinle
            _vad.VoiceActivityStarted += OnVoiceActivityStarted;
            _vad.VoiceActivityStopped += OnVoiceActivityStopped;
            _vad.VoiceActivityChanged += OnVoiceActivityChanged;
            
            // Smooth transition timer
            _adjustmentTimer = new Timer(OnAdjustmentTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            
            LogService.LogDebug("[SmartMicrophoneAdjuster] Initialized");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Akıllı mikrofon ayarlamayı başlat
        /// </summary>
        public void Enable()
        {
            lock (_lockObject)
            {
                if (_isEnabled) return;
                
                _isEnabled = true;
                _currentMicLevel = NORMAL_MIC_LEVEL;
                _targetMicLevel = NORMAL_MIC_LEVEL;
                
                // Timer'ı başlat
                _adjustmentTimer.Change(ADJUSTMENT_INTERVAL_MS, ADJUSTMENT_INTERVAL_MS);
                
                LogService.LogDebug("[SmartMicrophoneAdjuster] Enabled");
            }
        }
        
        /// <summary>
        /// Akıllı mikrofon ayarlamayı durdur
        /// </summary>
        public void Disable()
        {
            lock (_lockObject)
            {
                if (!_isEnabled) return;
                
                _isEnabled = false;
                
                // Timer'ı durdur
                _adjustmentTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                // Mikrofonu normale döndür
                SetMicrophoneLevel(NORMAL_MIC_LEVEL);
                
                LogService.LogDebug("[SmartMicrophoneAdjuster] Disabled");
            }
        }
        
        /// <summary>
        /// Mevcut mikrofon seviyesini al
        /// </summary>
        public float GetCurrentMicrophoneLevel()
        {
            lock (_lockObject)
            {
                return _currentMicLevel;
            }
        }
        
        /// <summary>
        /// Aktif durumu kontrol et
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                lock (_lockObject)
                {
                    return _isEnabled;
                }
            }
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// VAD konuşma başladığında
        /// </summary>
        private void OnVoiceActivityStarted(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                if (!_isEnabled) return;
                
                _isUserSpeaking = true;
                _lastVoiceTime = DateTime.UtcNow;
                
                // Kullanıcı konuşuyor - mikrofonu normale döndür
                _targetMicLevel = NORMAL_MIC_LEVEL;
                
                LogService.LogDebug("[SmartMicrophoneAdjuster] Kullanıcı konuşmaya başladı - Target: 100%");
            }
        }
        
        /// <summary>
        /// VAD konuşma bittiğinde
        /// </summary>
        private void OnVoiceActivityStopped(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                if (!_isEnabled) return;
                
                _isUserSpeaking = false;
                _lastVoiceTime = DateTime.UtcNow;
                
                LogService.LogDebug("[SmartMicrophoneAdjuster] Kullanıcı konuşmayı bitirdi");
            }
        }
        
        /// <summary>
        /// VAD ses aktivitesi değiştiğinde
        /// </summary>
        private void OnVoiceActivityChanged(object sender, VoiceActivityEventArgs e)
        {
            lock (_lockObject)
            {
                if (!_isEnabled) return;
                
                // Eğer kullanıcı konuşuyorsa mikrofonu normale tut
                if (_isUserSpeaking)
                {
                    _targetMicLevel = NORMAL_MIC_LEVEL;
                    return;
                }
                
                // Arka plan sesi analizi
                float distance = EstimateDistance(e.RMSLevel, e.BackgroundNoise);
                float newTargetLevel = CalculateMicrophoneLevel(distance, e.RMSLevel, e.BackgroundNoise);
                
                // Hedef seviyeyi güncelle
                if (Math.Abs(newTargetLevel - _targetMicLevel) > 5f) // %5'ten fazla değişiklik varsa
                {
                    _targetMicLevel = newTargetLevel;
                    
                    LogService.LogVerbose($"[SmartMicrophoneAdjuster] Distance: {distance:F1}m, RMS: {e.RMSLevel:F3}, Target: {_targetMicLevel:F0}%");
                }
            }
        }
        
        /// <summary>
        /// Gelişmiş mesafe tahmini algoritması
        /// </summary>
        private float EstimateDistance(float rms, float backgroundNoise)
        {
            if (backgroundNoise <= 0) backgroundNoise = 0.001f; // Division by zero önleme
            
            // Signal-to-Noise Ratio hesapla
            float snr = rms / backgroundNoise;
            float snrDb = 20f * (float)Math.Log10(snr); // dB cinsinden SNR
            
            // RMS'in mutlak değeri de önemli (ses şiddeti)
            float rmsDb = 20f * (float)Math.Log10(Math.Max(rms, 0.0001f));
            
            // Frekans spektrumu analizi (basitleştirilmiş)
            // İnsan sesi genelde 85-255 Hz (erkek) ve 165-255 Hz (kadın) aralığında
            // Uzak sesler yüksek frekansları kaybeder
            float frequencyFactor = EstimateFrequencyAttenuation(rms, backgroundNoise);
            
            // Mesafe tahmini formülü
            // Ses şiddeti mesafenin karesiyle ters orantılı olarak azalır (inverse square law)
            
            // SNR bazlı mesafe
            float snrDistance;
            if (snrDb > 30) snrDistance = 0.3f;      // Çok yakın (30cm)
            else if (snrDb > 20) snrDistance = 0.7f; // Yakın (70cm)
            else if (snrDb > 15) snrDistance = 1.2f; // Normal (1.2m)
            else if (snrDb > 10) snrDistance = 2.0f; // Orta (2m)
            else if (snrDb > 6) snrDistance = 3.0f;  // Uzak (3m)
            else if (snrDb > 3) snrDistance = 4.0f;  // Çok uzak (4m)
            else snrDistance = 5.0f;                 // Arka plan (5m+)
            
            // RMS bazlı mesafe düzeltmesi
            float rmsCorrection = 1.0f;
            if (rmsDb < -40) rmsCorrection = 1.5f;      // Çok düşük ses
            else if (rmsDb < -30) rmsCorrection = 1.2f; // Düşük ses
            else if (rmsDb > -15) rmsCorrection = 0.8f; // Yüksek ses
            else if (rmsDb > -10) rmsCorrection = 0.6f; // Çok yüksek ses
            
            // Frekans faktörü ile düzeltme
            float finalDistance = snrDistance * rmsCorrection * frequencyFactor;
            
            // Sınırları kontrol et
            finalDistance = Math.Max(0.3f, Math.Min(5.0f, finalDistance));
            
            // Debug log
            LogService.LogVerbose($"[SmartMicrophoneAdjuster] Distance Estimation - SNR: {snrDb:F1}dB, RMS: {rmsDb:F1}dB, Distance: {finalDistance:F1}m");
            
            return finalDistance;
        }
        
        /// <summary>
        /// Frekans zayıflaması tahmini (basitleştirilmiş)
        /// </summary>
        private float EstimateFrequencyAttenuation(float rms, float backgroundNoise)
        {
            // Uzak sesler yüksek frekanslarda daha fazla zayıflar
            // Bu basit bir tahmindir, gerçek spektrum analizi için FFT gerekir
            
            float snr = rms / backgroundNoise;
            
            // SNR düşükse, muhtemelen uzak bir ses ve yüksek frekanslar kayıp
            if (snr < 2) return 1.3f;      // %30 daha uzak tahmin et
            else if (snr < 5) return 1.1f; // %10 daha uzak tahmin et
            else return 1.0f;              // Normal
        }
        
        /// <summary>
        /// Mesafe ve ses seviyesine göre akıllı mikrofon seviyesi hesapla
        /// </summary>
        private float CalculateMicrophoneLevel(float distance, float rms, float backgroundNoise)
        {
            // SNR hesapla
            float snr = rms / backgroundNoise;
            float snrDb = 20f * (float)Math.Log10(Math.Max(snr, 0.001f));
            
            // Temel mesafe bazlı seviye (ters kare kanunu)
            // Mikrofon seviyesi = 100% - (mesafe faktörü * azaltma katsayısı)
            float distanceFactor = Math.Min(distance / 5.0f, 1.0f); // 0-1 arası normalize
            float baseReduction = distanceFactor * 80f; // Maksimum %80 azaltma
            
            // SNR bazlı düzeltme
            float snrBoost = 0f;
            if (snrDb < 3) // Çok düşük SNR
            {
                // Mikrofonu daha da azalt, arka plan gürültüsünü bastır
                snrBoost = -20f;
            }
            else if (snrDb < 6) // Düşük SNR
            {
                snrBoost = -10f;
            }
            else if (snrDb > 20) // Yüksek SNR
            {
                // Muhtemelen yakın konuşma, mikrofonu biraz arttır
                snrBoost = 10f;
            }
            
            // Arka plan gürültü seviyesine göre adaptasyon
            float noiseDb = 20f * (float)Math.Log10(Math.Max(backgroundNoise, 0.0001f));
            float noiseAdjustment = 0f;
            
            if (noiseDb > -20) // Yüksek arka plan gürültüsü
            {
                // Mikrofonu azaltarak gürültüyü filtrele
                noiseAdjustment = -15f;
            }
            else if (noiseDb < -40) // Çok sessiz ortam
            {
                // Mikrofon hassasiyetini arttırabilirsin
                noiseAdjustment = 5f;
            }
            
            // Nihai mikrofon seviyesi hesapla
            float targetLevel = NORMAL_MIC_LEVEL - baseReduction + snrBoost + noiseAdjustment;
            
            // Özel durumlar
            if (distance > 4.0f && snrDb < 6) 
            {
                // Çok uzak ve düşük SNR - agresif filtreleme
                targetLevel = BACKGROUND_MIC_LEVEL;
            }
            else if (distance < 0.5f && snrDb > 15)
            {
                // Çok yakın ve yüksek SNR - normal seviye
                targetLevel = NORMAL_MIC_LEVEL;
            }
            
            // Sınırları kontrol et
            targetLevel = Math.Max(BACKGROUND_MIC_LEVEL, Math.Min(NORMAL_MIC_LEVEL, targetLevel));
            
            // Debug log
            LogService.LogVerbose($"[SmartMicrophoneAdjuster] Mic Level Calc - Distance: {distance:F1}m, SNR: {snrDb:F1}dB, Noise: {noiseDb:F1}dB, Target: {targetLevel:F0}%");
            
            return targetLevel;
        }
        
        /// <summary>
        /// Smooth transition timer tick
        /// </summary>
        private void OnAdjustmentTimerTick(object state)
        {
            lock (_lockObject)
            {
                if (!_isEnabled) return;
                
                // Hedef seviyeye doğru yumuşak geçiş
                float diff = _targetMicLevel - _currentMicLevel;
                
                if (Math.Abs(diff) > 1f) // %1'den fazla fark varsa
                {
                    float adjustment = diff * TRANSITION_SMOOTHNESS;
                    _currentMicLevel += adjustment;
                    
                    // Sınırları kontrol et
                    _currentMicLevel = Math.Max(BACKGROUND_MIC_LEVEL, Math.Min(NORMAL_MIC_LEVEL, _currentMicLevel));
                    
                    // Mikrofon seviyesini ayarla
                    SetMicrophoneLevel(_currentMicLevel);
                    
                    // Event tetikle
                    MicrophoneAdjusted?.Invoke(this, new MicrophoneAdjustmentEventArgs
                    {
                        CurrentLevel = _currentMicLevel,
                        TargetLevel = _targetMicLevel,
                        IsUserSpeaking = _isUserSpeaking
                    });
                }
            }
        }
        
        /// <summary>
        /// Mikrofon seviyesini fiziksel olarak ayarla
        /// </summary>
        private void SetMicrophoneLevel(float level)
        {
            try
            {
                uint volumeLevel = (uint)Math.Round(level);
                AudioDuckingManager.SetMicrophoneVolume(volumeLevel);
                
                LogService.LogVerbose($"[SmartMicrophoneAdjuster] Mikrofon seviyesi ayarlandı: {volumeLevel}%");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[SmartMicrophoneAdjuster] Mikrofon seviye ayarlama hatası: {ex.Message}");
            }
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // VAD eventlerini temizle
                if (_vad != null)
                {
                    _vad.VoiceActivityStarted -= OnVoiceActivityStarted;
                    _vad.VoiceActivityStopped -= OnVoiceActivityStopped;
                    _vad.VoiceActivityChanged -= OnVoiceActivityChanged;
                }
                
                // Timer'ı temizle
                _adjustmentTimer?.Dispose();
                
                // Mikrofonu normale döndür
                try
                {
                    AudioDuckingManager.SetMicrophoneVolume((uint)NORMAL_MIC_LEVEL);
                }
                catch
                {
                    // Hata durumunda sessizce devam et
                }
                
                LogService.LogDebug("[SmartMicrophoneAdjuster] Disposed");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Mikrofon ayarlama event argümanları
    /// </summary>
    public class MicrophoneAdjustmentEventArgs : EventArgs
    {
        public float CurrentLevel { get; set; }
        public float TargetLevel { get; set; }
        public bool IsUserSpeaking { get; set; }
    }
}