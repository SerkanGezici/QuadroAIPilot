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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace QuadroAIPilot
{
    /// <summary>
    /// Refactored MainWindow - Delegates all operations to specialized managers
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        #region P/Invoke Declarations
        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        
        // Modifier keys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        
        // Virtual key codes
        private const uint VK_Q = 0x51;
        
        #endregion
        
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
        private readonly ModeManager _modeManager;
        
        // Animation storyboards
        private Storyboard _voiceIndicatorAnimation;
        private bool _isVoiceIndicatorAnimationActive = false;
        
        #endregion
        
        #region State Variables
        
        private readonly IntPtr _hWnd;
        private const int DefaultWindowSize = 300; // Varsayılan pencere boyutu (kare)
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
            SimpleCrashLogger.Log("MainWindow constructor started");
            
            try
            {
                // WebView2 User Data Folder'ı ayarla - InitializeComponent'ten ÖNCE!
                string userDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QuadroAIPilot",
                    "WebView2"
                );
                
                // Dizin yoksa oluştur
                Directory.CreateDirectory(userDataPath);
                
                // Environment variable'ı ayarla
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataPath);
                SimpleCrashLogger.Log($"WebView2 User Data Folder set to: {userDataPath}");
                
                this.InitializeComponent();
                SimpleCrashLogger.Log("MainWindow.InitializeComponent completed");
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "MainWindow.InitializeComponent");
                throw;
            }

            try
            {
                SimpleCrashLogger.Log("Getting services from container...");

                // Get dependencies from ServiceContainer after XAML initialization
                _modeManager = ServiceContainer.GetService<ModeManager>();
                SimpleCrashLogger.Log("Got ModeManager");

                _dictationManager = new DictationManager(_modeManager);
                SimpleCrashLogger.Log("Created DictationManager");

                // Set DictationManager in ModeManager
                _modeManager.SetDictationManager(_dictationManager);
                SimpleCrashLogger.Log("Set DictationManager in ModeManager");
                
                // Create WebViewManager with the actual WebView2 control from XAML
                if (webView == null)
                {
                    SimpleCrashLogger.Log("ERROR: WebView2 control not found in XAML!");
                    throw new InvalidOperationException("WebView2 control not found in XAML");
                }
                SimpleCrashLogger.Log("WebView2 control found in XAML");
                
                _webViewManager = new WebViewManager(webView, DispatcherQueue.GetForCurrentThread());
                SimpleCrashLogger.Log("Created WebViewManager");
                
                // Register WebViewManager in ServiceLocator for global access
                ServiceLocator.SetWebViewManager(_webViewManager);
                SimpleCrashLogger.Log("WebViewManager registered in ServiceLocator");
            
            // Set WebViewManager in DictationManager
            _dictationManager.SetWebViewManager(_webViewManager);
            
            // Set WebViewManager in CommandProcessor
            var commandProcessor = ServiceContainer.GetService<ICommandProcessor>();
            commandProcessor.SetWebViewManager(_webViewManager);

            // Set ModeManager in CommandProcessor
            commandProcessor.SetModeManager(_modeManager);
            
            // Create managers that need the window handle and WebView
            _hWnd = WindowNative.GetWindowHandle(this);
            
            // Create WindowManager with window handle
            _windowManager = new WindowManager(_hWnd, DefaultWindowSize);
            
            // WebViewManager'a window referansını ver
            if (_webViewManager is WebViewManager webViewMgr)
            {
                webViewMgr.SetMainWindow(this);
            }
            
            // Create UIManager with WebViewManager and DispatcherQueue
            _uiManager = new UIManager(_webViewManager, DispatcherQueue.GetForCurrentThread());
            
            // Initialize settings manager early (before using it)
            _settingsManager = SettingsManager.Instance;
            
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

            // Pencere pozisyonlama Window_Loaded event'inde yapılacak
            // Bu, DisplayArea'nın doğru değerleri döndürmesini sağlar
            
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

            // WinUI3'te Window'un Loaded event'i yok, Activated kullanılır
            // Ancak pozisyonlama için bir kez çalışması gerekiyor
            bool positionSet = false;
            this.Activated += (sender, args) =>
            {
                if (!positionSet)
                {
                    positionSet = true;
                    // Küçük bir gecikme ile pozisyonlamayı yap
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        Window_Loaded(sender, args);
                    });
                }
            };

            // Setup coordinate tracking for WindowController
            SetupCoordinateTracking();

            // Event coordinator initialization
            _eventCoordinator.AttachEvents();
            
            // Initialize theme manager
            _themeManager = ThemeManager.Instance;
            
            // Apply OS-appropriate backdrop
            ApplySystemBackdrop();
            
            // Yeni tema sisteminde window şeffaflığı yok
            _themeManager.SetWebViewManager(_webViewManager);
            
            // Setup UI event handlers
            SetupUIEventHandlers();
            
            // Initialize animations
            InitializeAnimations();

            // WebView initialization - on UI thread (must stay on UI thread!)
            _ = InitializeWebViewAsyncSafe();
            
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
            
                SimpleCrashLogger.Log("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "MainWindow constructor");
                throw;
            }
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

                    case Windows.System.VirtualKey.Number1:
                        // Ctrl+1: Komut Modu
                        _modeManager.Switch(AppState.UserMode.Command);
                        e.Handled = true;
                        break;

                    case Windows.System.VirtualKey.Number2:
                        // Ctrl+2: Yazı Modu
                        _modeManager.Switch(AppState.UserMode.Writing);
                        e.Handled = true;
                        break;

                    case Windows.System.VirtualKey.Number3:
                        // Ctrl+3: AI Asistan Modu
                        _modeManager.Switch(AppState.UserMode.AI);
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
                LogService.LogDebug("[Settings] Dialog açılıyor...");
                
                // XamlRoot null kontrolü
                if (this.Content?.XamlRoot == null)
                {
                    LogService.LogError("[Settings] XamlRoot null! Dialog açılamıyor.");
                    return;
                }
                
                var settingsDialog = new Dialogs.SettingsDialog();
                settingsDialog.XamlRoot = this.Content.XamlRoot;
                
                LogService.LogDebug("[Settings] Dialog oluşturuldu, ShowAsync çağrılıyor...");
                var result = await settingsDialog.ShowAsync();
                
                LogService.LogDebug($"[Settings] Dialog sonucu: {result}");
                
                if (result == ContentDialogResult.Primary)
                {
                    Debug.WriteLine("[MainWindow] Settings saved successfully");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[Settings] Dialog hatası: {ex.Message}");
                LogService.LogError($"[Settings] Stack Trace: {ex.StackTrace}");
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

        private void Window_Loaded(object sender, object e)
        {
            try
            {
                SimpleCrashLogger.Log("Window_Loaded event started");

                // Pencere pozisyonunu ayarla (Window tam yüklendikten sonra)
                SetupWindowPosition();

                SimpleCrashLogger.Log("Window_Loaded event completed");
            }
            catch (Exception ex)
            {
                SimpleCrashLogger.LogException(ex, "Window_Loaded");
                Debug.WriteLine($"[MainWindow] Window_Loaded error: {ex.Message}");
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // Pencere konumunu ve boyutunu kaydet
                SaveWindowBounds();
                
                Dispose();
                Debug.WriteLine("[MainWindow] Window closed - Dispose called");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Window close error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Saves current window position and size to settings
        /// </summary>
        private void SaveWindowBounds()
        {
            try
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                if (appWindow != null)
                {
                    var position = appWindow.Position;
                    var size = appWindow.Size;
                    
                    _settingsManager.Settings.WindowBounds.X = position.X;
                    _settingsManager.Settings.WindowBounds.Y = position.Y;
                    _settingsManager.Settings.WindowBounds.Width = size.Width;
                    _settingsManager.Settings.WindowBounds.Height = size.Height;
                    
                    // Ayarları kaydet
                    _ = Task.Run(async () => await _settingsManager.SaveSettingsAsync());
                    
                    Debug.WriteLine($"[MainWindow] Window bounds saved: X={position.X}, Y={position.Y}, W={size.Width}, H={size.Height}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error saving window bounds: {ex.Message}");
            }
        }

        /// <summary>
        /// Pencere pozisyonunu ayarlar ve ekranda görünür olmasını sağlar
        /// </summary>
        private void SetupWindowPosition()
        {
            try
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow == null)
                {
                    Debug.WriteLine("[MainWindow] AppWindow is null, skipping position setup");
                    return;
                }

                // Kayıtlı pencere ayarlarını al
                var bounds = _settingsManager.Settings.WindowBounds;

                // Ekran boyutunu al
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);

                if (displayArea == null || displayArea.WorkArea.Width <= 0 || displayArea.WorkArea.Height <= 0)
                {
                    Debug.WriteLine("[MainWindow] DisplayArea not ready, using default position");
                    // DisplayArea hazır değilse, güvenli varsayılan değerler kullan
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = 100,
                        Y = 100,
                        Width = bounds.Width,
                        Height = bounds.Height
                    });
                }
                else
                {
                    // Pencere pozisyonunu hesapla ve ekranda olmasını sağla
                    var position = EnsureWindowIsOnScreen(bounds.X, bounds.Y, bounds.Width, bounds.Height, displayArea);

                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = position.X,
                        Y = position.Y,
                        Width = position.Width,
                        Height = position.Height
                    });

                    Debug.WriteLine($"[MainWindow] Window positioned at: X={position.X}, Y={position.Y}, W={position.Width}, H={position.Height}");
                }

                // Pencereyi yeniden boyutlandırılabilir yap
                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error setting up window position: {ex.Message}");
                SimpleCrashLogger.LogException(ex, "SetupWindowPosition");
            }
        }

        /// <summary>
        /// Pencere pozisyonunun ekranda görünür olmasını sağlar
        /// </summary>
        private (int X, int Y, int Width, int Height) EnsureWindowIsOnScreen(int x, int y, int width, int height, Microsoft.UI.Windowing.DisplayArea displayArea)
        {
            var workAreaWidth = displayArea.WorkArea.Width;
            var workAreaHeight = displayArea.WorkArea.Height;

            // Minimum görünür alan (pencere en az %10'u görünür olmalı)
            var minVisible = 100;

            // Pencere boyutu ekrandan büyükse, ekrana sığdır
            if (width > workAreaWidth)
                width = workAreaWidth - 100;
            if (height > workAreaHeight)
                height = workAreaHeight - 100;

            // Pencere sağ kenardan taşıyorsa
            if (x + minVisible > workAreaWidth)
                x = workAreaWidth - width - 50;

            // Pencere alt kenardan taşıyorsa
            if (y + minVisible > workAreaHeight)
                y = workAreaHeight - height - 50;

            // Pencere sol kenardan taşıyorsa
            if (x + width < minVisible)
                x = 50;

            // Pencere üst kenardan taşıyorsa (negatif Y)
            if (y < 0)
                y = 50;

            return (x, y, width, height);
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
                // Multi-account email system starting...
                
                // Simple email account manager initialization
                var emailAccountManager = new Services.SimpleEmailAccountManager();
                await emailAccountManager.InitializeAccountsAsync();
                
                var accounts = emailAccountManager.GetActiveAccounts();
                // {accounts.Count} email accounts loaded
                
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

        #region Window Subclassing for Hotkey
        
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _oldWndProc;
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        private const int GWL_WNDPROC = -4;
        
        private static IntPtr SetWindowLongPtrHelper(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
        
        private void SubclassWindow()
        {
            _wndProcDelegate = new WndProcDelegate(WndProc);
            IntPtr wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProc = SetWindowLongPtrHelper(_hWnd, GWL_WNDPROC, wndProcPtr);
        }
        
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                Debug.WriteLine("[MainWindow] Hotkey (Ctrl+Shift+Q) algılandı!");
                
                // UI thread'inde dikteyi başlat
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await _webViewManager?.ExecuteScript("toggleDikte()");
                });
                
                return IntPtr.Zero;
            }
            
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
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
                    // Unregister hotkey
                    UnregisterHotKey(_hWnd, HOTKEY_ID);
                    Debug.WriteLine("[MainWindow] Hotkey unregistered");
                    
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
                    
                    // Clear ServiceLocator
                    ServiceLocator.Clear();
                    Debug.WriteLine("[MainWindow] ServiceLocator cleared");
                    
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
            // WebView navigation completed

            // WebView navigation completion will be handled by WebViewManager
        }

        /// <summary>
        /// Safely initializes WebView2 on UI thread with error handling (async Task pattern)
        /// CRITICAL: This method MUST run on UI thread - WebView2 is a COM object requiring STA thread
        /// </summary>
        private async Task InitializeWebViewAsyncSafe()
        {
            try
            {
                // Call the internal initialization method
                // This STAYS on UI thread (no Task.Run) - WebView2 requires UI thread
                await InitializeWebViewAsyncInternal();
            }
            catch (Exception ex)
            {
                LogService.LogError($"[MainWindow] WebView initialization error: {ex.Message}", ex);
                Debug.WriteLine($"[MainWindow] WebView initialization hatası: {ex.Message}");
                Debug.WriteLine($"[MainWindow] Stack Trace: {ex.StackTrace}");

                // Try fallback HTML on UI thread
                try
                {
                    var basicHtml = @"
                        <!DOCTYPE html>
                        <html>
                        <head><title>QuadroAI Pilot - Error Recovery</title></head>
                        <body style='background:#1e1e1e;color:white;font-family:Segoe UI;padding:20px;'>
                            <h1>🔄 QuadroAI Pilot - Recovery Mode</h1>
                            <p style='color:#ff6b6b;'>WebView2 başlatılamadı. Lütfen aşağıdaki adımları deneyin:</p>
                            <ul>
                                <li>Microsoft Edge WebView2 Runtime kurulu olduğundan emin olun</li>
                                <li>Uygulamayı yeniden başlatın</li>
                                <li>Gerekirse bilgisayarınızı yeniden başlatın</li>
                            </ul>
                            <p style='color:#888; font-size:12px; margin-top:30px;'>Error: " + ex.Message + @"</p>
                        </body>
                        </html>";
                    webView.NavigateToString(basicHtml);
                    Debug.WriteLine("[MainWindow] Fallback HTML loaded successfully");
                }
                catch (Exception fallbackEx)
                {
                    LogService.LogError($"[MainWindow] Fallback HTML error: {fallbackEx.Message}", fallbackEx);
                    Debug.WriteLine($"[MainWindow] Fallback HTML yükleme hatası: {fallbackEx.Message}");
                }
            }
        }

        private async Task InitializeWebViewAsyncInternal()
        {
            try
            {
                // WebView initialization başlıyor...
                
                // WebView2'yi başlat
                // User data folder environment variable ile zaten ayarlandı
                await webView.EnsureCoreWebView2Async();
                
                // CoreWebView2 başlatıldıktan sonra ayarları yapılandır
                if (webView.CoreWebView2 != null)
                {
                    // WebView2 runtime versiyon kontrolü
                    try
                    {
                        var webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                        LogService.LogDebug($"[MainWindow] WebView2 Runtime Version: {webView2Version}");
                        
                        // Minimum versiyon kontrolü (93.0.954.0)
                        if (!string.IsNullOrEmpty(webView2Version))
                        {
                            // Version numarasını parse et
                            var versionParts = webView2Version.Split(' ')[0].Split('.');
                            if (versionParts.Length >= 3)
                            {
                                if (int.TryParse(versionParts[0], out int major))
                                {
                                    if (major < 93)
                                    {
                                        LogService.LogWarning($"[MainWindow] WebView2 Runtime version too old: {webView2Version}. Minimum required: 93.0.954.0");
                                        // WebViewManager henüz hazır olmayabilir, bu yüzden doğrudan JavaScript çalıştır
                                        await webView.CoreWebView2.ExecuteScriptAsync(
                                            "appendFeedback('⚠️ WebView2 Runtime versiyonu eski. Web Speech API düzgün çalışmayabilir.')");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($"[MainWindow] WebView2 version check failed: {ex.Message}");
                    }
                    
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
                
                // Register global hotkey (Ctrl+Shift+Q)
                bool hotkeyRegistered = RegisterHotKey(_hWnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_Q);
                if (hotkeyRegistered)
                {
                    Debug.WriteLine("[MainWindow] Hotkey (Ctrl+Shift+Q) başarıyla kaydedildi");
                    
                    // Subclass window to handle WM_HOTKEY messages
                    SubclassWindow();
                }
                else
                {
                    Debug.WriteLine("[MainWindow] Hotkey kaydedilemedi!");
                }
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