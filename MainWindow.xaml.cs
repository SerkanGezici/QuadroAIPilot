using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Web.WebView2.Core;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;
using QuadroAIPilot.Services.AI;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Helpers;
using QuadroAIPilot.State;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WinRT.Interop;

namespace QuadroAIPilot
{
    /// <summary>
    /// Refactored MainWindow - Delegates all operations to specialized managers
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        #region Specialized Managers
        
        private readonly UIManager _uiManager;
        private readonly EventCoordinator _eventCoordinator;
        private readonly WindowController _windowController;
        private readonly ThemeManager _themeManager;
        private readonly SettingsManager _settingsManager;
        
        // Core dependencies
        private readonly IWebViewManager _webViewManager;
        private readonly IDictationManager _dictationManager;
        private readonly WindowManager _windowManager;
        
        // Animation storyboards
        private Storyboard _voiceIndicatorAnimation;
        private bool _isVoiceIndicatorAnimationActive = false;
        
        #endregion
        
        #region State Variables
        
        private readonly IntPtr _hWnd;
        private const int AppBarWidth = 300;
        private bool _disposed = false;

        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// Gets the WebViewManager instance
        /// </summary>
        public IWebViewManager WebViewManager => _webViewManager;
        
        #endregion

        #region Constructor and Initialization
        
        public MainWindow()
        {
            this.InitializeComponent();

            // Get dependencies from ServiceContainer after XAML initialization
            var modeManager = ServiceContainer.GetService<ModeManager>();
            _dictationManager = new DictationManager(modeManager);
            
            // Create WebViewManager with the actual WebView2 control from XAML
            if (webView == null)
            {
                throw new InvalidOperationException("WebView2 control not found in XAML");
            }
            _webViewManager = new WebViewManager(webView, DispatcherQueue.GetForCurrentThread());
            
            // Set WebViewManager in DictationManager
            _dictationManager.SetWebViewManager(_webViewManager);
            
            // Set WebViewManager in CommandProcessor
            var commandProcessor = ServiceContainer.GetService<ICommandProcessor>();
            commandProcessor.SetWebViewManager(_webViewManager);
            
            // Create managers that need the window handle and WebView
            _hWnd = WindowNative.GetWindowHandle(this);
            
            // Create WindowManager with window handle
            _windowManager = new WindowManager(_hWnd, AppBarWidth);
            
            // WebViewManager'a window referansını ver
            if (_webViewManager is WebViewManager webViewMgr)
            {
                webViewMgr.SetMainWindow(this);
            }
            
            // Create UIManager with WebViewManager and DispatcherQueue
            _uiManager = new UIManager(_webViewManager, DispatcherQueue.GetForCurrentThread());
            
            // Create WindowController with all required parameters
            _windowController = new WindowController(
                _hWnd,
                _windowManager,
                _dictationManager,
                _webViewManager,
                DispatcherQueue.GetForCurrentThread());
            
            // Create EventCoordinator with all required parameters
            _eventCoordinator = new EventCoordinator(
                ServiceContainer.GetService<ICommandProcessor>(),
                _dictationManager,
                _webViewManager,
                _uiManager,
                DispatcherQueue.GetForCurrentThread());

            // WindowManager AppBar operations
            _windowManager.SaveCurrentWorkArea();
            // AppBar modu şeffaflığı engelliyor - geçici olarak devre dışı
            // _windowManager.RegisterAppBar(_hWnd, AppBarWidth);
            
            // Window'u görünür yap
            this.Activate();
            
            // Şeffaflık için ek ayarlar
            this.ExtendsContentIntoTitleBar = true;
            
            // Grid'in default arka planını ayarla
            if (this.Content is Grid rootGrid)
            {
                // Varsayılan olarak şeffaf arka plan
                rootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
            else
            {
                Debug.WriteLine("[MainWindow] Warning: Root element is not a Grid");
            }
            
            // Window event handlers
            this.Closed += MainWindow_Closed;

            // Setup coordinate tracking for WindowController
            SetupCoordinateTracking();

            // Event coordinator initialization
            _eventCoordinator.AttachEvents();
            
            // Initialize theme and settings managers
            _settingsManager = SettingsManager.Instance;
            _themeManager = ThemeManager.Instance;
            
            // Apply OS-appropriate backdrop
            ApplySystemBackdrop();
            
            // Yeni tema sisteminde window şeffaflığı yok
            _themeManager.SetWebViewManager(_webViewManager);
            
            // Setup UI event handlers
            SetupUIEventHandlers();
            
            // Initialize animations
            InitializeAnimations();

            // WebView initialization - on main thread
            InitializeWebViewAsync();
            
            // Multi-account email system initialization - handle async safely
            _ = Task.Run(async () =>
            {
                await ErrorHandler.SafeExecuteAsync(async () =>
                {
                    await InitializeEmailSystemAsync();
                }, "MainWindow_EmailSystemInitialization");
            });
            
            // Setup mode change event handler
            AppState.ModeChanged += OnModeChanged;
        }

        #endregion
        
        #region UI Event Handlers Setup
        
        /// <summary>
        /// Sets up UI event handlers for settings and theme controls
        /// </summary>
        private void SetupUIEventHandlers()
        {
            // Settings button click (will be added to HTML)
            _webViewManager.OnSettingsRequested += async (s, e) => 
            {
                await ShowSettingsAsync();
            };
            
            // Setup keyboard shortcuts
            // WinUI3'te KeyDown yerine Content'e event handler ekliyoruz
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.KeyDown += MainWindow_KeyDown;
            }
        }
        
