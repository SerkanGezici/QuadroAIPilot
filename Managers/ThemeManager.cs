using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Managers
{
    public class ThemeManager
    {
        private static ThemeManager _instance;
        private readonly SettingsManager _settingsManager;
        private IWebViewManager _webViewManager;
        
        // Tema renkleri
        private readonly Dictionary<AppTheme, ThemeColors> _themeColors;

        public static ThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ThemeManager();
                }
                return _instance;
            }
        }

        private ThemeManager()
        {
            _settingsManager = SettingsManager.Instance;
            
            // _themeColors'u burada başlat
            _themeColors = new Dictionary<AppTheme, ThemeColors>();
            InitializeThemeColors();
            
            // Settings değişikliklerini dinle
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }
        
        public AppTheme CurrentTheme => _settingsManager.Settings.Theme;
        
        public void SetWebViewManager(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager;
        }

        private void InitializeThemeColors()
        {
            _themeColors.Clear();
            
            // Dark Elegance (Koyu & Şık)
            _themeColors.Add(AppTheme.DarkElegance, new ThemeColors
            {
                BackgroundGradient = "linear-gradient(135deg, #0f0f0f 0%, #1a1a2e 25%, #16213e 50%, #0f3460 100%)",
                BackgroundColor = "#0f0f0f",
                BackgroundOpacity = 1.0,
                PrimaryColor = "#e94560",
                SecondaryColor = "rgba(255, 255, 255, 0.05)",
                TextColor = "#ffffff",
                BorderColor = "rgba(255, 255, 255, 0.15)",
                FeedbackColor = "#4ecdc4",
                GlowIntensity = 0.3,
                BlurAmount = 20
            });
            
            // Warm Sunset (Sıcak Tonlar)
            _themeColors.Add(AppTheme.WarmSunset, new ThemeColors
            {
                BackgroundGradient = "linear-gradient(135deg, #ff6b6b 0%, #feca57 25%, #ff9ff3 50%, #ff6b6b 100%)",
                BackgroundColor = "#ff6b6b",
                BackgroundOpacity = 1.0,
                PrimaryColor = "#ee5a6f",
                SecondaryColor = "rgba(255, 255, 255, 0.25)",
                TextColor = "#ffffff",
                BorderColor = "rgba(255, 255, 255, 0.3)",
                GlowIntensity = 0.1,
                BlurAmount = 25
            });
            
            // Nordic Aurora (Minimal & Modern)
            _themeColors.Add(AppTheme.NordicAurora, new ThemeColors
            {
                BackgroundGradient = "linear-gradient(135deg, #2e3440 0%, #3b4252 25%, #434c5e 50%, #4c566a 100%)",
                BackgroundColor = "#2e3440",
                BackgroundOpacity = 1.0,
                PrimaryColor = "#88c0d0",
                SecondaryColor = "rgba(236, 239, 244, 0.05)",
                TextColor = "#eceff4",
                BorderColor = "rgba(236, 239, 244, 0.2)",
                FeedbackColor = "#a3be8c",
                GlowIntensity = 0.15,
                BlurAmount = 15
            });
            
            // Neon Nights (Canlı & Modern)
            _themeColors.Add(AppTheme.NeonNights, new ThemeColors
            {
                BackgroundGradient = "linear-gradient(135deg, #12c2e9 0%, #c471ed 50%, #f64f59 100%)",
                BackgroundColor = "#12c2e9",
                BackgroundOpacity = 1.0,
                PrimaryColor = "#ff006e",
                SecondaryColor = "rgba(0, 0, 0, 0.3)",
                TextColor = "#ffffff",
                BorderColor = "rgba(255, 255, 255, 0.25)",
                AccentColor = "#ffbe0b",
                GlowIntensity = 0.6,
                BlurAmount = 40
            });
        }

        private async void OnSettingsChanged(object sender, AppSettings settings)
        {
            await ApplyThemeAsync(settings.Theme);
        }

        private Window _window;
        
        public async Task InitializeAsync(Window window)
        {
            _window = window;
            
            // Yeni tema sisteminde backdrop kullanmıyoruz
            _window.SystemBackdrop = null;
            
            // İlk tema uygulaması
            var theme = _settingsManager.Settings.Theme;
            await ApplyThemeAsync(theme);
        }

        public async Task ApplyThemeAsync(AppTheme theme)
        {
            try
            {
                var colors = GetThemeColors(theme);
                var settings = _settingsManager.Settings;
                
                // Yeni tema sisteminde backdrop kullanmıyoruz
                if (_window != null)
                {
                    // Backdrop'u kaldır
                    _window.SystemBackdrop = null;
                    
                    // Grid'in arka planını şeffaf yap (HTML gradient görünsün)
                    if (_window.Content is Panel rootPanel)
                    {
                        rootPanel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Windows.UI.Color.FromArgb(0, 0, 0, 0));
                    }
                    
                    // Pencere çerçevesini gizle
                    if (_window.Content is Grid rootGrid)
                    {
                        var windowBorder = rootGrid.FindName("WindowBorder") as Border;
                        if (windowBorder != null)
                        {
                            windowBorder.BorderThickness = new Thickness(0);
                        }
                    }
                }
                
                // JavaScript'e tema bilgilerini gönder
                await InjectThemeToWebViewAsync(colors);
                
                // XAML tarafı tema güncellemeleri yapılacak (MainWindow'da)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tema uygulanamadı: {ex.Message}");
            }
        }

        private ThemeColors GetThemeColors(AppTheme theme)
        {
            // Yeni tema sisteminde System tema yok
            var colors = _themeColors.ContainsKey(theme) ? _themeColors[theme].Clone() : _themeColors[AppTheme.DarkElegance].Clone();
            
            // Kullanıcı ayarlarına göre dinamik değerleri güncelle
            var settings = _settingsManager.Settings;
            
            // BlurIntensity'ye göre blur miktarını ayarla (widget'lar için)
            colors.BlurAmount = settings.BlurIntensity;
            
            return colors;
        }

        private async Task InjectThemeToWebViewAsync(ThemeColors colors)
        {
            var settings = _settingsManager.Settings;
            
            // FeedbackColor ve AccentColor için varsayılan değerler
            var feedbackColor = !string.IsNullOrEmpty(colors.FeedbackColor) ? colors.FeedbackColor : "#34C759";
            var accentColor = !string.IsNullOrEmpty(colors.AccentColor) ? colors.AccentColor : colors.PrimaryColor;
            
            var script = $@"
                (function() {{
                    // F5 refresh kontrolü - daha güçlü kontrol
                    const isRefresh = sessionStorage.getItem('pageRefreshed') === 'true';
                    const lastTheme = sessionStorage.getItem('lastTheme');
                    const currentTheme = '{settings.Theme}';
                    const isF5Refresh = window.performance && window.performance.navigation && 
                                       window.performance.navigation.type === 1;
                    
                    // F5 ile refresh ve aynı tema ise atla
                    if ((isRefresh || isF5Refresh) && lastTheme === currentTheme) {{
                        console.log('[ThemeManager] F5 refresh with same theme - skipping theme injection');
                        console.log('[ThemeManager] isRefresh:', isRefresh, 'isF5Refresh:', isF5Refresh, 'lastTheme:', lastTheme, 'currentTheme:', currentTheme);
                        return;
                    }}
                    
                    // Tema değişikliğini kaydet
                    sessionStorage.setItem('lastTheme', currentTheme);
                    sessionStorage.setItem('themeApplied', 'true');
                    
                    // CSS Variables güncelleme
                    const root = document.documentElement;
                    
                    // Gradient background
                    document.body.style.background = '{colors.BackgroundGradient}';
                    
                    // Temel renkler
                    root.style.setProperty('--glass-bg', '{colors.SecondaryColor}');
                    root.style.setProperty('--glass-blur', '{colors.BlurAmount}px');
                    root.style.setProperty('--glass-border', '{colors.BorderColor}');
                    root.style.setProperty('--primary-color', '{colors.PrimaryColor}');
                    root.style.setProperty('--accent-color', '{accentColor}');
                    root.style.setProperty('--bg-secondary', '{colors.SecondaryColor}');
                    root.style.setProperty('--text-primary', '{colors.TextColor}');
                    root.style.setProperty('--text-secondary', '{colors.TextColor}');
                    root.style.setProperty('--feedback-color', '{feedbackColor}');
                    root.style.setProperty('--border-color', '{colors.BorderColor}');
                    
                    // Efekt ayarları
                    root.style.setProperty('--glow-intensity', '{colors.GlowIntensity}');
                    root.style.setProperty('--accent-glow', '0 0 {20 * colors.GlowIntensity}px {colors.PrimaryColor}');
                    root.style.setProperty('--animation-speed', '{settings.AnimationSpeed}');
                    root.style.setProperty('--enable-animations', '{(settings.EnableAnimations ? "1" : "0")}');
                    
                    // Tema sınıfı güncelleme
                    document.body.className = document.body.className.replace(/theme-\w+/, '');
                    document.body.classList.add('theme-{settings.Theme.ToString().ToLower()}');
                    
                    // Yeni tema sisteminde full-transparency yok
                    document.body.classList.remove('full-transparency');
                    document.body.classList.remove('theme-clear');
                    
                    // Performans sınıfı
                    document.body.classList.toggle('perf-low', {(settings.Performance == PerformanceProfile.Low ? "true" : "false")});
                    document.body.classList.toggle('perf-high', {(settings.Performance == PerformanceProfile.High ? "true" : "false")});
                    
                    // Efekt toggle'ları
                    document.body.classList.toggle('no-animations', !{settings.EnableAnimations.ToString().ToLower()});
                    document.body.classList.toggle('no-glow', !{settings.EnableGlowEffects.ToString().ToLower()});
                    document.body.classList.toggle('no-parallax', !{settings.EnableParallaxEffects.ToString().ToLower()});
                    
                    // Custom event fire et
                    window.dispatchEvent(new CustomEvent('themeChanged', {{
                        detail: {{
                            theme: '{settings.Theme}',
                            colors: {JsonSerializer.Serialize(colors)}
                        }}
                    }}));
                }})();
            ";

            if (_webViewManager != null)
            {
                await _webViewManager.ExecuteScriptAsync(script);
            }
        }

        private string HexToRgb(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }
            
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            
            return $"{r}, {g}, {b}";
        }

        public async Task<bool> CheckGPUCapabilityAsync()
        {
            try
            {
                var script = @"
                    (function() {
                        const canvas = document.createElement('canvas');
                        const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
                        if (!gl) return { supported: false };
                        
                        const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
                        const vendor = debugInfo ? gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) : 'Unknown';
                        const renderer = debugInfo ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) : 'Unknown';
                        
                        return {
                            supported: true,
                            vendor: vendor,
                            renderer: renderer,
                            maxTextureSize: gl.getParameter(gl.MAX_TEXTURE_SIZE),
                            maxViewportDims: gl.getParameter(gl.MAX_VIEWPORT_DIMS)
                        };
                    })();
                ";

                if (_webViewManager != null)
                {
                    var result = await _webViewManager.ExecuteScriptAsync(script);
                    // GPU bilgilerini parse et ve capability seviyesini belirle
                    return true; // Şimdilik basit kontrol
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public SolidColorBrush GetBrush(string colorKey)
        {
            var theme = GetThemeColors(_settingsManager.Settings.Theme);
            var color = colorKey switch
            {
                "Primary" => theme.PrimaryColor,
                "Background" => theme.BackgroundColor,
                "Text" => theme.TextColor,
                _ => theme.SecondaryColor
            };

            return new SolidColorBrush(ColorHelper.FromArgb(255,
                Convert.ToByte(color.Substring(1, 2), 16),
                Convert.ToByte(color.Substring(3, 2), 16),
                Convert.ToByte(color.Substring(5, 2), 16)));
        }
    }

    public class ThemeColors
    {
        public string BackgroundGradient { get; set; }
        public string BackgroundColor { get; set; }
        public double BackgroundOpacity { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string TextColor { get; set; }
        public string BorderColor { get; set; }
        public string FeedbackColor { get; set; }
        public string AccentColor { get; set; }
        public double GlowIntensity { get; set; }
        public double BlurAmount { get; set; }
        
        public ThemeColors Clone()
        {
            return new ThemeColors
            {
                BackgroundGradient = this.BackgroundGradient,
                BackgroundColor = this.BackgroundColor,
                BackgroundOpacity = this.BackgroundOpacity,
                PrimaryColor = this.PrimaryColor,
                SecondaryColor = this.SecondaryColor,
                TextColor = this.TextColor,
                BorderColor = this.BorderColor,
                FeedbackColor = this.FeedbackColor,
                AccentColor = this.AccentColor,
                GlowIntensity = this.GlowIntensity,
                BlurAmount = this.BlurAmount
            };
        }
    }
}