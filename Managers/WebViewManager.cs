using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using QuadroAIPilot.Services;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Configuration;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.State;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;

namespace QuadroAIPilot.Managers
{
    public class WebViewManager : IWebViewManager
    {
        private WebView2? _webView;
        private DispatcherQueue? _dispatcherQueue;
        private bool _disposed = false;
        private Microsoft.UI.Xaml.Window? _mainWindow;
        // DÜZELTME: Initialization tamamlanma flag'i
        private volatile bool _isInitialized = false;
        private readonly object _initLock = new object();

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<TextareaPositionEventArgs>? TextareaPositionChanged;
        public event EventHandler? OnSettingsRequested;

        public WebViewManager(WebView2 webView, DispatcherQueue dispatcherQueue)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        public void SetMainWindow(Microsoft.UI.Xaml.Window window)
        {
            _mainWindow = window;
        }

        public async Task InitializeAsync()
        {
            // DÜZELTME: Double initialization önleme
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    LogService.LogWarning("[WebViewManager] Already initialized, skipping");
                    return;
                }
            }

            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_disposed || _webView == null)
                {
                    return;
                }

                // CoreWebView2 zaten MainWindow'da başlatıldı, kontrol et
                if (_webView.CoreWebView2 == null)
                {
                    LogService.LogDebug("[WebViewManager] CoreWebView2 henüz hazır değil, başlatılıyor...");
                    await _webView.EnsureCoreWebView2Async();
                }

                // DÜZELTME: CoreWebView2 null check
                if (_webView.CoreWebView2 == null)
                {
                    LogService.LogError("[WebViewManager] CoreWebView2 initialization failed!");
                    return;
                }

                LogService.LogDebug("[WebViewManager] CoreWebView2 hazır!");

                // WebView2 güvenlik ve audio izinleri ayarla
                ConfigureWebViewPermissions();

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index.html");
                // LogService.LogDebug($" HTML path: {htmlPath}");
                // LogService.LogDebug($" File exists: {File.Exists(htmlPath)}");
                
                if (File.Exists(htmlPath))
                {
                    // Convert to file URI for WebView2
                    string fileUri = new Uri(htmlPath).AbsoluteUri;
                    // LogService.LogDebug($" Navigating to: {fileUri}");
                    _webView.CoreWebView2.Navigate(fileUri);
                    // LogService.LogDebug("[WebViewManager] Navigate çağrıldı");
                }
                else
                {
                    LogService.LogDebug($" index.html bulunamadı: {htmlPath}"); // KRITIK HATA - bunu bırak
                    // Load basic HTML as fallback
                    var basicHtml = @"
                        <!DOCTYPE html>
                        <html lang='tr'>
                        <head>
                            <meta charset='utf-8'>
                            <title>QuadroAI Pilot</title>
                            <style>
                                body { background: #1e1e1e; color: white; font-family: 'Segoe UI', Arial; padding: 20px; }
                                textarea { width: 100%; height: 300px; background: #2d2d2d; color: white; border: 1px solid #555; padding: 10px; }
                            </style>
                        </head>
                        <body>
                            <h1>QuadroAI Pilot</h1>
                            <textarea id='txtCikti' placeholder='HTML dosyası yüklenemedi, fallback UI...'></textarea>
                        </body>
                        </html>";
                    _webView.NavigateToString(basicHtml);
                }

                _webView.NavigationCompleted += WebView_NavigationCompleted;

                // DÜZELTME: Initialization tamamlandı flag'ini set et
                lock (_initLock)
                {
                    _isInitialized = true;
                }

                LogService.LogDebug("[WebViewManager] Initialization completed successfully");

                // DÜZELTME: Pending mesajları flush et
                FlushPendingMessages();
            }, "WebViewManager.InitializeAsync");
        }

        private void ConfigureWebViewPermissions()
        {
            try
            {
                if (_webView?.CoreWebView2 == null) return;
                
                // Autoplay ve media permissions
                _webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                _webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                
                // Web Speech API için gerekli ayarlar
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                
                // Permission requests için handler ekle
                _webView.CoreWebView2.PermissionRequested += OnPermissionRequested;
                
                // Document title changed event'i ekle (güvenlik için)
                _webView.CoreWebView2.DocumentTitleChanged += (sender, args) =>
                {
                    // Blob URL'lerin düzgün yüklenmesini kontrol et
                    LogService.LogVerbose($"[WebViewManager] Document title changed: {_webView.CoreWebView2.DocumentTitle}");
                };
                
                LogService.LogDebug("[WebViewManager] WebView permissions configured");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[WebViewManager] ConfigureWebViewPermissions error: {ex.Message}");
            }
        }
        
        private void OnPermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs args)
        {
            // Mikrofon izni için özel işlem
            if (args.PermissionKind == CoreWebView2PermissionKind.Microphone)
            {
                LogService.LogDebug("[WebViewManager] Microphone permission requested");
                
                // Mikrofon iznini ver
                args.State = CoreWebView2PermissionState.Allow;
                args.Handled = true;
                
                LogService.LogDebug("[WebViewManager] Microphone permission granted");
                
                // JavaScript'e mikrofon izninin verildiğini bildir
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Kısa bir gecikme
                    await ExecuteScriptAsync(@"
                        if (window.recognition) {
                            console.log('[WebView] Microphone permission granted, recognition ready');
                        }
                    ");
                });
            }
            // Diğer izinler
            else if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                     args.PermissionKind == CoreWebView2PermissionKind.Autoplay)
            {
                args.State = CoreWebView2PermissionState.Allow;
                args.Handled = true;
                LogService.LogDebug($"[WebViewManager] Permission granted: {args.PermissionKind}");
            }
            else
            {
                // Diğer izinleri reddet
                args.State = CoreWebView2PermissionState.Deny;
                args.Handled = true;
                LogService.LogDebug($"[WebViewManager] Permission denied: {args.PermissionKind}");
            }
        }

        private async void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            // LogService.LogDebug($" NavigationCompleted - Success: {args.IsSuccess}");
            if (!args.IsSuccess) 
            {
                LogService.LogDebug($" Navigation failed!"); // HATA DURUMU - bunu bırak
                return;
            }
            
            // LogService.LogDebug("[WebViewManager] Navigation başarılı, WebMessageReceived event handler ekleniyor");
            
            // WebMessageReceived event handler'ı ekle (zaten ekliyse bir şey olmaz)
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived; // Önce kaldır
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived; // Sonra ekle
            }
            
            // JavaScript'ten refresh kontrolü yapalım
            try
            {
                var refreshCheckScript = @"
                    (function() {
                        // Performance API ile refresh kontrolü
                        const isReload = window.performance && window.performance.navigation && 
                                       window.performance.navigation.type === 1;
                        
                        // SessionStorage ile de kontrol
                        const hasBeenLoaded = sessionStorage.getItem('pageLoadedBefore') === 'true';
                        
                        if (!hasBeenLoaded) {
                            sessionStorage.setItem('pageLoadedBefore', 'true');
                        }
                        
                        return isReload || hasBeenLoaded;
                    })();
                ";
                
                var result = await _webView.CoreWebView2.ExecuteScriptAsync(refreshCheckScript);
                var isRefresh = result == "true";
                
                if (isRefresh)
                {
                    Debug.WriteLine("[WebViewManager] Page refresh detected (F5) - Skipping theme update");
                    return;
                }
                
                // Normal yükleme - tema uygula
                var themeManager = ThemeManager.Instance;
                if (themeManager != null)
                {
                    // Tema uygulaması için kısa bir gecikme
                    await Task.Delay(100);
                    await themeManager.ApplyThemeAsync(themeManager.CurrentTheme);
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[WebViewManager] Refresh check error: {ex.Message}");
                // Hata durumunda tema uygulamayı atla
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                // LogService.LogDebug($" WebMessage alındı: {json}");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Action kontrolü
                if (root.TryGetProperty("action", out var actionElement))
                {
                    var action = actionElement.GetString();
                    
                    // Textarea koordinatlarını kontrol et
                    if (action == "textareaPosition")
                    {
                        var args = new TextareaPositionEventArgs();
                        if (root.TryGetProperty("left", out var leftProp)) args.Left = leftProp.GetDouble();
                        if (root.TryGetProperty("top", out var topProp)) args.Top = topProp.GetDouble();
                        if (root.TryGetProperty("width", out var widthProp)) args.Width = widthProp.GetDouble();
                        if (root.TryGetProperty("height", out var heightProp)) args.Height = heightProp.GetDouble();
                        
                        TextareaPositionChanged?.Invoke(this, args);
                        return;
                    }
                    
                    // Settings açma isteği
                    if (action == "openSettings")
                    {
                        OnSettingsRequested?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    
                    // Arka plan analizi isteği
                    if (action == "analyzeBackground")
                    {
                        AnalyzeBackgroundBrightness();
                        return;
                    }
                    
                    // ÇÖZÜM: Edge TTS tamamlandı bildirimi
                    if (action == "ttsCompleted")
                    {
                        var source = root.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() : "unknown";
                        LogService.LogDebug($"[WebViewManager] TTS completed notification received from: {source}");
                        
                        // TTS state'i otomatik olarak SpeechCompleted event'inde sıfırlanır
                        
                        // SpeechCompleted event'ini tetikle
                        _ = Task.Run(() => 
                        {
                            var speechCompletedEvent = typeof(TextToSpeechService)
                                .GetField("SpeechCompleted", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                ?.GetValue(null) as EventHandler;
                            speechCompletedEvent?.Invoke(null, EventArgs.Empty);
                        });
                        
                        LogService.LogDebug($"[WebViewManager] TTS state reset edildi ve event tetiklendi");
                        return;
                    }
                    
                    // Web Speech API sonuçları
                    if (action == "webSpeechResult")
                    {
                        LogService.LogDebug("[WebViewManager] webSpeechResult action alındı");

                        if (root.TryGetProperty("text", out var textProp) &&
                            root.TryGetProperty("isFinal", out var isFinalProp))
                        {
                            var text = textProp.GetString();
                            var isFinal = isFinalProp.GetBoolean();

                            LogService.LogDebug($"[WebViewManager] webSpeechResult - Text: '{text}', IsFinal: {isFinal}");

                            // WebSpeechBridge'e ilet
                            var webSpeechBridge = DictationManager.GetWebSpeechBridge();
                            if (webSpeechBridge != null)
                            {
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    LogService.LogDebug("[WebViewManager] WebSpeechBridge'e gönderiliyor");
                                    _ = Task.Run(async () =>
                                        await webSpeechBridge.HandleWebSpeechResult(text, isFinal));
                                }
                                else
                                {
                                    LogService.LogDebug("[WebViewManager] Boş text, işlenmiyor");
                                }
                            }
                            else
                            {
                                LogService.LogDebug("[WebViewManager] WebSpeechBridge servisi bulunamadı!");
                            }
                        }
                        else
                        {
                            LogService.LogDebug("[WebViewManager] webSpeechResult - text veya isFinal property bulunamadı");
                        }
                        return;
                    }

                    // VAD (Voice Activity Detection) - Konuşma başladı
                    if (action == "vadSpeechStarted")
                    {
                        LogService.LogInfo("[WebViewManager] [VAD] Konuşma başladı");
                        // İsteğe bağlı: UI güncellemesi yapılabilir
                        return;
                    }

                    // VAD (Voice Activity Detection) - Konuşma bitti
                    if (action == "vadSpeechEnded")
                    {
                        if (root.TryGetProperty("transcript", out var transcriptProp) &&
                            root.TryGetProperty("duration", out var durationProp))
                        {
                            var transcript = transcriptProp.GetString();
                            var duration = durationProp.GetInt32();

                            LogService.LogInfo($"[WebViewManager] [VAD] Konuşma bitti: '{transcript}' (Süre: {duration}ms)");

                            // AI Mode'daysa metni işle
                            if (AppState.CurrentMode == AppState.UserMode.AI)
                            {
                                var dictationManager = ServiceContainer.GetService<IDictationManager>();
                                if (dictationManager != null && !string.IsNullOrWhiteSpace(transcript))
                                {
                                    LogService.LogInfo($"[WebViewManager] [VAD] AI Mode'da, DictationManager'a gönderiliyor: '{transcript}'");

                                    // DictationManager üzerinden işle
                                    _ = Task.Run(() =>
                                    {
                                        dictationManager.HandleTextChanged(transcript);
                                    });
                                }
                                else
                                {
                                    LogService.LogWarning("[WebViewManager] [VAD] DictationManager bulunamadı veya transcript boş");
                                }
                            }
                            else
                            {
                                LogService.LogDebug($"[WebViewManager] [VAD] AI Mode'da değil (Mevcut mod: {AppState.CurrentMode}), işlenmedi");
                            }
                        }
                        return;
                    }


                    // Execute command from Web Speech
                    if (action == "executeCommand")
                    {
                        if (root.TryGetProperty("text", out var textProp))
                        {
                            var commandProcessor = ServiceContainer.GetService<ICommandProcessor>();
                            if (commandProcessor != null)
                            {
                                var text = textProp.GetString();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    _ = Task.Run(async () => 
                                        await commandProcessor.ProcessCommandAsync(text));
                                }
                            }
                        }
                        return;
                    }
                    
                    // Speak action - JavaScript'ten gelen TTS isteği
                    if (action == "speak")
                    {
                        if (root.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                LogService.LogDebug($"[WebViewManager] Speak action received: {text}");
                                
                                // TextToSpeechService kullan
                                _ = Task.Run(async () => 
                                {
                                    await TextToSpeechService.SpeakTextAsync(text);
                                });
                            }
                        }
                        return;
                    }
                    
                    // SpeakTextWithUserName action - Kullanıcı adıyla TTS
                    if (action == "speakTextWithUserName")
                    {
                        if (root.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // Kullanıcı adıyla formatla
                                bool includeComma = true;
                                if (root.TryGetProperty("includeComma", out var commaProp))
                                {
                                    includeComma = commaProp.GetBoolean();
                                }
                                
                                var formattedText = Helpers.UserNameHelper.FormatMessageWithUserName(text, includeComma);
                                LogService.LogDebug($"[WebViewManager] SpeakTextWithUserName: {formattedText}");
                                
                                // TextToSpeechService kullan
                                _ = Task.Run(async () => 
                                {
                                    await TextToSpeechService.SpeakTextAsync(formattedText);
                                });
                            }
                        }
                        return;
                    }
                    
                    // Ready sound action - Uyandırma/uyuma sesleri için
                    if (action == "speakReadySound")
                    {
                        if (root.TryGetProperty("text", out var textProp) &&
                            root.TryGetProperty("soundType", out var soundTypeProp))
                        {
                            var text = textProp.GetString();
                            var soundType = soundTypeProp.GetString();
                            
                            // Kullanıcı adı eklenecek mi kontrol et
                            var useUserName = false;
                            if (root.TryGetProperty("useUserName", out var useUserNameProp))
                            {
                                useUserName = useUserNameProp.GetBoolean();
                            }
                            
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // Uyandırma metinlerine kullanıcı adını ekle
                                if (useUserName && soundType == "wake")
                                {
                                    text = Helpers.UserNameHelper.FormatMessageWithUserName(text, false);
                                }
                                
                                LogService.LogDebug($"[WebViewManager] Ready sound TTS: {soundType} - {text}");
                                
                                // TextToSpeechService kullan
                                _ = Task.Run(async () =>
                                {
                                    await TextToSpeechService.SpeakTextAsync(text);

                                    // TTS bittiğinde JavaScript'e callback yap
                                    await Task.Delay(500); // Kısa bekleme

                                    // UI thread'e geç
                                    _dispatcherQueue?.TryEnqueue(() =>
                                    {
                                        SendMessage(new { action = "readySoundCompleted", soundType });
                                    });
                                });
                            }
                        }
                        return;
                    }
                }

                // Diğer mesajları ana window'a ilet
                MessageReceived?.Invoke(this, json);
            }
            catch (Exception ex)
            {
                LogService.LogDebug($" JSON parse hatası: {ex.Message}");
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pendingMessages = new();
        private System.Threading.Timer _messageFlushTimer;

        public void SendMessage(object message)
        {
            try
            {
                // JSON serialize işlemi - thread-safe
                string json;
                try
                {
                    json = JsonSerializer.Serialize(message);
                }
                catch (Exception jsonEx)
                {
                    LogService.LogError($"[WebViewManager] JSON serialization error: {jsonEx.Message}");
                    return;
                }

                // Mod değişikliği mesajlarını logla
                if (json.Contains("modeChanged"))
                {
                    LogService.LogInfo($"[WebViewManager] Sending modeChanged message: {json}");
                }

                // DÜZELTME: Initialization kontrolü
                if (!_isInitialized)
                {
                    _pendingMessages.Enqueue(json);
                    LogService.LogDebug($"[WebViewManager] Not initialized yet, message queued (Queue size: {_pendingMessages.Count})");

                    // Timer'ı başlat (henüz başlatılmadıysa)
                    if (_messageFlushTimer == null)
                    {
                        _messageFlushTimer = new System.Threading.Timer(_ => FlushPendingMessages(), null, 500, 500);
                    }
                    return;
                }

                // WebView null kontrolü - CoreWebView2'ye ERİŞME! (COM exception olur)
                if (_webView == null)
                {
                    _pendingMessages.Enqueue(json);
                    LogService.LogWarning($"[WebViewManager] WebView is null, message queued (Queue size: {_pendingMessages.Count})");

                    // Timer'ı başlat (henüz başlatılmadıysa)
                    if (_messageFlushTimer == null)
                    {
                        _messageFlushTimer = new System.Threading.Timer(_ => FlushPendingMessages(), null, 500, 500);
                    }
                    return;
                }

                // DispatcherQueue yoksa hata
                if (_dispatcherQueue == null)
                {
                    LogService.LogError("[WebViewManager] DispatcherQueue is null, cannot send message");
                    _pendingMessages.Enqueue(json); // Kuyruğa al, belki sonra düzelir
                    return;
                }

                // HER ZAMAN UI thread'e yönlendir (COM güvenliği için)
                bool enqueued = _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // Double-check WebView (UI thread içinde)
                        if (_webView?.CoreWebView2 != null)
                        {
                            _webView.CoreWebView2.PostWebMessageAsString(json);

                            if (json.Contains("modeChanged"))
                            {
                                LogService.LogInfo("[WebViewManager] modeChanged message posted to WebView");
                            }
                        }
                        else
                        {
                            LogService.LogWarning("[WebViewManager] CoreWebView2 became null before sending");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"[WebViewManager] UI thread mesaj gönderme hatası: {ex.Message}\nStack: {ex.StackTrace}");
                    }
                });

                if (!enqueued)
                {
                    LogService.LogError($"[WebViewManager] Failed to enqueue message to UI thread");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[WebViewManager] Mesaj gönderme hatası: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void FlushPendingMessages()
        {
            // DÜZELTME: Initialization kontrolü
            if (!_isInitialized)
            {
                LogService.LogDebug("[WebViewManager] Not initialized yet, flush postponed");
                return;
            }

            // Timer background thread'de çalışır, UI thread'e geçmemiz lazım
            if (_dispatcherQueue == null)
            {
                LogService.LogWarning("[WebViewManager] DispatcherQueue is null, cannot flush messages");
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // DÜZELTME: Double-check initialization ve WebView hazır mı kontrol et
                    if (!_isInitialized || _webView?.CoreWebView2 == null || _pendingMessages.IsEmpty)
                    {
                        return;
                    }

                    LogService.LogInfo($"[WebViewManager] Flushing {_pendingMessages.Count} pending messages");

                    while (_pendingMessages.TryDequeue(out var json))
                    {
                        try
                        {
                            _webView.CoreWebView2.PostWebMessageAsString(json);
                            LogService.LogDebug($"[WebViewManager] Pending message flushed");
                        }
                        catch (Exception ex)
                        {
                            LogService.LogError($"[WebViewManager] Flush error: {ex.Message}");
                        }
                    }

                    // Timer'ı durdur
                    _messageFlushTimer?.Dispose();
                    _messageFlushTimer = null;

                    LogService.LogInfo("[WebViewManager] Message flush completed");
                }
                catch (Exception ex)
                {
                    LogService.LogError($"[WebViewManager] FlushPendingMessages error: {ex.Message}");
                }
            });
        }

        public void UpdateText(string text)
        {
            SendMessage(new { action = "updateText", text });
        }

        public void UpdateButtonText(string buttonId, string text)
        {
            SendMessage(new { action = "updateButtonText", buttonId, text });
        }

        public void SetButtonState(string buttonId, bool disabled)
        {
            SendMessage(new { action = "setButtonState", buttonId, disabled });
        }

        public void SendWidgetUpdate(string widgetType, object data)
        {
            SendMessage(new { action = "updateWidget", widgetType, data });
        }

        public void UpdateDictationState(bool isActive)
        {
            SendMessage(new { action = "updateDictationState", isActive });
        }

        public async Task AppendFeedback(string message)
        {
            await Task.CompletedTask; // Async signature için gerekli
            SendMessage(new { action = "appendFeedback", text = message });
        }

        public async Task AppendOutput(string message, bool isError = false)
        {
            // AppendOutput logları gereksiz - sadece SendMessage çağır
            await Task.CompletedTask; // Async signature için gerekli
            SendMessage(new { action = "appendOutput", text = message, isSystem = isError });
        }

        public void ClearText()
        {
            SendMessage(new { action = "clearText" });
        }

        public async Task ClearContent()
        {
            await Task.CompletedTask; // Async signature için gerekli
            SendMessage(new { action = "clearText" });
        }        public async Task ClearTextForce()
        {
            // UI thread'inde olduğundan emin olarak işlemi gerçekleştir
            if (_dispatcherQueue?.HasThreadAccess == false)
            {
                var tcs = new TaskCompletionSource<bool>();
                _dispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await PerformClearTextForceAsync();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($" UI thread ClearTextForce hatası: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                await tcs.Task;
            }
            else
            {
                await PerformClearTextForceAsync();
            }
        }

        public async Task SpeakWithEdgeTTS(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            
            // Önce ses çıkışını test et
            await TestAudioCapabilityAsync();
            
            // UI thread'inde olduğundan emin olarak işlemi gerçekleştir
            if (_dispatcherQueue?.HasThreadAccess == false)
            {
                var tcs = new TaskCompletionSource<bool>();
                _dispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await PerformSpeakWithEdgeTTSAsync(text);
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($" UI thread SpeakWithEdgeTTS hatası: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                await tcs.Task;
            }
            else
            {
                await PerformSpeakWithEdgeTTSAsync(text);
            }
        }

        private async Task PerformSpeakWithEdgeTTSAsync(string text)
        {
            try
            {
                // UI thread'de olduğumuzdan emin olalım
                if (_dispatcherQueue?.HasThreadAccess == false)
                {
                    LogService.LogDebug("[WebViewManager] PerformSpeakWithEdgeTTSAsync UI thread'de değil!");
                }
                
                // Mevcut Edge sesini belirle
                string voiceName = "Emel"; // Varsayılan
                if (TextToSpeechService.CurrentEdgeVoice == "tr-TR-AhmetNeural")
                {
                    voiceName = "Ahmet";
                }
                else if (TextToSpeechService.CurrentEdgeVoice == "tr-TR-EmelNeural")
                {
                    voiceName = "Emel";
                }
                
                // ÇÖZÜM: UI thread kontrolü yapıldı, güvenli şekilde SendMessage kullan
                // JavaScript'e speakWithEdge mesajı gönder
                SendMessage(new { action = "speakWithEdge", text = text, voice = voiceName });
                
                LogService.LogDebug($"[WebViewManager] Edge TTS mesajı gönderildi - Voice: {voiceName}");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($" PerformSpeakWithEdgeTTSAsync hatası: {ex.Message}");
            }
        }

        public async Task SendAudioStreamAsync(byte[] audioData, string format = "webm", string text = null)
        {
            if (_disposed || _webView == null || audioData == null || audioData.Length == 0) 
            {
                return;
            }

            // Audio data'yı base64'e çevir
            string base64Audio = Convert.ToBase64String(audioData);
            
            // Text boşsa boş string kullan
            if (string.IsNullOrEmpty(text))
            {
                text = "";
            }

            // UI thread'inde olduğundan emin olalım
            if (_dispatcherQueue?.HasThreadAccess == false)
            {
                var tcs = new TaskCompletionSource<bool>();
                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // WebView mesaj sistemi ile audio data gönder
                        SendMessage(new { 
                            action = "playAudioStream", 
                            audioData = base64Audio,
                            format = format,
                            text = text
                        });
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($" UI thread audio stream hatası: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                await tcs.Task;
            }
            else
            {
                // Zaten UI thread'deyiz
                SendMessage(new { 
                    action = "playAudioStream", 
                    audioData = base64Audio,
                    format = format,
                    text = text
                });
            }
        }

        private async Task PerformClearTextForceAsync()
        {
            try
            {
                // Önce mesaj ile temizle
                SendMessage(new { action = "clearText" });
                
                // Kısa bir bekleme
                await Task.Delay(50);
                
                // Sonra JavaScript ile de emin ol
                await _webView.CoreWebView2.ExecuteScriptAsync(@"
                    const textarea = document.querySelector('#txtCikti');
                    console.log('[WebViewManager Script] textarea bulundu mu:', !!textarea);
                    if (textarea) {
                        console.log('[WebViewManager Script] Mevcut değer:', textarea.value);
                        textarea.value = '';
                        textarea.focus();
                        textarea.setSelectionRange(0, 0);
                        if (typeof checkButtonStates === 'function') {
                            checkButtonStates();
                        }
                        console.log('[WebViewManager Script] textarea temizlendi, yeni değer:', textarea.value);
                    } else {
                        console.error('[WebViewManager Script] textarea bulunamadı!');
                    }
                ");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($" PerformClearTextForceAsync hatası: {ex.Message}");
            }
        }
        
        public void StopTTS()
        {
            try
            {
                LogService.LogDebug("[WebViewManager] StopTTS çağrıldı");
                SendMessage(new { action = "stopTTS" });
            }
            catch (Exception ex)
            {
                LogService.LogError($"[WebViewManager] StopTTS hatası: {ex.Message}");
            }
        }
        
        private async Task TestAudioCapabilityAsync()
        {
            try
            {
                
                string testScript = @"
                    (async function() {
                        console.log('[WebViewManager] Ses testi başlıyor...');
                        
                        // Web Speech API kontrolü
                        const hasSpeechSynthesis = 'speechSynthesis' in window;
                        console.log('[WebViewManager] speechSynthesis mevcut:', hasSpeechSynthesis);
                        
                        if (hasSpeechSynthesis) {
                            const voices = window.speechSynthesis.getVoices();
                            console.log('[WebViewManager] Ses sayısı:', voices.length);
                            
                            // Türkçe sesleri kontrol et
                            const turkishVoices = voices.filter(v => v.lang.includes('tr-TR'));
                            console.log('[WebViewManager] Türkçe ses sayısı:', turkishVoices.length);
                            
                            // Edge Neural sesleri kontrol et
                            const edgeVoices = voices.filter(v => 
                                v.name.includes('Neural') || v.name.includes('Online'));
                            console.log('[WebViewManager] Edge Neural ses sayısı:', edgeVoices.length);
                        }
                        
                        // AudioContext kontrolü
                        try {
                            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
                            console.log('[WebViewManager] AudioContext state:', audioContext.state);
                            
                            // Resume if suspended
                            if (audioContext.state === 'suspended') {
                                await audioContext.resume();
                                console.log('[WebViewManager] AudioContext resumed');
                            }
                            
                            audioContext.close();
                        } catch (e) {
                            console.error('[WebViewManager] AudioContext hatası:', e);
                        }
                        
                        return hasSpeechSynthesis;
                    })();
                ";
                
                var result = await _webView.CoreWebView2.ExecuteScriptAsync(testScript);
            }
            catch (Exception ex)
            {
                LogService.LogDebug($" Ses testi hatası: {ex.Message}");
            }
        }        public async Task FocusTextArea()
        {
            
            // UI thread safe ExecuteScript kullan
            await ExecuteScript(@"
                const textarea = document.querySelector('#txtCikti');
                if (textarea) {
                    textarea.click();
                    textarea.focus();
                    textarea.setSelectionRange(textarea.value.length, textarea.value.length);
                    console.log('[WebViewManager] FocusTextArea - textarea focus edildi');
                    
                    // Ek güvenlik için tekrar focus at
                    setTimeout(function() {
                        textarea.focus();
                    }, 100);
                } else {
                    console.error('[WebViewManager] FocusTextArea - textarea bulunamadı!');
                }
            ");
        }

        public async Task ExecuteScript(string script)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_disposed || _webView?.CoreWebView2 == null) return;
                
                // UI thread'inde olduğundan emin olalım
                if (_dispatcherQueue?.HasThreadAccess == false)
                {
                    
                    // TaskCompletionSource kullanarak senkron bekle
                    var tcs = new TaskCompletionSource<bool>();
                    
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await _webView.CoreWebView2.ExecuteScriptAsync(script);
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            LogService.LogDebug($" ExecuteScript hatası: {ex.Message}");
                            tcs.SetException(ex);
                        }
                    });
                    
                    await tcs.Task;
                }
                else
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }, "WebViewManager.ExecuteScript");
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            return await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_disposed) return string.Empty;

                // SECURITY FIX: Script validation
                if (!SecurityValidator.IsScriptSafe(script))
                {
                    LogService.LogWarning("[SECURITY] Unsafe script blocked in ExecuteScriptAsync");
                    return string.Empty;
                }

                // WebView veya CoreWebView2 null kontrolü
                if (_webView == null)
                {
                    LogService.LogDebug("[WebViewManager] ExecuteScriptAsync: _webView is null");
                    return string.Empty;
                }
                
                // UI thread'inde olduğundan emin olalım
                if (_dispatcherQueue?.HasThreadAccess == false)
                {
                    // TaskCompletionSource kullanarak senkron bekle
                    var tcs = new TaskCompletionSource<string>();
                    
                    bool enqueued = _dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                    {
                        try
                        {
                            // UI thread'de tekrar kontrol
                            if (_disposed || _webView == null) 
                            {
                                tcs.SetResult(string.Empty);
                                return;
                            }
                            
                            // CoreWebView2 kontrolü
                            if (_webView.CoreWebView2 == null)
                            {
                                LogService.LogDebug("[WebViewManager] ExecuteScriptAsync: CoreWebView2 is null in UI thread");
                                tcs.SetResult(string.Empty);
                                return;
                            }
                            
                            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                            
                            // JSON string'i parse et ve log'la
                            if (!string.IsNullOrEmpty(result) && result != "null" && result != "undefined")
                            {
                                try
                                {
                                    // JSON string'inden tırnak işaretlerini kaldır
                                    var cleanResult = result.Trim('"');
                                    LogService.LogInfo($"[WebViewManager] ExecuteScriptAsync result (UI thread): {cleanResult}");
                                }
                                catch
                                {
                                    LogService.LogInfo($"[WebViewManager] ExecuteScriptAsync raw result (UI thread): {result}");
                                }
                            }
                            
                            tcs.SetResult(result ?? string.Empty);
                        }
                        catch (Exception ex)
                        {
                            LogService.LogDebug($"[WebViewManager] ExecuteScriptAsync hatası: {ex.Message}");
                            tcs.SetResult(string.Empty); // Exception yerine empty string dön
                        }
                    });
                    
                    if (!enqueued)
                    {
                        LogService.LogDebug("[WebViewManager] ExecuteScriptAsync: Failed to enqueue to UI thread");
                        return string.Empty;
                    }
                    
                    return await tcs.Task;
                }
                else
                {
                    // Zaten UI thread'deyiz
                    if (_webView.CoreWebView2 == null)
                    {
                        LogService.LogDebug("[WebViewManager] ExecuteScriptAsync: CoreWebView2 is null");
                        return string.Empty;
                    }
                    
                    var result = await _webView.CoreWebView2.ExecuteScriptAsync(script) ?? string.Empty;
                    
                    // JSON string'i parse et ve log'la
                    if (!string.IsNullOrEmpty(result) && result != "null" && result != "undefined")
                    {
                        try
                        {
                            // JSON string'inden tırnak işaretlerini kaldır
                            var cleanResult = result.Trim('"');
                            LogService.LogInfo($"[WebViewManager] ExecuteScriptAsync result: {cleanResult}");
                        }
                        catch
                        {
                            LogService.LogInfo($"[WebViewManager] ExecuteScriptAsync raw result: {result}");
                        }
                    }
                    
                    return result;
                }
            }, "WebViewManager.ExecuteScriptAsync", string.Empty);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private async void AnalyzeBackgroundBrightness()
        {
            try
            {
                // MainWindow kullan
                if (_mainWindow == null) return;

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                
                // Pencere pozisyonunu al
                if (!GetWindowRect(hWnd, out var rect)) return;

                // Pencerenin merkez noktasını hesapla
                int centerX = (rect.Left + rect.Right) / 2;
                int centerY = (rect.Top + rect.Bottom) / 2;

                // Ekran görüntüsü al ve parlaklık analizi yap
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        // Merkez noktasından bir piksel al
                        graphics.CopyFromScreen(centerX, centerY, 0, 0, new System.Drawing.Size(1, 1));
                        var pixelColor = bitmap.GetPixel(0, 0);
                        
                        // Parlaklık hesapla (0-1 aralığında)
                        float brightness = pixelColor.GetBrightness();
                        
                        // Sonucu JavaScript'e gönder
                        SendMessage(new { 
                            action = "backgroundAnalysisResult", 
                            brightness = brightness 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Arka plan analizi hatası: {ex.Message}");
                // Hata durumunda varsayılan değer gönder
                SendMessage(new { 
                    action = "backgroundAnalysisResult", 
                    brightness = 0.5 
                });
            }
        }


        /// <summary>
        /// Loads HTML content into the WebView
        /// </summary>
        public async Task LoadHtmlContentAsync(string htmlContent)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                LogService.LogDebug("Loading HTML content into WebView");
                
                // Wait for WebView to be ready
                if (_webView == null || _webView.CoreWebView2 == null)
                {
                    LogService.LogDebug("WebView not ready, waiting for initialization");
                    // Wait a bit for initialization
                    await Task.Delay(1000);
                }
                
                // Navigate to the HTML content
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _webView.NavigateToString(htmlContent);
                });
                
                LogService.LogDebug("HTML content loaded successfully");
            }, "LoadHtmlContentAsync");
        }

        #region IDisposable Implementation

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
                    // Detach events
                    if (_webView != null)
                    {
                        _webView.NavigationCompleted -= WebView_NavigationCompleted;
                        
                        // Detach CoreWebView2 events if available
                        if (_webView.CoreWebView2 != null)
                        {
                            _webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                        }
                    }

                    // Clear references
                    _webView = null;
                    _dispatcherQueue = null;

                    // Clear event handlers
                    MessageReceived = null;
                    TextareaPositionChanged = null;

                    _disposed = true;
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($" Dispose error: {ex.Message}");
                }
            }
        }

        #endregion
    }

    public class TextareaPositionEventArgs : EventArgs
    {
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
        public double Width { get; set; } = -1;
        public double Height { get; set; } = -1;
    }
}
