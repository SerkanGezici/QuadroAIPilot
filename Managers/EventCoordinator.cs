using Microsoft.UI.Dispatching;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Commands;
using QuadroAIPilot.Services;
using QuadroAIPilot.State;
using QuadroAIPilot.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Coordinates all event handling and routing between components
    /// </summary>
    public class EventCoordinator : IDisposable
    {
        #region Fields

        private readonly ICommandProcessor _commandProcessor;
        private readonly IDictationManager _dictationManager;
        private readonly IWebViewManager _webViewManager;
        private readonly UIManager _uiManager;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _disposed = false;

        // Event handling state
        private volatile bool _eventsAttached = false;
        private readonly object _eventLock = new object();
        
        // State tracking
        private string? _lastMode = null;

        #endregion

        #region Constructor

        public EventCoordinator(
            ICommandProcessor commandProcessor,
            IDictationManager dictationManager,
            IWebViewManager webViewManager,
            UIManager uiManager,
            DispatcherQueue dispatcherQueue)
        {
            _commandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));
            _dictationManager = dictationManager ?? throw new ArgumentNullException(nameof(dictationManager));
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        #endregion

        #region Event Setup and Teardown

        /// <summary>
        /// Attaches all event handlers
        /// </summary>
        public void AttachEvents()
        {
            lock (_eventLock)
            {
                if (_eventsAttached) return;

                try
                {
                // Command processing events
                _commandProcessor.CommandProcessed += OnCommandProcessed;
                
                // Application state events
                AppState.StateChanged += OnAppStateChanged;

                // TTS events for UI feedback
                TextToSpeechService.SpeechGenerated += OnSpeechGenerated;
                TextToSpeechService.OutputGenerated += OnOutputGenerated;
                
                // WebView events
                _webViewManager.MessageReceived += OnWebViewMessageReceived;
                _webViewManager.TextareaPositionChanged += OnTextareaPositionChanged;
                
                // Dictation events
                // TextRecognized artık kullanılmıyor - ModeManager üzerinden işleniyor
                _dictationManager.StateChanged += OnDictationStateChanged;

                    _eventsAttached = true;
                    //("[EventCoordinator] All events attached successfully");
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError(ex, "EventCoordinator.AttachEvents");
                }
            }
        }

        /// <summary>
        /// Detaches all event handlers
        /// </summary>
        public void DetachEvents()
        {
            lock (_eventLock)
            {
                if (!_eventsAttached) return;

                try
                {
                // Command processing events
                _commandProcessor.CommandProcessed -= OnCommandProcessed;
                
                // Application state events
                AppState.StateChanged -= OnAppStateChanged;

                // TTS events
                TextToSpeechService.SpeechGenerated -= OnSpeechGenerated;
                TextToSpeechService.OutputGenerated -= OnOutputGenerated;
                
                // WebView events
                _webViewManager.MessageReceived -= OnWebViewMessageReceived;
                _webViewManager.TextareaPositionChanged -= OnTextareaPositionChanged;
                
                // Dictation events
                // TextRecognized artık kullanılmıyor - ModeManager üzerinden işleniyor
                _dictationManager.StateChanged -= OnDictationStateChanged;

                    _eventsAttached = false;
                    //("[EventCoordinator] All events detached successfully");
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError(ex, "EventCoordinator.DetachEvents");
                }
            }
        }

        #endregion

        #region Command Processing Events

        /// <summary>
        /// Handles command processing completion
        /// </summary>
        private void OnCommandProcessed(object? sender, CommandProcessResult e)
        {
            // Use Task.Run to handle async work safely in event handler
            _ = Task.Run(async () =>
            {
                await ErrorHandler.SafeExecuteAsync(async () =>
                {
                    //($"[EventCoordinator] Command processed: {e.CommandText}, Success: {e.Success}");
                    
                    if (e.Success)
                    {
                        await _uiManager.ShowSuccessFeedbackAsync($"Komut başarılı: {e.DetectedIntent}");
                    }
                    else
                    {
                        // Hata mesajına detay ekle
                        string errorMessage = string.IsNullOrWhiteSpace(e.ResultMessage) 
                            ? $"Komut işlenemedi: '{e.CommandText}'" 
                            : e.ResultMessage;
                        
                        await _uiManager.ShowErrorFeedbackAsync(errorMessage);
                    }

                    // Set processing complete
                    _dictationManager.SetProcessingComplete();
                    
                    // Clear the dictation text area in UI through UIManager (UI thread safe)
                    await _uiManager.ClearContentForceAsync();
                }, "OnCommandProcessed");
            });
        }

        #endregion

        #region Application State Events

        /// <summary>
        /// Handles application state changes
        /// </summary>
        private void OnAppStateChanged(object? sender, AppState.ApplicationState e)
        {
            ErrorHandler.SafeExecute(() =>
            {
                var currentMode = AppState.CurrentMode;
                
                // Sadece gerçek mod değişimlerinde mesaj göster
                if (_lastMode != currentMode.ToString())
                {
                    _lastMode = currentMode.ToString();
                    
                    // WebView'a mod değişikliğini hemen bildir (UI thread'de)
                    LogService.LogInfo($"[EventCoordinator] Notifying WebView of mode change: {currentMode}");
                    
                    var message = new
                    {
                        action = "modeChanged",
                        mode = currentMode.ToString()
                    };
                    
                    _webViewManager.SendMessage(message);
                    
                    LogService.LogInfo($"[EventCoordinator] Mode change message sent to WebView");
                    
                    // UI'da bilgi mesajı göster (async)
                    _ = Task.Run(async () =>
                    {
                        await ErrorHandler.SafeExecuteAsync(async () =>
                        {
                            await _uiManager.ShowInfoMessageAsync($"Mod değişti: {currentMode}");
                        }, "OnAppStateChanged - ShowInfoMessage");
                    });
                }
                
                //($"[EventCoordinator] App state changed to: {currentMode}");
            }, "OnAppStateChanged");
        }

        #endregion

        #region TTS Events

        /// <summary>
        /// Handles TTS speech generation for UI
        /// </summary>
        private void OnSpeechGenerated(object? sender, string text)
        {
            ErrorHandler.SafeExecute(() =>
            {
                _uiManager.HandleSpeechGenerated(text);
            }, "OnSpeechGenerated");
        }

        /// <summary>
        /// Handles TTS output generation for UI
        /// </summary>
        private void OnOutputGenerated(object? sender, string text)
        {
            ErrorHandler.SafeExecute(() =>
            {
                // Output içeriğini loglama - gereksiz
                _uiManager.HandleOutputGenerated(text);
            }, "OnOutputGenerated");
        }

        #endregion

        #region WebView Events

        /// <summary>
        /// Handles incoming WebView messages and routes to appropriate handlers
        /// </summary>
        private void OnWebViewMessageReceived(object? sender, string json)
        {
            ErrorHandler.SafeExecute(() =>
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("action", out var actionElement)) 
                    return;
                
                string? action = actionElement.GetString();
                if (string.IsNullOrEmpty(action)) return;

                //($"[EventCoordinator] WebView message received: {action}");

                switch (action)
                {
                    case "startDikte":
                        _dispatcherQueue.TryEnqueue(() => HandleStartDictation());
                        break;

                    case "stopDikte":
                        _dispatcherQueue.TryEnqueue(() => HandleStopDictation());
                        break;

                    case "speak":
                        if (root.TryGetProperty("text", out var textElement))
                        {
                            var text = textElement.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleSpeakText(text));
                            }
                        }
                        break;

                    case "execute":
                        // "text" veya "command" property'sini kontrol et (widget'lar için uyumluluk)
                        string? commandText = null;
                        
                        if (root.TryGetProperty("text", out var executeTextElement))
                        {
                            commandText = executeTextElement.GetString();
                        }
                        else if (root.TryGetProperty("command", out var commandElement))
                        {
                            commandText = commandElement.GetString();
                        }
                        
                        if (!string.IsNullOrEmpty(commandText))
                        {
                            _dispatcherQueue.TryEnqueue(() => HandleExecuteCommand(commandText));
                        }
                        break;

                    case "requestWidget":
                        if (root.TryGetProperty("widgetType", out var widgetTypeElement))
                        {
                            var widgetType = widgetTypeElement.GetString();
                            if (!string.IsNullOrEmpty(widgetType))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleWidgetRequest(widgetType));
                            }
                        }
                        break;
                        
                    case "widgetClicked":
                        if (root.TryGetProperty("widgetType", out var clickedWidgetTypeElement))
                        {
                            var clickedWidgetType = clickedWidgetTypeElement.GetString();
                            if (!string.IsNullOrEmpty(clickedWidgetType))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleWidgetClick(clickedWidgetType));
                            }
                        }
                        break;
                        
                    case "speakText":
                        if (root.TryGetProperty("text", out var speakTextElement))
                        {
                            var textToSpeak = speakTextElement.GetString();
                            if (!string.IsNullOrEmpty(textToSpeak))
                            {
                                // DictationManager'a TTS metnini HEMEN bildir (async blok dışında)
                                if (_dictationManager != null)
                                {
                                    _dictationManager.UpdateTtsContent(textToSpeak);
                                    LogService.LogInfo($"[EventCoordinator] TTS metni DictationManager'a ÖNCELİKLE bildirildi: {textToSpeak.Substring(0, Math.Min(50, textToSpeak.Length))}...");
                                }
                                
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    await TextToSpeechService.SpeakTextAsync(textToSpeak);
                                });
                            }
                        }
                        break;

                    case "textChanged":
                        if (root.TryGetProperty("text", out var changedTextElement))
                        {
                            var changedText = changedTextElement.GetString();
                            if (!string.IsNullOrEmpty(changedText))
                            {
                                // Gönder butonundan mı geldi kontrol et
                                bool fromSendButton = false;
                                if (root.TryGetProperty("fromSendButton", out var fromSendButtonElement))
                                {
                                    fromSendButton = fromSendButtonElement.GetBoolean();
                                }

                                _dispatcherQueue.TryEnqueue(() => HandleTextChanged(changedText, fromSendButton));
                            }
                        }
                        break;

                    case "stopTts":
                        _dispatcherQueue.TryEnqueue(() => HandleStopTts());
                        break;

                    case "openFile":
                        if (root.TryGetProperty("filePath", out var filePathElement))
                        {
                            var filePath = filePathElement.GetString();
                            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                            {
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    LogService.LogInfo($"[EventCoordinator] Opening file from search result: {filePath}");
                                    
                                    // FileSearchService kullanarak dosyayı aç
                                    var fileService = ServiceContainer.GetService<FileSearchService>();
                                    if (fileService != null)
                                    {
                                        var result = await fileService.OpenFileAsync(filePath);
                                        if (result)
                                        {
                                            var fileName = System.IO.Path.GetFileName(filePath);
                                            await TextToSpeechService.SpeakTextAsync($"{fileName} açılıyor");
                                        }
                                        else
                                        {
                                            await TextToSpeechService.SpeakTextAsync("Dosya açılamadı");
                                        }
                                    }
                                    else
                                    {
                                        // Alternatif yöntem
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = filePath,
                                            UseShellExecute = true
                                        });
                                    }
                                });
                            }
                            else
                            {
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    LogService.LogInfo($"[EventCoordinator] File not found: {filePath}");
                                    await TextToSpeechService.SpeakTextAsync("Dosya bulunamadı");
                                });
                            }
                        }
                        break;

                    case "openFolder":
                        if (root.TryGetProperty("folderPath", out var folderPathElement))
                        {
                            var folderPath = folderPathElement.GetString();
                            if (!string.IsNullOrEmpty(folderPath) && System.IO.Directory.Exists(folderPath))
                            {
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    LogService.LogInfo($"[EventCoordinator] Opening folder from search result: {folderPath}");
                                    
                                    // Windows Explorer'da klasörü aç
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = "explorer.exe",
                                        Arguments = $"\"{folderPath}\"",
                                        UseShellExecute = true
                                    });
                                    
                                    var folderName = System.IO.Path.GetFileName(folderPath);
                                    await TextToSpeechService.SpeakTextAsync($"{folderName} klasörü açılıyor");
                                });
                            }
                            else
                            {
                                _dispatcherQueue.TryEnqueue(async () =>
                                {
                                    LogService.LogInfo($"[EventCoordinator] Folder not found: {folderPath}");
                                    await TextToSpeechService.SpeakTextAsync("Klasör bulunamadı");
                                });
                            }
                        }
                        break;

                    case "voiceChanged":
                        if (root.TryGetProperty("voice", out var voiceElement))
                        {
                            var voice = voiceElement.GetString();
                            if (!string.IsNullOrEmpty(voice))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleVoiceChanged(voice));
                            }
                        }
                        break;

                    case "ttsStarted":
                        _dispatcherQueue.TryEnqueue(() => HandleTtsStarted());
                        break;

                    case "ttsEnded":
                        _dispatcherQueue.TryEnqueue(() => HandleTtsEnded());
                        break;

                    case "openLink":
                        if (root.TryGetProperty("url", out var urlElement))
                        {
                            var url = urlElement.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleOpenLink(url));
                            }
                        }
                        break;

                    case "openNewsDetail":
                        if (root.TryGetProperty("index", out var indexElement))
                        {
                            var index = indexElement.GetInt32();
                            _dispatcherQueue.TryEnqueue(() => HandleOpenNewsDetail(index));
                        }
                        break;

                    case "openEmail":
                        if (root.TryGetProperty("entryId", out var entryIdElement))
                        {
                            var entryId = entryIdElement.GetString();
                            if (!string.IsNullOrEmpty(entryId))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleOpenEmail(entryId));
                            }
                        }
                        break;

                    case "webSpeechResult":
                        if (root.TryGetProperty("text", out var speechTextElement) && 
                            root.TryGetProperty("isFinal", out var isFinalElement))
                        {
                            var speechText = speechTextElement.GetString();
                            var isFinal = isFinalElement.GetBoolean();
                            if (!string.IsNullOrEmpty(speechText))
                            {
                                _dispatcherQueue.TryEnqueue(() => HandleWebSpeechResult(speechText, isFinal));
                            }
                        }
                        break;

                    default:
                        //($"[EventCoordinator] Unknown WebView action: {action}");
                        break;
                }
            }, "OnWebViewMessageReceived");
        }

        /// <summary>
        /// Handles textarea position changes for coordinate system
        /// </summary>
        private void OnTextareaPositionChanged(object? sender, TextareaPositionEventArgs e)
        {
            ErrorHandler.SafeExecute(() =>
            {
                if (e == null) return;
                
                // Debug.WriteLine($"[EventCoordinator] Textarea position changed: L={e.Left}, T={e.Top}, W={e.Width}, H={e.Height}");
                
                // Notify other components that might need coordinate information
                // This could be expanded to notify window management, etc.
            }, "OnTextareaPositionChanged");
        }

        #endregion

        #region Dictation Events

        // OnSpeechRecognizedRoute metodu kaldırıldı - artık ModeManager üzerinden işleniyor

        /// <summary>
        /// Handles dictation manager state changes
        /// </summary>
        private void OnDictationStateChanged(object? sender, DictationStateChangedEventArgs e)
        {
            // Use Task.Run to handle async work safely in event handler
            _ = Task.Run(async () =>
            {
                await ErrorHandler.SafeExecuteAsync(async () =>
                {
                    if (e == null) return;
                    
                    //($"[EventCoordinator] Dictation state changed: Active={e.IsActive}, Processing={e.IsProcessing}, Restarting={e.IsRestarting}");
                    
                    // Update UI based on dictation state
                    if (e.IsRestarting)
                    {
                        await _uiManager.ShowInfoMessageAsync("Ses tanıma yeniden başlatılıyor...");
                    }
                    // Ses tanıma aktif mesajını kaldırdık - gereksiz tekrar
                }, "OnDictationStateChanged");
            });
        }

        #endregion

        #region WebView Action Handlers

        /// <summary>
        /// Handles start dictation action - artık toggle kullanılıyor
        /// </summary>
        private void HandleStartDictation()
        {
            ErrorHandler.SafeExecute(() =>
            {
                //("[EventCoordinator] Dictation toggle requested");
                
                // Önce TTS'i durdur
                TextToSpeechService.StopSpeaking();
                //("[EventCoordinator] TTS durduruldu, dikte toggle yapılıyor");
                
                // Use Task.Run to handle async toggle safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        bool newState = await _dictationManager.ToggleDictation();
                        //($"[EventCoordinator] Dictation toggle sonucu: {(newState ? "Aktif" : "Pasif")}");
                    }, "HandleStartDictation_Toggle");
                });
            }, "HandleStartDictation");
        }

        /// <summary>
        /// Handles stop dictation action - artık toggle kullanılıyor
        /// </summary>
        private void HandleStopDictation()
        {
            ErrorHandler.SafeExecute(() =>
            {
                //("[EventCoordinator] Stop dictation requested - toggle kullanılıyor");
                
                // Use Task.Run to handle async toggle safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        bool newState = await _dictationManager.ToggleDictation();
                        //($"[EventCoordinator] Dictation stop toggle sonucu: {(newState ? "Aktif" : "Pasif")}");
                        
                        // UI'ya dikte durumunu bildir
                        _webViewManager.UpdateDictationState(newState);
                    }, "HandleStopDictation_Toggle");
                });
            }, "HandleStopDictation");
        }

        /// <summary>
        /// Handles speak text action
        /// </summary>
        private void HandleSpeakText(string text)
        {
            ErrorHandler.SafeExecute(() =>
            {
                //($"[EventCoordinator] Speak text requested: {text}");
                // Use Task.Run to handle async command processing safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        await _commandProcessor.ProcessCommandAsync($"söyle {text}");
                    }, "HandleSpeakText_ProcessCommand");
                });
            }, "HandleSpeakText");
        }

        /// <summary>
        /// Handles execute command action
        /// </summary>
        private void HandleExecuteCommand(string command)
        {
            ErrorHandler.SafeExecute(() =>
            {
                //($"[EventCoordinator] Execute command requested: {command}");
                // Use Task.Run to handle async command processing safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        await _commandProcessor.ProcessCommandAsync(command);
                    }, "HandleExecuteCommand_ProcessCommand");
                });
            }, "HandleExecuteCommand");
        }

        /// <summary>
        /// Handles widget data request
        /// </summary>
        private void HandleWidgetRequest(string widgetType)
        {
            ErrorHandler.SafeExecute(() =>
            {
                // Use Task.Run to handle async widget data processing safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        await HandleWidgetDataRequest(widgetType);
                    }, "HandleWidgetRequest_ProcessWidget");
                });
            }, "HandleWidgetRequest");
        }
        
        /// <summary>
        /// Handles widget click events
        /// </summary>
        private void HandleWidgetClick(string widgetType)
        {
            ErrorHandler.SafeExecute(() =>
            {
                LogService.LogInfo($"[EventCoordinator] Widget clicked: {widgetType}");

                // Mevcut modu sakla - widget tıklaması modu değiştirmemeli
                var currentMode = AppState.CurrentMode;
                LogService.LogInfo($"[EventCoordinator] Current mode before widget click: {currentMode}");

                string commandText = widgetType.ToLowerInvariant() switch
                {
                    "meetings" => "bugünkü toplantıları oku",
                    "mails" => "okunmamış maillerimi oku",
                    _ => null
                };

                if (!string.IsNullOrEmpty(commandText))
                {
                    LogService.LogInfo($"[EventCoordinator] Executing command for widget: {commandText}");

                    // Komutu çalıştır
                    _ = Task.Run(async () =>
                    {
                        await ErrorHandler.SafeExecuteAsync(async () =>
                        {
                            // Önce UI feedback ver
                            await _uiManager.ShowInfoMessageAsync($"🔊 {(widgetType == "meetings" ? "Toplantılar" : "Mailler")} okunuyor...");

                            // Komutu çalıştır
                            await _commandProcessor.ProcessCommandAsync(commandText);

                            // Mod değişmişse geri yükle
                            if (AppState.CurrentMode != currentMode)
                            {
                                LogService.LogInfo($"[EventCoordinator] Mode changed during widget command ({AppState.CurrentMode} -> {currentMode}), NOT restoring to prevent conflicts");
                                // NOT: Modu otomatik geri yükleme - bu CommandProcessor'ın kendi logic'ine müdahale edebilir
                                // Widget komutları modu değiştirmemeli ama kullanıcı sesli "mod değiştir" diyebilir
                            }
                        }, "HandleWidgetClick_ProcessCommand");
                    });
                }
            }, "HandleWidgetClick");
        }

        /// <summary>
        /// Processes widget data request and sends response
        /// </summary>
        private async Task HandleWidgetDataRequest(string widgetType)
        {
            switch (widgetType.ToLowerInvariant())
            {
                case "weather":
                    await HandleWeatherWidget();
                    break;
                case "mails":
                    await HandleEmailsWidget();
                    break;
                case "meetings":
                    await HandleMeetingsWidget();
                    break;
                case "news":
                    await HandleNewsWidget();
                    break;
                default:
                    LogService.LogDebug($"[EventCoordinator] Unknown widget type: {widgetType}");
                    break;
            }
        }

        /// <summary>
        /// Handles weather widget data request
        /// </summary>
        private async Task HandleWeatherWidget()
        {
            try
            {
                var weatherService = new Services.WebServices.WeatherService();
                var weatherData = await weatherService.GetWeatherAsync();
                
                var widgetData = new
                {
                    temperature = weatherData.Temperature,
                    description = weatherData.Description,
                    icon = Services.WebServices.WeatherService.GetWeatherEmoji(weatherData.IconCode),
                    lastUpdated = weatherData.LastUpdated
                };
                
                _webViewManager.SendWidgetUpdate("weather", widgetData);
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[EventCoordinator] Weather widget error: {ex.Message}");
                LogService.LogDebug($"[EventCoordinator] Weather widget stack trace: {ex.StackTrace}");
                
                // Hata durumunda bile widget'ı güncelle
                var errorData = new
                {
                    temperature = "--",
                    description = "Veri alınamadı",
                    icon = "❓",
                    lastUpdated = DateTime.Now
                };
                
                _webViewManager.SendWidgetUpdate("weather", errorData);
            }
        }

        /// <summary>
        /// Handles emails widget data request
        /// </summary>
        private async Task HandleEmailsWidget()
        {
            try
            {
                var realOutlookReader = new Services.RealOutlookReader();
                bool connected = await realOutlookReader.ConnectAsync();
                
                if (connected)
                {
                    // TTS'siz sadece mail sayısını al
                    var unreadCount = await realOutlookReader.GetUnreadCountOnlyAsync();
                    var widgetData = new
                    {
                        count = unreadCount,
                        lastUpdated = DateTime.Now
                    };
                    
                    _webViewManager.SendWidgetUpdate("mails", widgetData);
                    realOutlookReader.Disconnect();
                }
                else
                {
                    _webViewManager.SendWidgetUpdate("mails", new { count = 0, lastUpdated = DateTime.Now });
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[EventCoordinator] Emails widget error: {ex.Message}");
                _webViewManager.SendWidgetUpdate("mails", new { count = 0, lastUpdated = DateTime.Now });
            }
        }

        /// <summary>
        /// Handles meetings widget data request
        /// </summary>
        private async Task HandleMeetingsWidget()
        {
            try
            {
                LogService.LogInfo("[EventCoordinator] HandleMeetingsWidget başladı");
                
                var realOutlookReader = new Services.RealOutlookReader();
                bool connected = await realOutlookReader.ConnectAsync();
                
                LogService.LogInfo($"[EventCoordinator] HandleMeetingsWidget - Outlook bağlantı durumu: {connected}");
                
                if (connected)
                {
                    // TTS'siz sadece toplantı sayısını al
                    var meetingCount = await realOutlookReader.GetTodayMeetingCountOnlyAsync();
                    
                    LogService.LogInfo($"[EventCoordinator] HandleMeetingsWidget - Toplantı sayısı: {meetingCount}");
                    
                    var widgetData = new
                    {
                        count = meetingCount,
                        lastUpdated = DateTime.Now
                    };
                    
                    // UI thread'de çalıştığından emin olmak için
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _webViewManager.SendWidgetUpdate("meetings", widgetData);
                    });
                    
                    realOutlookReader.Disconnect();
                    
                    LogService.LogInfo("[EventCoordinator] HandleMeetingsWidget - Widget güncellendi");
                }
                else
                {
                    LogService.LogInfo("[EventCoordinator] HandleMeetingsWidget - Outlook bağlantısı başarısız");
                    
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _webViewManager.SendWidgetUpdate("meetings", new { count = 0, lastUpdated = DateTime.Now });
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[EventCoordinator] Meetings widget error: {ex.Message}");
                LogService.LogDebug($"[EventCoordinator] Meetings widget stack trace: {ex.StackTrace}");
                
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _webViewManager.SendWidgetUpdate("meetings", new { count = 0, lastUpdated = DateTime.Now });
                });
            }
        }

        /// <summary>
        /// Handles news widget data request
        /// </summary>
        private async Task HandleNewsWidget()
        {
            try
            {
                // Önce NewsMemoryService'te veri var mı kontrol et (sadece son 10 haber)
                var newsItems = Services.NewsMemoryService.GetNewsCount() > 0 ? 
                    Services.NewsMemoryService.GetAllNews().Take(10).ToList() : 
                    new List<QuadroAIPilot.Models.Web.RSSItem>();
                
                // Eğer veri yoksa, haber komutunu sessizce çalıştır (UI'da görünmeyecek)
                if (newsItems.Count == 0)
                {
                    LogService.LogInfo("[EventCoordinator] NewsMemoryService'te haber yok, sessizce haber çekiliyor...");
                    
                    try
                    {
                        // WebInfoCommand'ı direkt çağır (UI'da görünmeyecek)
                        var webInfoCommand = new Commands.WebInfoCommand();
                        var context = new QuadroAIPilot.Models.CommandContext
                        {
                            RawCommand = "son haberler"
                        };
                        var result = await webInfoCommand.ExecuteAsync(context);
                        
                        // Sonucu kontrol et
                        if (result.IsSuccess)
                        {
                            LogService.LogInfo("[EventCoordinator] Haberler başarıyla yüklendi");
                            
                            // Haberler yüklendikten sonra tekrar al - daha fazla deneme (sadece son 10 haber)
                            for (int i = 0; i < 3; i++)
                            {
                                await Task.Delay(200); // Kısa bekleme
                                newsItems = Services.NewsMemoryService.GetNewsCount() > 0 ? 
                                    Services.NewsMemoryService.GetAllNews().Take(10).ToList() : 
                                    new List<QuadroAIPilot.Models.Web.RSSItem>();
                                
                                LogService.LogInfo($"[EventCoordinator] Deneme {i + 1}: NewsMemoryService'ten {newsItems.Count} haber alındı");
                                
                                if (newsItems.Count > 0)
                                    break;
                            }
                            
                            if (newsItems.Count == 0)
                            {
                                LogService.LogInfo("[EventCoordinator] Haberler yüklendi ama NewsMemoryService'e ulaşmadı");
                            }
                        }
                        else
                        {
                            LogService.LogInfo($"[EventCoordinator] WebInfoCommand başarısız: {result.Message}");
                            newsItems = GetMockNewsData();
                        }
                    }
                    catch (Exception cmdEx)
                    {
                        LogService.LogInfo($"[EventCoordinator] Haber çekme hatası: {cmdEx.Message}");
                        // Hata durumunda mock data kullan
                        newsItems = GetMockNewsData();
                    }
                }
                
                // Widget veri göndermeden önce kontrol et
                if (newsItems.Count > 0)
                {
                    LogService.LogInfo($"[EventCoordinator] NewsWidget: {newsItems.Count} haber gönderiliyor");
                    var widgetData = new
                    {
                        headlines = newsItems.Select(item => new { 
                            title = item.Title, 
                            link = item.Link 
                        }).ToList(),
                        lastUpdated = DateTime.Now
                    };
                    
                    _webViewManager.SendWidgetUpdate("news", widgetData);
                }
                else
                {
                    LogService.LogWarning("[EventCoordinator] NewsWidget: Hiç haber bulunamadı");
                    // Boş durumda bile widget'ı güncelle
                    _webViewManager.SendWidgetUpdate("news", new { headlines = new List<object>(), lastUpdated = DateTime.Now });
                }
            }
            catch (Exception ex)
            {
                LogService.LogInfo($"[EventCoordinator] News widget error: {ex.Message}");
                // Hata durumunda boş liste gönder
                _webViewManager.SendWidgetUpdate("news", new { headlines = new List<object>(), lastUpdated = DateTime.Now });
            }
        }

        /// <summary>
        /// Mock haber verisi döndür
        /// </summary>
        private List<QuadroAIPilot.Models.Web.RSSItem> GetMockNewsData()
        {
            return new List<QuadroAIPilot.Models.Web.RSSItem>
            {
                new QuadroAIPilot.Models.Web.RSSItem 
                { 
                    Title = "Teknoloji haberleri yükleniyor...", 
                    Link = "#",
                    PublishDate = DateTime.Now
                }
            };
        }

        /// <summary>
        /// Handles text changed action
        /// </summary>
        private void HandleTextChanged(string text, bool fromSendButton = false)
        {
            ErrorHandler.SafeExecute(() =>
            {
                // Gönder butonundan geldiyse manuel yazı modunu resetle
                if (fromSendButton)
                {
                    LogService.LogInfo($"[EventCoordinator] HandleTextChanged - Gönder butonundan geldi, manuel yazı modu kapatılıyor");
                    DictationManager.OnManualTypingEnded();
                }

                // Yazı modunda textChanged event'lerini işleme
                if (State.AppState.CurrentMode == State.AppState.UserMode.Writing)
                {
                    LogService.LogDebug($"[EventCoordinator] HandleTextChanged - Writing mode, ignoring textChanged event: '{text}'");
                    return;
                }

                LogService.LogInfo($"[ProcessTextChanged] [EventCoordinator] HandleTextChanged - Metin işleniyor: '{text}', fromSendButton: {fromSendButton}");
                _dictationManager.HandleTextChanged(text);
            }, "HandleTextChanged");
        }

        /// <summary>
        /// Handles stop TTS action
        /// </summary>
        private async void HandleStopTts()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                //("[EventCoordinator] Stop TTS requested");
                TextToSpeechService.StopSpeaking();
                _uiManager.AppendFeedback("🔇 TTS durduruldu");
            }, "HandleStopTts");
        }

        /// <summary>
        /// Handles TTS started notification from WebView
        /// </summary>
        private void HandleTtsStarted()
        {
            ErrorHandler.SafeExecute(() =>
            {
                //("[EventCoordinator] TTS started (from WebView)");
                
                // TTS başladı
                
                // YENI STRATEJI: Win+H'ı KAPATMA!
                // DictationManager zaten smart filtering kullanıyor
                //("[EventCoordinator] Win+H remains active with smart filtering");
            }, "HandleTtsStarted");
        }

        /// <summary>
        /// Handles TTS ended notification from WebView
        /// </summary>
        private void HandleTtsEnded()
        {
            ErrorHandler.SafeExecute(() =>
            {
                //("[EventCoordinator] TTS ended (from WebView)");
                
                // TTS bitti
                
                // TTS bittiğinde otomatik dikte başlatma YAPMA
                // Kullanıcı tekrar konuş butonuna basana kadar bekle
                
                // WebView'a dikte durumunu güncelle (pasif)
                _webViewManager.UpdateDictationState(_dictationManager.IsActive);
            }, "HandleTtsEnded");
        }

        /// <summary>
        /// Handles voice selection change
        /// </summary>
        private void HandleVoiceChanged(string voice)
        {
            ErrorHandler.SafeExecute(() =>
            {
                //($"[EventCoordinator] Voice changed to: {voice}");
                
                // Voice değerine göre TextToSpeechService'i güncelle
                string voiceName = "Emel"; // Varsayılan
                
                switch (voice)
                {
                    case "automatic":
                        TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                        voiceName = "Emel";
                        //("[EventCoordinator] Otomatik ses seçimi aktif");
                        break;
                        
                    case "edge-emel":
                        TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                        voiceName = "Emel";
                        //("[EventCoordinator] Edge Emel sesi seçildi");
                        break;
                        
                    case "edge-ahmet":
                        TextToSpeechService.CurrentEdgeVoice = "tr-TR-AhmetNeural";
                        voiceName = "Ahmet";
                        //("[EventCoordinator] Edge Ahmet sesi seçildi");
                        break;
                        
                    case "windows-tolga":
                        TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural"; // Tolga artık sadece fallback
                        voiceName = "Tolga";
                        //("[EventCoordinator] Windows Tolga sesi seçildi");
                        break;
                        
                    default:
                        //($"[EventCoordinator] Bilinmeyen ses tipi: {voice}");
                        break;
                }
                
                // JavaScript'e ses değişikliğini bildir
                _webViewManager?.SendMessage(new
                {
                    action = "updateVoiceSetting",
                    voiceName = voiceName
                });
                
                LogService.LogDebug($"[EventCoordinator] JavaScript'e ses değişikliği gönderildi: {voiceName}");
            }, "HandleVoiceChanged");
        }

        /// <summary>
        /// Handles open link action from WebView
        /// </summary>
        private void HandleOpenLink(string url)
        {
            ErrorHandler.SafeExecute(() =>
            {
                //($"[EventCoordinator] Open link requested: {url}");
                
                try
                {
                    // URL'yi varsayılan tarayıcıda aç
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    
                    //($"[EventCoordinator] Successfully opened URL: {url}");
                }
                catch (Exception)
                {
                    //($"[EventCoordinator] Error opening URL");
                    _uiManager.AppendFeedback($"Link açılırken hata oluştu");
                }
            }, "HandleOpenLink");
        }

        /// <summary>
        /// Handles open news detail by index
        /// </summary>
        private void HandleOpenNewsDetail(int index)
        {
            ErrorHandler.SafeExecute(() =>
            {
                //($"[EventCoordinator] Open news detail requested for index: {index}");
                
                // NewsMemoryService'den haberi al
                var newsItem = Services.NewsMemoryService.GetNewsByIndex(index);
                if (newsItem != null && !string.IsNullOrEmpty(newsItem.Link))
                {
                    try
                    {
                        // URL'yi varsayılan tarayıcıda aç
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = newsItem.Link,
                            UseShellExecute = true
                        });
                        
                        //($"[EventCoordinator] Successfully opened news: {newsItem.Title}");
                        _uiManager.AppendFeedback($"📰 '{newsItem.Title}' haberi tarayıcıda açıldı");
                    }
                    catch (Exception)
                    {
                        //($"[EventCoordinator] Error opening news");
                        _uiManager.AppendFeedback($"Haber açılırken hata oluştu");
                    }
                }
                else
                {
                    //($"[EventCoordinator] News not found for index: {index}");
                    _uiManager.AppendFeedback($"{index} numaralı haber bulunamadı");
                }
            }, "HandleOpenNewsDetail");
        }

        /// <summary>
        /// Handles open email by EntryID
        /// </summary>
        private void HandleOpenEmail(string entryId)
        {
            ErrorHandler.SafeExecute(() =>
            {
                // Log.Information($"[EventCoordinator] Open email requested for EntryID: {entryId}");
                
                try
                {
                    // Outlook'u aç ve e-postayı göster
                    Type outlookType = Type.GetTypeFromProgID("Outlook.Application");
                    if (outlookType != null)
                    {
                        dynamic outlookApp = Activator.CreateInstance(outlookType);
                        if (outlookApp != null)
                        {
                            dynamic nameSpace = outlookApp.GetNamespace("MAPI");
                            if (nameSpace != null)
                            {
                                try
                                {
                                    // EntryID ile mail'i bul ve göster
                                    dynamic mailItem = nameSpace.GetItemFromID(entryId);
                                    if (mailItem != null)
                                    {
                                        mailItem.Display();
                                        _uiManager.AppendFeedback($"📧 E-posta Outlook'ta açıldı");
                                    }
                                }
                                catch (Exception)
                                {
                                    // Log.Error($"[EventCoordinator] Error opening email: {ex.Message}");
                                    _uiManager.AppendFeedback($"E-posta açılırken hata oluştu");
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Log.Error($"[EventCoordinator] Error connecting to Outlook: {ex.Message}");
                    _uiManager.AppendFeedback($"Outlook bağlantısı kurulamadı");
                }
            }, "HandleOpenEmail");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Processes a voice command and returns success status
        /// </summary>
        public async Task<bool> ProcessVoiceCommand(string command)
        {
            try
            {
                //($"[EventCoordinator] Processing voice command: {command}");
                
                _uiManager.SetProcessingState(true);
                
                // Process the command
                var result = await _commandProcessor.ProcessCommandAsync(command);
                
                _uiManager.SetProcessingState(false);
                
                // Return true if command was processed successfully
                return result;
            }
            catch (Exception)
            {
                //($"[EventCoordinator] Error processing voice command: {ex.Message}");
                _uiManager.SetProcessingState(false);
                return false;
            }
        }

        /// <summary>
        /// Handles Web Speech API result from JavaScript
        /// </summary>
        private void HandleWebSpeechResult(string text, bool isFinal)
        {
            ErrorHandler.SafeExecute(() =>
            {
                LogService.LogDebug($"[EventCoordinator] Web Speech Result received - Text: '{text}', Final: {isFinal}");
                
                if (!isFinal || string.IsNullOrWhiteSpace(text))
                {
                    return;
                }
                
                // Use Task.Run to handle async work safely
                _ = Task.Run(async () =>
                {
                    await ErrorHandler.SafeExecuteAsync(async () =>
                    {
                        // WebSpeechBridge'e gönder
                        var webSpeechBridge = DictationManager.GetWebSpeechBridge();
                        if (webSpeechBridge != null)
                        {
                            await webSpeechBridge.HandleWebSpeechResult(text, isFinal);
                        }
                        else
                        {
                            LogService.LogDebug("[EventCoordinator] WebSpeechBridge not initialized yet!");
                        }
                    }, "HandleWebSpeechResult_Process");
                });
            }, "HandleWebSpeechResult");
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EventCoordinator()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Managed resources cleanup
                        DetachEvents();

                        // Clear any pending tasks
                        System.Diagnostics.Debug.WriteLine("[EventCoordinator] Disposed - all events detached");
                    }

                    // Unmanaged resources cleanup (if any)

                    _disposed = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EventCoordinator] Dispose error: {ex.Message}");
                }
            }
        }

        #endregion
    }
}