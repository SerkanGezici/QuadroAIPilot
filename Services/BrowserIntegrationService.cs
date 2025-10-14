using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Interfaces;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;

namespace QuadroAIPilot.Services
{
    public interface IBrowserIntegrationService
    {
        Task StartAsync();
        Task StopAsync();
        bool IsRunning { get; }
        event EventHandler<string> TextReadRequested;
    }

    public class BrowserIntegrationService : IBrowserIntegrationService
    {
        private readonly ILogger<BrowserIntegrationService> _logger;
        private readonly IWindowsApiService _windowsApiService;
        private readonly IGoogleTranslateService _translateService;
        
        private HttpListener _httpListener;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private string _lastClipboardContent = string.Empty;
        private readonly SemaphoreSlim _translationSemaphore = new(1, 1);

        private const string HTTP_PREFIX = "http://127.0.0.1:19741/";
        
        public bool IsRunning => _isRunning;
        public event EventHandler<string> TextReadRequested;

        public BrowserIntegrationService(
            ILogger<BrowserIntegrationService> logger,
            IWindowsApiService windowsApiService,
            IGoogleTranslateService translateService)
        {
            _logger = logger;
            _windowsApiService = windowsApiService;
            _translateService = translateService;
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Browser integration service is already running");
                return;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;
                
                // HTTP Listener başlat (sadece trigger için)
                StartHttpListener();

                _logger.LogInformation("Browser integration service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start browser integration service");
                _isRunning = false;
                throw;
            }
        }


        private void StartHttpListener()
        {
            Task.Run(async () =>
            {
                try
                {
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add(HTTP_PREFIX);
                    _httpListener.Start();

                    _logger.LogInformation($"HTTP listener started on {HTTP_PREFIX}");

                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var context = await _httpListener.GetContextAsync();
                            _ = Task.Run(() => HandleHttpRequest(context));
                        }
                        catch (HttpListenerException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HTTP listener error");
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                // CORS headers
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                if (context.Request.Url.AbsolutePath == "/trigger-read")
                {
                    _logger.LogInformation($"Received request: {context.Request.HttpMethod} {context.Request.Url.AbsolutePath}");
                    
                    if (context.Request.HttpMethod == "POST")
                    {
                        // Clipboard'dan metni al ve işle
                        await ProcessClipboardText();
                    }
                    else
                    {
                        _logger.LogWarning($"Received {context.Request.HttpMethod} request, expecting POST");
                    }
                    
                    context.Response.StatusCode = 200;
                    var response = Encoding.UTF8.GetBytes("{\"success\":true}");
                    await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }

                context.Response.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling HTTP request");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task ProcessClipboardText()
        {
            try
            {
                _logger.LogInformation("ProcessClipboardText started");
                
                string clipboardText = null;
                var tcs = new TaskCompletionSource<string>();

                var window = (Application.Current as App)?.MainWindow as MainWindow;
                if (window != null)
                {
                    window.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var dataPackageView = Clipboard.GetContent();
                            if (dataPackageView.Contains(StandardDataFormats.Text))
                            {
                                var textTask = dataPackageView.GetTextAsync();
                                clipboardText = textTask.GetAwaiter().GetResult();
                                _logger.LogInformation($"Clipboard text retrieved: {clipboardText?.Length ?? 0} characters");
                            }
                            else
                            {
                                _logger.LogWarning("Clipboard doesn't contain text format");
                            }
                            tcs.SetResult(clipboardText);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reading clipboard");
                            tcs.SetException(ex);
                        }
                    });
                    
                    clipboardText = await tcs.Task;
                }
                else
                {
                    _logger.LogError("MainWindow is null");
                    return;
                }

                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    _logger.LogWarning("Clipboard is empty or doesn't contain text");
                    return;
                }

                _logger.LogInformation($"Processing clipboard text: {clipboardText.Length} characters");
                _logger.LogInformation($"First 50 chars: {clipboardText.Substring(0, Math.Min(50, clipboardText.Length))}...");

                // Metni çevir ve oku
                await TranslateAndReadText(clipboardText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing clipboard text");
            }
        }

        private async Task TranslateAndReadText(string text)
        {
            await _translationSemaphore.WaitAsync();
            try
            {
                // Önce TTS'i durdur (yeni metin geldi)
                await TextToSpeechService.StopSpeakingAsync();

                // Metni temizle
                var cleanedText = CleanText(text);

                // Google Translate ile çeviri yap
                var translatedText = await TranslateWithGoogle(cleanedText);

                // Çevrilmiş metni seslendir
                if (!string.IsNullOrWhiteSpace(translatedText))
                {
                    TextReadRequested?.Invoke(this, translatedText);
                    
                    // Çeviri sonucunu textarea'ya yazdır
                    var app = Microsoft.UI.Xaml.Application.Current as App;
                    var mainWindow = app?.MainWindow as MainWindow;
                    var webViewManager = mainWindow?.WebViewManager;
                    
                    if (webViewManager != null)
                    {
                        // Önce textarea'yı temizle
                        await webViewManager.ClearContent();
                        // Çeviri sonucunu yazdır
                        await webViewManager.AppendOutput(translatedText);
                    }
                    
                    await TextToSpeechService.SpeakTextAsync(translatedText);
                }
            }
            finally
            {
                _translationSemaphore.Release();
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Gereksiz whitespace'leri temizle
            text = Regex.Replace(text, @"\s+", " ");
            
            // Sayfa numaralarını kaldır (Page 1/5, Sayfa 1/5 vb.)
            text = Regex.Replace(text, @"(Page|Sayfa|Seite)\s+\d+\s*/\s*\d+", "", RegexOptions.IgnoreCase);
            
            // Click here, Read more gibi gereksiz link metinlerini kaldır
            text = Regex.Replace(text, @"\b(click here|read more|here|devamını oku|tıklayın|buraya tıklayın)\b", "", RegexOptions.IgnoreCase);
            
            // URL'leri kaldır
            text = Regex.Replace(text, @"https?://[^\s]+", "");
            
            // E-posta adreslerini kaldır
            text = Regex.Replace(text, @"\S+@\S+\.\S+", "");
            
            // Birden fazla noktalama işaretini düzelt
            text = Regex.Replace(text, @"\.{2,}", ".");
            text = Regex.Replace(text, @"\!{2,}", "!");
            text = Regex.Replace(text, @"\?{2,}", "?");
            
            // Başta ve sondaki boşlukları temizle
            text = text.Trim();

            return text;
        }

        private async Task<string> TranslateWithGoogle(string text)
        {
            try
            {
                _logger.LogInformation($"TranslateWithGoogle started with text length: {text.Length}");
                
                // Google Translate API'yi kullan
                var translatedText = await _translateService.TranslateAsync(text, "tr", "auto");
                
                if (!string.IsNullOrEmpty(translatedText) && translatedText != text)
                {
                    _logger.LogInformation($"Translation completed: {text.Length} -> {translatedText.Length} characters");
                    return translatedText;
                }
                else
                {
                    _logger.LogWarning("Translation returned empty or same text");
                    return text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Translation failed");
                return text; // Hata durumunda orijinal metni döndür
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                
                _httpListener?.Stop();
                _httpListener?.Close();

                _isRunning = false;
                _logger.LogInformation("Browser integration service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping browser integration service");
            }
        }
    }
}