        /// <summary>
        /// Handles keyboard shortcuts at the window level
        /// </summary>
        private async void MainWindow_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Check if Ctrl key is pressed
            var isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            
            if (isCtrlPressed)
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.Space:
                    case Windows.System.VirtualKey.K:
                        // Forward to WebView to show command palette
                        await _webViewManager.ExecuteScriptAsync("toggleCommandPalette()");
                        e.Handled = true;
                        break;
                        
                    case Windows.System.VirtualKey.D:
                        // Toggle dictation
                        await _webViewManager.ExecuteScriptAsync("toggleDikte()");
                        e.Handled = true;
                        break;
                        
                    case Windows.System.VirtualKey.Enter:
                        // Execute command
                        await _webViewManager.ExecuteScriptAsync("executeCommand()");
                        e.Handled = true;
                        break;
                        
                    case Windows.System.VirtualKey.L:
                        // Clear all
                        await _webViewManager.ExecuteScriptAsync("executeCommandAction('clearAll')");
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F11)
            {
                // Toggle focus mode
                await _webViewManager.ExecuteScriptAsync("toggleFocusMode()");
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                // Close all modals
                await _webViewManager.ExecuteScriptAsync("closeAllModals()");
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Initializes animations for UI elements
        /// </summary>
        private void InitializeAnimations()
        {
            // Voice indicator pulse animation
            _voiceIndicatorAnimation = new Storyboard();
            
            var scaleX = new DoubleAnimation
            {
                From = 1.0,
                To = 1.5,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            
            var scaleY = new DoubleAnimation
            {
                From = 1.0,
                To = 1.5,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            
            Storyboard.SetTarget(scaleX, OuterRingScale);
            Storyboard.SetTargetProperty(scaleX, "ScaleX");
            
            Storyboard.SetTarget(scaleY, OuterRingScale);
            Storyboard.SetTargetProperty(scaleY, "ScaleY");
            
            _voiceIndicatorAnimation.Children.Add(scaleX);
            _voiceIndicatorAnimation.Children.Add(scaleY);
        }
        
        /// <summary>
        /// Shows voice indicator with animation
        /// </summary>
        public async Task ShowVoiceIndicatorAsync(string status = "Dinleniyor...")
        {
            await Task.Run(() => DispatcherQueue.TryEnqueue(() =>
            {
                if (StatusText != null)
                    StatusText.Text = status;
                    
                if (GlassOverlay != null)
                    GlassOverlay.Visibility = Visibility.Visible;
                    
                if (_voiceIndicatorAnimation != null)
                {
                    _isVoiceIndicatorAnimationActive = true;
                    _voiceIndicatorAnimation.Begin();
                }
            }));
        }
        
        /// <summary>
        /// Hides voice indicator
        /// </summary>
        public async Task HideVoiceIndicatorAsync()
        {
            await Task.Run(() => DispatcherQueue.TryEnqueue(() =>
            {
                StopVoiceIndicatorAnimation();
                
                if (GlassOverlay != null)
                    GlassOverlay.Visibility = Visibility.Collapsed;
            }));
        }
        
        /// <summary>
        /// Stops voice indicator animation safely
        /// </summary>
        private void StopVoiceIndicatorAnimation()
        {
            if (_voiceIndicatorAnimation != null && _isVoiceIndicatorAnimationActive)
            {
                _voiceIndicatorAnimation.Stop();
                _isVoiceIndicatorAnimationActive = false;
            }
        }
        
        /// <summary>
        /// Shows settings dialog
        /// </summary>
        private async Task ShowSettingsAsync()
        {
            try
            {
                var settingsDialog = new Dialogs.SettingsDialog();
                settingsDialog.XamlRoot = this.Content.XamlRoot;
                
                var result = await settingsDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    Debug.WriteLine("[MainWindow] Settings saved successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error showing settings dialog: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Coordinate Tracking Setup
        
        /// <summary>
        /// Sets up coordinate tracking between WebView and WindowController
        /// </summary>
        private void SetupCoordinateTracking()
        {
            _webViewManager.TextareaPositionChanged += OnTextareaPositionChanged;
        }

        /// <summary>
        /// Handles textarea position changes and updates WindowController
        /// </summary>
        private void OnTextareaPositionChanged(object? sender, TextareaPositionEventArgs e)
        {
            if (e == null) return;
            
            // Update WindowController with new coordinates
            _windowController.UpdateTextareaCoordinates(e.Left, e.Top, e.Width, e.Height);
            
            // Debug.WriteLine($"[MainWindow] Textarea coordinates updated and forwarded to WindowController: L={e.Left}, T={e.Top}, W={e.Width}, H={e.Height}");
        }

        #endregion

        #region Window Event Handlers
        
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                Dispose();
                Debug.WriteLine("[MainWindow] Window closed - Dispose called");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Window close error: {ex.Message}");
            }
        }

        #endregion
        
        #region Email System Initialization
        
        /// <summary>
        /// Initializes multi-account email system
        /// </summary>
        private async Task InitializeEmailSystemAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                Debug.WriteLine("[MainWindow] Multi-account email system starting...");
                
                // Simple email account manager initialization
                var emailAccountManager = new Services.SimpleEmailAccountManager();
                await emailAccountManager.InitializeAccountsAsync();
                
                var accounts = emailAccountManager.GetActiveAccounts();
                Debug.WriteLine($"[MainWindow] {accounts.Count} email accounts loaded");
                
                // Kişiselleştirilmiş selamlama göster
                await ShowPersonalizedGreetingAsync();
                
                // Send account summary to UI through UIManager
                if (accounts.Count > 0)
                {
                    await _uiManager.ShowInfoMessageAsync($"📧 {accounts.Count} Email Account Loaded");
                    
                    foreach (var account in accounts.Take(3))
                    {
                        var status = account.IsAuthenticated ? "✓" : "○";
                        await _uiManager.ShowInfoMessageAsync($"{status} {account.FriendlyName}");
                    }
                    
                    if (accounts.Count > 3)
                    {
                        await _uiManager.ShowInfoMessageAsync($"... and {accounts.Count - 3} more accounts");
                    }
                }
            }, "InitializeEmailSystem");
        }

        #endregion
        
        #region Personalized Greeting
        
        /// <summary>
        /// Kişiselleştirilmiş selamlama göster
        /// </summary>
        private async Task ShowPersonalizedGreetingAsync()
        {
            try
            {
                var profileService = ServiceContainer.GetService<PersonalProfileService>();
                var profile = await profileService.LoadProfileAsync();
                
                if (profile != null && profile.IsActive)
                {
                    var greeting = profileService.GetPersonalizedGreeting(profile);
                    
                    // WebView üzerinden kişiselleştirilmiş selamlama göster
                    await _webViewManager.ExecuteScriptAsync($@"
                        // Kişiselleştirilmiş selamlama göster
                        const greetingDiv = document.createElement('div');
                        greetingDiv.className = 'personalized-greeting';
                        greetingDiv.innerHTML = `
                            <div class='greeting-content'>
                                <h2>{greeting.Replace("'", "\\'")}</h2>
                                <p>QuadroAI Pilot'a hoş geldiniz</p>
                            </div>
                        `;
                        greetingDiv.style.cssText = `
                            position: fixed;
                            top: 20px;
                            right: 20px;
                            background: var(--glass-bg);
                            backdrop-filter: blur(20px);
                            border: 1px solid rgba(255, 255, 255, 0.1);
                            border-radius: 16px;
                            padding: 20px 30px;
                            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
                            animation: slideIn 0.5s ease-out, fadeOut 0.5s ease-out 4.5s forwards;
                            z-index: 10000;
                        `;
                        
                        // Animasyon stilleri
                        if (!document.getElementById('greeting-styles')) {{
                            const style = document.createElement('style');
                            style.id = 'greeting-styles';
                            style.textContent = `
                                @keyframes slideIn {{
                                    from {{
                                        transform: translateX(100%);
                                        opacity: 0;
                                    }}
                                    to {{
                                        transform: translateX(0);
                                        opacity: 1;
                                    }}
                                }}
                                @keyframes fadeOut {{
                                    from {{
                                        opacity: 1;
                                    }}
                                    to {{
                                        opacity: 0;
                                    }}
                                }}
                                .greeting-content h2 {{
                                    margin: 0 0 8px 0;
                                    color: var(--primary-color);
                                    font-size: 24px;
                                    font-weight: 600;
                                }}
                                .greeting-content p {{
                                    margin: 0;
                                    color: var(--text-secondary);
                                    font-size: 14px;
                                }}
                            `;
                            document.head.appendChild(style);
                        }}
                        
                        document.body.appendChild(greetingDiv);
                        
                        // 5 saniye sonra kaldır
                        setTimeout(() => {{
                            greetingDiv.remove();
                        }}, 5000);
                    ");
                    
                    // Doğum günü ise özel efekt ekle
                    if (profile.IsBirthdayToday())
                    {
                        await _webViewManager.ExecuteScriptAsync(@"
                            // Doğum günü konfeti efekti
                            const confettiScript = document.createElement('script');
                            confettiScript.src = 'https://cdn.jsdelivr.net/npm/canvas-confetti@1.5.1/dist/confetti.browser.min.js';
                            confettiScript.onload = () => {
                                confetti({
                                    particleCount: 100,
                                    spread: 70,
                                    origin: { y: 0.6 }
                                });
                            };
                            document.head.appendChild(confettiScript);
                        ");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] ShowPersonalizedGreeting error: {ex.Message}");
            }
        }
        
        #endregion

        #region Public Interface Methods

        /// <summary>
        /// Gets current UI processing state
        /// </summary>
        public bool IsProcessing => _uiManager.IsProcessing;

        /// <summary>
        /// Clears UI content
        /// </summary>
        public async Task ClearContentAsync()
        {
            await _uiManager.ClearContentAsync();
        }

        /// <summary>
        /// Shows success message
        /// </summary>
        public async Task ShowSuccessAsync(string message)
        {
            await _uiManager.ShowSuccessFeedbackAsync(message);
        }

        /// <summary>
        /// Shows error message
        /// </summary>
        public async Task ShowErrorAsync(string message)
        {
            await _uiManager.ShowErrorFeedbackAsync(message);
        }

        /// <summary>
        /// Brings window to foreground
        /// </summary>
        public void BringToForeground()
        {
            _windowController.BringWindowToForeground();
        }

        /// <summary>
        /// Starts dictation with focus
        /// </summary>
        public async Task StartDictationAsync()
        {
            await ShowVoiceIndicatorAsync("Dinleniyor...");
            await _windowController.StartDictationWithFocusAsync(forceRestart: true);
        }

        /// <summary>
        /// Restarts dictation after command processing
        /// </summary>
        public async Task RestartDictationAsync()
        {
            await _windowController.RestartDictationWithFocusAsync();
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Stop animations first
                    StopVoiceIndicatorAnimation();
                    
                    // Dispose animation resources
                    if (_voiceIndicatorAnimation != null)
                    {
                        _voiceIndicatorAnimation.Stop();
                        _voiceIndicatorAnimation = null;
                    }
                    
                    // Detach event coordinator
                    _eventCoordinator?.DetachEvents();
                    _eventCoordinator?.Dispose();
                    
                    // Dispose managers
                    _uiManager?.Dispose();
                    _windowController?.Dispose();
                    
                    // Clean up coordinate tracking
                    if (_webViewManager != null)
                    {
                        _webViewManager.TextareaPositionChanged -= OnTextareaPositionChanged;
                        _webViewManager.Dispose();
                    }
                    
                    // Clean up dictation
                    _dictationManager?.Dispose();
                    
                    // Stop Browser Integration Service - with timeout
                    var browserIntegrationService = ServiceContainer.GetOptionalService<IBrowserIntegrationService>();
                    if (browserIntegrationService != null)
                    {
                        try
                        {
                            var stopTask = browserIntegrationService.StopAsync();
                            if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                            {
                                Debug.WriteLine("[MainWindow] Browser Integration Service stop timeout");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MainWindow] Browser Integration Service stop error: {ex.Message}");
                        }
                    }
                    
                    // WindowManager cleanup
                    _windowManager?.Dispose();
                    
                    // Window event cleanup
                    this.Closed -= MainWindow_Closed;
                    
                    // Clean up mode change event handler
                    AppState.ModeChanged -= OnModeChanged;
                    
                    // Clean up WebView event handlers
                    if (webView != null)
                    {
                        webView.NavigationCompleted -= WebView_NavigationCompleted;
                    }
                    
                    _disposed = true;
                    Debug.WriteLine("[MainWindow] Dispose completed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainWindow] Dispose error: {ex.Message}");
                }
            }
        }

        #endregion
        
        #region Window Transparency
        
        /// <summary>
        /// Enables window transparency using Win32 API
        /// </summary>
        /// <summary>
        /// Applies appropriate system backdrop based on OS support
        /// </summary>
        private void ApplySystemBackdrop()
        {
            try
            {
                var currentTheme = _settingsManager.Settings.Theme;
                
                // Yeni tema sisteminde backdrop kullanmıyoruz
                // ThemeManager zaten backdrop'ı kaldırıyor
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error applying system backdrop: {ex.Message}");
                // Continue without backdrop - not critical
            }
        }
        
        private void EnableWindowTransparency()
        {
            try
            {
                var windowsApiService = new WindowsApiService();
                
                // Window'u şeffaf yap - sabit değer kullan
                windowsApiService.SetWindowTransparency(_hWnd, 0.95);
                
                // Cam çerçeve efekti ekle
                windowsApiService.ExtendGlassFrame(_hWnd);
                
                Debug.WriteLine("[MainWindow] Window transparency enabled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Failed to enable window transparency: {ex.Message}");
            }
        }
        
        #endregion

        #region Event Handlers

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            Debug.WriteLine($"[MainWindow] WebView navigation completed");
            
            // WebView navigation completion will be handled by WebViewManager
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                Debug.WriteLine("[MainWindow] WebView initialization başlıyor...");
                Debug.WriteLine($"[MainWindow] WebView instance: {webView != null}");
                Debug.WriteLine($"[MainWindow] WebViewManager instance: {_webViewManager != null}");
                
                // WebView2'yi özel ayarlarla başlat
                await webView.EnsureCoreWebView2Async();
                
                // Kullanılan Edge versiyonunu logla
                Debug.WriteLine($"[MainWindow] WebView2 Version: {webView.CoreWebView2.Environment.BrowserVersionString}");
                Debug.WriteLine($"[MainWindow] User Data Folder: {webView.CoreWebView2.Environment.UserDataFolder}");
                
                // CoreWebView2 başlatıldıktan sonra ayarları yapılandır
                if (webView.CoreWebView2 != null)
                {
                    // WebView2 ayarları
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                    
                    // WebView2 arka planını şeffaf yap
                    webView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    
                    // WebView2 için özel arka plan stili
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                        document.body.style.backgroundColor = 'transparent';
                        document.documentElement.style.backgroundColor = 'transparent';
                    ");
                    
                    Debug.WriteLine("[MainWindow] WebView2 başlatıldı ve ayarları yapılandırıldı");
                }
                
                await _webViewManager.InitializeAsync();
                Debug.WriteLine("[MainWindow] WebView başarıyla initialize edildi");
                
                // F5 refresh kontrolü - sadece ilk yüklemede tema uygula
                var refreshCheckScript = @"
                    (function() {
                        // Check if this is a refresh (F5)
                        const isRefresh = sessionStorage.getItem('pageRefreshed') === 'true';
                        const lastTheme = sessionStorage.getItem('lastTheme');
                        const currentTheme = '" + _settingsManager.Settings.Theme.ToString() + @"';
                        
                        if (isRefresh && lastTheme === currentTheme) {
                            console.log('[MainWindow] F5 refresh detected with same theme - skipping theme initialization');
                            return true;
                        }
                        
                        // Not a refresh or theme changed
                        return false;
                    })();
                ";
                
                var isRefreshResult = await webView.CoreWebView2.ExecuteScriptAsync(refreshCheckScript);
                var isRefresh = isRefreshResult == "true";
                
                // Her durumda theme manager'ı initialize et ama F5'te tema uygulamayı atla
                await _themeManager.InitializeAsync(this);
                
                if (!isRefresh)
                {
                    // İlk yükleme - tema uygula
                    await _themeManager.ApplyThemeAsync(_settingsManager.Settings.Theme);
                    Debug.WriteLine("[MainWindow] Theme system initialized and theme applied");
                }
                else
                {
                    Debug.WriteLine("[MainWindow] F5 refresh detected - theme manager initialized but theme application skipped");
                    
                    // F5'te sadece tema değişkenlerinin doğru olduğundan emin ol
                    var verifyScript = @"
                        console.log('[MainWindow] Verifying theme after F5...');
                        console.log('Current theme class:', document.body.className);
                        console.log('Background:', document.body.style.background);
                        sessionStorage.setItem('f5Verified', 'true');
                    ";
                    await webView.CoreWebView2.ExecuteScriptAsync(verifyScript);
                }
                
                // TextToSpeechService'e WebViewManager'ı bağla
                TextToSpeechService.SetWebViewManager(_webViewManager);
                Debug.WriteLine("[MainWindow] WebViewManager TextToSpeechService'e bağlandı");
                
                // Start Browser Integration Service
                var browserIntegrationService = ServiceContainer.GetService<IBrowserIntegrationService>();
                await browserIntegrationService.StartAsync();
                Debug.WriteLine("[MainWindow] Browser Integration Service başlatıldı");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] WebView initialization hatası: {ex.Message}");
                Debug.WriteLine($"[MainWindow] Stack Trace: {ex.StackTrace}");
                
                // Fallback: Basic HTML yükle
                try
                {
                    var basicHtml = @"
                        <!DOCTYPE html>
                        <html>
                        <head><title>QuadroAI Pilot</title></head>
                        <body style='background:#1e1e1e;color:white;font-family:Segoe UI;padding:20px;'>
                            <h1>QuadroAI Pilot - Fallback Mode</h1>
                            <p>WebView2 yüklenemedi. Lütfen Microsoft Edge WebView2 Runtime kurulu olduğundan emin olun.</p>
                            <textarea id='txtCikti' style='width:100%;height:300px;background:#2d2d2d;color:white;border:1px solid #555;'></textarea>
                        </body>
                        </html>";
                    webView.NavigateToString(basicHtml);
                    Debug.WriteLine("[MainWindow] Fallback HTML yüklendi");
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"[MainWindow] Fallback HTML yükleme hatası: {fallbackEx.Message}");
                }
            }
        }

        #endregion

        #region UI Enhancement Event Handlers
        
        /// <summary>
        /// Handles mode changes - shows/hides writing mode engine selector
        /// </summary>
        private void OnModeChanged(object sender, AppState.UserMode mode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Show/hide writing mode engine selector
                // Win+H kaldırıldı, artık sadece Web Speech API kullanılıyor
            });
        }
        

        /// <summary>
        /// Komut önerisi butonuna tıklandığında
        /// </summary>
        private async void SuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Content is string command)
                {
                    // Önerilen komutu çalıştır
                    await ProcessUserCommand(command);
                    
                    // Öneri overlay'ini gizle
                    HideSuggestionsOverlay();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Suggestion button click error: {ex.Message}");
            }
        }

        /// <summary>
        /// Komutu öğret butonuna tıklandığında
        /// </summary>
        private async void LearnCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Kullanıcıdan komut açıklaması al
                var dialog = new ContentDialog
                {
                    Title = "Komut Öğretme",
                    Content = new TextBox 
                    { 
                        PlaceholderText = "Bu komut ne yapmalı?",
                        TextWrapping = TextWrapping.Wrap
                    },
                    PrimaryButtonText = "Öğret",
                    CloseButtonText = "İptal"
                };

                dialog.XamlRoot = this.Content.XamlRoot;
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var textBox = dialog.Content as TextBox;
                    if (!string.IsNullOrWhiteSpace(textBox?.Text))
                    {
                        // Komutu öğrenme servisine ekle
                        var learningService = ServiceContainer.GetService<UserLearningService>();
                        await learningService.AddCustomCommandAsync(
                            _lastFailedCommand, 
                            textBox.Text);
                        
                        // Başarı mesajı göster
                        await ShowNotification("Komut öğrenildi!");
                    }
                }

                HideErrorFeedbackOverlay();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Learn command error: {ex.Message}");
            }
        }

        /// <summary>
        /// Hata mesajını kapat
        /// </summary>
        private void DismissError_Click(object sender, RoutedEventArgs e)
        {
            HideErrorFeedbackOverlay();
        }

        /// <summary>
        /// Komut önerilerini göster
        /// </summary>
        public async Task ShowCommandSuggestions(List<string> suggestions)
        {
            await Task.Run(() => DispatcherQueue.TryEnqueue(() =>
            {
                if (suggestions != null && suggestions.Any())
                {
                    SuggestionsList.ItemsSource = suggestions;
                    SuggestionsOverlay.Visibility = Visibility.Visible;
                    
                    // 10 saniye sonra otomatik gizle
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                    timer.Tick += (s, e) =>
                    {
                        HideSuggestionsOverlay();
                        timer.Stop();
                    };
                    timer.Start();
                }
            }));
        }

        /// <summary>
        /// Hata geri bildirimini göster
        /// </summary>
        public async Task ShowErrorFeedback(string message, List<string> suggestions, bool showLearnOption = false)
        {
            await Task.Run(() => DispatcherQueue.TryEnqueue(() =>
            {
                ErrorMessageText.Text = message;
                ErrorSuggestionsList.ItemsSource = suggestions;
                LearnCommandButton.Visibility = showLearnOption ? Visibility.Visible : Visibility.Collapsed;
                ErrorFeedbackOverlay.Visibility = Visibility.Visible;
            }));
        }

        /// <summary>
        /// Bildirim göster
        /// </summary>
        private async Task ShowNotification(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Bilgi",
                Content = message,
                CloseButtonText = "Tamam"
            };
            
            dialog.XamlRoot = this.Content.XamlRoot;
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Öneri overlay'ini gizle
        /// </summary>
        private void HideSuggestionsOverlay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SuggestionsOverlay.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Hata geri bildirimi overlay'ini gizle
        /// </summary>
        private void HideErrorFeedbackOverlay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ErrorFeedbackOverlay.Visibility = Visibility.Collapsed;
            });
        }

        // Başarısız komut takibi için
        private string _lastFailedCommand = string.Empty;

        /// <summary>
        /// Kullanıcı komutunu işle (mevcut metodun güncellenmesi için)
        /// </summary>
        private async Task ProcessUserCommand(string command)
        {
            try
            {
                _lastFailedCommand = command;
                
                // Komut işleme mantığı (EventCoordinator üzerinden)
                var success = await _eventCoordinator.ProcessVoiceCommand(command);
                
                if (!success)
                {
                    // Hata durumunda geri bildirim göster
                    var feedbackService = ServiceContainer.GetService<IErrorFeedbackService>();
                    if (feedbackService != null)
                    {
                        var intentResult = await ServiceContainer.GetService<LocalIntentDetector>()
                            .DetectIntentAsync(command);
                            
                        var feedback = await feedbackService.GetFeedbackForError(command, intentResult);
                        
                        await ShowErrorFeedback(
                            feedback.Message, 
                            feedback.Suggestions.ToList(),
                            !string.IsNullOrEmpty(feedback.LearningSuggestion));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Process command error: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Bullet point converter for XAML binding
    /// </summary>
    public class BulletPointConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string text)
            {
                return $"• {text}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}