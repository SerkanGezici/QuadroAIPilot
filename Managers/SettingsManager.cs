using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace QuadroAIPilot.Managers
{
    public enum AppTheme
    {
        GradientParadise,  // Modern mor-pembe gradient (2025 trendi)
        DarkElegance,      // Koyu ve şık gradient
        WarmSunset,        // Sıcak tonlar gradient
        NordicAurora,      // Minimal koyu gri tonları
        NeonNights         // Canlı neon gradient
    }

    public enum PerformanceProfile
    {
        Auto,        // Otomatik (GPU/Pil durumuna göre)
        High,        // Tüm efektler açık
        Medium,      // Dengeli
        Low,         // Minimum efekt
        PowerSaver   // Güç tasarrufu modu
    }

    public class AppSettings
    {
        public AppTheme Theme { get; set; } = AppTheme.GradientParadise;
        public PerformanceProfile Performance { get; set; } = PerformanceProfile.Auto;
        public bool EnableAnimations { get; set; } = true;
        public double AnimationSpeed { get; set; } = 1.0; // 0.5-2.0 arası
        public bool EnableParallaxEffects { get; set; } = true;
        public bool EnableGlowEffects { get; set; } = true;
        public double BlurIntensity { get; set; } = 20.0; // 0-30 arası
        public string TTSVoice { get; set; } = "automatic"; // TTS ses seçimi
    }

    public class SettingsManager
    {
        private static SettingsManager _instance;
        private AppSettings _settings;
        private readonly string _settingsPath;
        private readonly object _lockObject = new object();

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                }
                return _instance;
            }
        }

        public AppSettings Settings
        {
            get
            {
                lock (_lockObject)
                {
                    return _settings;
                }
            }
        }

        public event EventHandler<AppSettings> SettingsChanged;

        private SettingsManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "QuadroAIPilot");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _settingsPath = Path.Combine(appFolder, "settings.json");
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings yüklenemedi: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() => SaveSettings());
        }

        private void SaveSettings()
        {
            try
            {
                lock (_lockObject)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(_settings, options);
                    File.WriteAllText(_settingsPath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings kaydedilemedi: {ex.Message}");
            }
        }

        public async Task UpdateThemeAsync(AppTheme theme)
        {
            lock (_lockObject)
            {
                _settings.Theme = theme;
            }
            
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(this, _settings);
        }

        public async Task UpdatePerformanceProfileAsync(PerformanceProfile profile)
        {
            lock (_lockObject)
            {
                _settings.Performance = profile;
                
                // Performans profiline göre otomatik ayarlamalar
                switch (profile)
                {
                    case PerformanceProfile.High:
                        _settings.EnableAnimations = true;
                        _settings.EnableParallaxEffects = true;
                        _settings.EnableGlowEffects = true;
                        _settings.AnimationSpeed = 1.0;
                        break;
                        
                    case PerformanceProfile.Low:
                    case PerformanceProfile.PowerSaver:
                        _settings.EnableAnimations = false;
                        _settings.EnableParallaxEffects = false;
                        _settings.EnableGlowEffects = false;
                        _settings.AnimationSpeed = 0.5;
                        break;
                        
                    case PerformanceProfile.Medium:
                        _settings.EnableAnimations = true;
                        _settings.EnableParallaxEffects = false;
                        _settings.EnableGlowEffects = true;
                        _settings.AnimationSpeed = 0.8;
                        break;
                }
            }
            
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(this, _settings);
        }

        public async Task UpdateSettingAsync<T>(string propertyName, T value)
        {
            lock (_lockObject)
            {
                var property = typeof(AppSettings).GetProperty(propertyName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_settings, value);
                }
            }
            
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(this, _settings);
        }

        public async Task UpdateSettingsAsync(AppSettings newSettings)
        {
            lock (_lockObject)
            {
                _settings = newSettings;
            }
            
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(this, _settings);
        }

        public ElementTheme GetElementTheme()
        {
            return _settings.Theme switch
            {
                AppTheme.GradientParadise => ElementTheme.Light,
                AppTheme.DarkElegance => ElementTheme.Dark,
                AppTheme.WarmSunset => ElementTheme.Light,
                AppTheme.NordicAurora => ElementTheme.Dark,
                AppTheme.NeonNights => ElementTheme.Dark,
                _ => ElementTheme.Light
            };
        }

        public bool ShouldUseTransparency()
        {
            // Sistem ayarlarını kontrol et
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var systemTransparency = uiSettings.AdvancedEffectsEnabled;

            // Yeni tema sisteminde şeffaflık kullanmıyoruz
            // Sadece widget'lar için hafif blur efekti var
            if (!systemTransparency)
            {
                return false;
            }

            // Diğer durumlarda performans profiline bak
            return _settings.Performance != PerformanceProfile.Low && 
                   _settings.Performance != PerformanceProfile.PowerSaver;
        }

        public async Task<PerformanceProfile> DetectOptimalPerformanceAsync()
        {
            try
            {
                // PerformanceMonitor kullanarak öneri al
                var perfMonitor = Services.PerformanceMonitor.Instance;
                return await perfMonitor.GetRecommendedPerformanceProfileAsync();
            }
            catch
            {
                // Hata durumunda basit kontrol
                var powerStatus = Windows.System.Power.PowerManager.BatteryStatus;
                var batteryLevel = Windows.System.Power.PowerManager.RemainingChargePercent;

                if (powerStatus == Windows.System.Power.BatteryStatus.Discharging && batteryLevel < 20)
                {
                    return PerformanceProfile.PowerSaver;
                }

                return PerformanceProfile.Medium;
            }
        }

        public void ResetToDefaults()
        {
            lock (_lockObject)
            {
                _settings = new AppSettings();
            }
            
            SaveSettings();
            SettingsChanged?.Invoke(this, _settings);
        }
    }
}