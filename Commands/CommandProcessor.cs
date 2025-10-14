﻿// CommandProcessor.cs – ses/volume komutları erken yakalama (2025-05-06)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuadroAIPilot.Services;
using QuadroAIPilot.Services.AI;
using QuadroAIPilot.Models.AI;
using QuadroAIPilot.Models;
using QuadroAIPilot.State;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Infrastructure;
using Microsoft.Extensions.Logging;

namespace QuadroAIPilot.Commands
{
    public class CommandProcessor : ICommandProcessor
    {
        private readonly CommandExecutor _executor;
        private readonly ApplicationService _applicationService;
        private readonly FileSearchService _fileSearchService;
        private readonly ApplicationRegistry _appRegistry;
        private readonly WindowsApiService _windowsApiService;
        private readonly ILogger<CommandProcessor> _logger;
        private readonly LocalIntentDetector _intentDetector;
        private IWebViewManager _webViewManager;

        public event EventHandler<CommandProcessResult>? CommandProcessed;

        public CommandProcessor(
            CommandExecutor ex,
            ApplicationService appService,
            FileSearchService fileSearchService,
            LocalIntentDetector intentDetector = null,
            IWebViewManager webViewManager = null)
        {
            _executor = ex ?? throw new ArgumentNullException(nameof(ex));
            _applicationService = appService ?? throw new ArgumentNullException(nameof(appService));
            _fileSearchService = fileSearchService ?? throw new ArgumentNullException(nameof(fileSearchService));
            _appRegistry = ApplicationRegistry.Instance;
            _windowsApiService = new WindowsApiService();
            _logger = LoggingService.CreateLogger<CommandProcessor>();
            _webViewManager = webViewManager; // Opsiyonel olarak al
            
            // AI detector opsiyonel, yoksa oluştur
            if (intentDetector == null)
            {
                var learningService = new UserLearningService(LoggingService.CreateLogger<UserLearningService>());
                _intentDetector = new LocalIntentDetector(learningService, LoggingService.CreateLogger<LocalIntentDetector>());
            }
            else
            {
                _intentDetector = intentDetector;
            }
        }
        
        /// <summary>
        /// Sets the WebViewManager instance (for late binding)
        /// </summary>
        public void SetWebViewManager(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager;
            _logger.LogInformation("WebViewManager set in CommandProcessor");
        }

        /*--------------------------------------------------
         *  Statik tanımlar
         *-------------------------------------------------*/
        private static readonly string[] _launchVerbs =
            { "aç", "başlat", "çalıştır", "getir" };        private static readonly string[] _closeExpressions =
            { "uygulamayı kapat", "pencereyi kapat", "sonlandır" };

        private static readonly string[] _knownTypes =
        {
            "excel","word","powerpoint","sunum",
            "pdf","metin",
            "fotoğraf","resim","görsel",
            "video","müzik","ses",
            "zip","sıkıştırılmış"
        };        /*--------------------------------------------------
         *  Ana işleyici
         *-------------------------------------------------*/
        public async Task<bool> ProcessCommandAsync(string raw)
        {
            Debug.WriteLine($"[CommandProcessor] *** KOMUT BAŞLADI *** Raw: '{raw}'");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    _logger.LogWarning("Boş komut girişi alındı");
                    return false;
                }
                
                _logger.LogInformation("Komut işleme başlatıldı: {Command}", raw);
                
                // Tırnak işaretlerini temizle
                char[] trimChars = { '"', '\'', '\u201C', '\u201D', '\u2018', '\u2019' }; // Normal ve fancy tırnaklar
                raw = raw.TrimEnd(trimChars);
                Debug.WriteLine($"[CommandProcessor] Tırnak temizleme sonrası: '{raw}'");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Komut işleme başlangıcında hata: {Command}", raw);
                GlobalExceptionHandler.HandleException(ex, "CommandProcessor.ProcessCommandAsync.Start");
                LoggingService.LogCommandExecution(raw ?? "null", false, stopwatch.ElapsedMilliseconds, ex.Message);
                return false;
            }
            
            // AI ile intent detection
            IntentResult aiResult = null;
            string txt;
            try
            {
                aiResult = await _intentDetector.DetectIntentAsync(raw);
                if (aiResult != null && aiResult.Intent != null)
                {
                    _logger.LogInformation("AI Intent Detection: {Intent} ({Confidence:P})", 
                        aiResult.Intent.Name, aiResult.Confidence);
                }
                
                // DEBUG: AI sonuçlarını detaylı logla
                if (aiResult != null)
                {
                    _logger.LogInformation("[CommandProcessor] AI Original: '{OriginalText}'", aiResult.OriginalText);
                    _logger.LogInformation("[CommandProcessor] AI Processed: '{ProcessedText}'", aiResult.ProcessedText);
                    if (aiResult.Intent != null)
                    {
                        _logger.LogInformation("[CommandProcessor] AI Intent: {Intent} ({Confidence})", 
                            aiResult.Intent.Name, aiResult.Confidence);
                    }
                }
                
                // AI güvenli bir sonuç döndürdüyse, işlenmiş metni kullan
                if (aiResult != null && aiResult.IsSuccessful && !string.IsNullOrEmpty(aiResult.ProcessedText))
                {
                    txt = aiResult.ProcessedText.ToLowerInvariant();
                    _logger.LogInformation("AI işlenmiş komut kullanılıyor: {ProcessedCommand}", txt);
                    Debug.WriteLine($"[CommandProcessor] Using AI processed text: '{txt}'");
                }
                else
                {
                    // AI başarısız oldu, normal devam et
                    txt = raw.Trim().ToLowerInvariant();
                    Debug.WriteLine($"[CommandProcessor] AI failed, using original: '{txt}'");
                }
            }
            catch (Exception aiEx)
            {
                _logger.LogWarning(aiEx, "AI intent detection hatası, normal işleme devam ediliyor");
                txt = raw.Trim().ToLowerInvariant();
            }
            
            Debug.WriteLine($"[CommandProcessor] *** TXT DEĞERİ *** txt: '{txt}', uzunluk: {txt?.Length}");
            
            // Edge sesleri yükleme komutu
            if ((txt.Contains("edge") && txt.Contains("yükle")) || txt.Contains("edge seslerini yükle"))
            {
                // Debug.WriteLine($"[CommandProcessor] Edge sesleri yükleme komutu algılandı");
                var installCommand = new InstallEdgeVoicesCommand(raw);
                var installResult = await installCommand.ExecuteAsync();
                
                CommandProcessed?.Invoke(this, new CommandProcessResult
                {
                    CommandText = raw,
                    Success = installResult,
                    ResultMessage = installResult ? "Edge sesleri yükleme başlatıldı" : "Edge sesleri yükleme başarısız",
                    DetectedIntent = "InstallEdgeVoices"
                });
                
                LoggingService.LogCommandExecution(raw, installResult, 100, "Install Edge Voices");
                return installResult;
            }
            
            // Edge TTS komutu - CommandFactory kullan
            if (txt.Contains("edge") && (txt.Contains("tts") || txt.Contains("ses")))
            {
                // Debug.WriteLine($"[CommandProcessor] Edge TTS komutu algılandı (ERKEN KONTROL) - CommandFactory'ye yönlendiriliyor");
                
                var stopwatchEdge = Stopwatch.StartNew();
                var commandFactory = new CommandFactory(_applicationService, _fileSearchService, _windowsApiService);
                var edgeTtsCommand = commandFactory.CreateCommandFromText(raw);
                
                if (edgeTtsCommand != null)
                {
                    var edgeResult = await edgeTtsCommand.ExecuteAsync();
                    stopwatchEdge.Stop();
                    
                    CommandProcessed?.Invoke(this, new CommandProcessResult
                    {
                        CommandText = raw,
                        Success = edgeResult,
                        ResultMessage = edgeResult ? "Edge TTS komutu başarılı" : "Edge TTS komutu başarısız",
                        DetectedIntent = "EdgeTTS"
                    });
                    
                    LoggingService.LogCommandExecution(raw, edgeResult, stopwatchEdge.ElapsedMilliseconds, "Edge TTS");
                    return edgeResult;
                }
            }
            
            // E-posta/e posta normalizasyonu (sistemi desteklemek için)
            txt = txt.Replace("e-posta", "eposta").Replace("e posta", "eposta");
            
            bool ok = false;
            string det = "Bilinmiyor";
            
            try
            {
                /*----------- 0) COMMAND REGISTRY - EN YÜKSEK ÖNCELİK -----------*/
                // Önce CommandRegistry'yi kontrol et
                var commandMeta = CommandRegistry.Instance.FindCommand(txt);
                if (commandMeta != null)
                {
                    _logger.LogInformation("[CommandProcessor] CommandRegistry'de komut bulundu: {CommandId}", commandMeta.CommandId);
                    
                    // Web/haber komutlarını WebInfoCommand'e yönlendir
                    if (commandMeta.CommandId.Contains("news") || commandMeta.CommandId.Contains("wikipedia") || 
                        commandMeta.CommandId.Contains("twitter") || commandMeta.CommandId == "read_news")
                    {
                        var webInfoCmd = new WebInfoCommand();
                        var context = new CommandContext { RawCommand = raw };
                        var response = await webInfoCmd.ExecuteAsync(context);
                        
                        if (response.IsSuccess)
                        {
                            if (!string.IsNullOrEmpty(response.HtmlContent) && _webViewManager != null)
                            {
                                await _webViewManager.AppendOutput(response.HtmlContent);
                            }
                            
                            // ÇÖZÜM: Duplicate TTS çağrısı kaldırıldı - aşağıda tek seferde yapılacak
                            
                            Raise(raw, true, response.Message ?? "Web komutu başarıyla işlendi", "Web Info Command");
                            return true;
                        }
                        else
                        {
                            Raise(raw, false, response.Message ?? "Web komutu başarısız", "Web Info Command");
                            return false;
                        }
                    }
                    else
                    {
                        // Normal komutları CommandExecutor ile işle
                        _logger.LogInformation("[CommandProcessor] Normal komut bulundu: {CommandName}", commandMeta.CommandName);
                        
                        // CommandRegistry komutlarını sistem komutu olarak işle
                        ok = await _executor.ExecuteIntentAsync(new CommandIntentResult
                        {
                            Type = CommandIntentType.SystemCommand,
                            Command = txt
                        });
                        
                        det = commandMeta.CommandName;
                        
                        if (ok)
                        {
                            Raise(raw, true, "Komut başarılı", det);
                            return true;
                        }
                        else
                        {
                            Raise(raw, false, "Komut başarısız", det);
                            return false;
                        }
                    }
                }
                
                // CommandFactory'yi bir kez oluştur - çoklu çağrı optimizasyonu
                var commandFactory = new CommandFactory(_applicationService, _fileSearchService, _windowsApiService);
                
                /*----------- 1) ÖZEL KOMUTLAR -----------*/
                // Edge TTS kontrolü yukarıda yapıldı
                
                /*----------- 2) WEB INFO KOMUTLARI -----------*/
                // Wikipedia, haberler, Twitter trendleri - öncelikli kontrol
                Debug.WriteLine($"[CommandProcessor] *** 2. BÖLÜM *** Web komut kontrolü başlıyor: '{txt}'");
                var webInfoCommand = new WebInfoCommand();
                Debug.WriteLine($"[CommandProcessor] WebInfoCommand oluşturuldu");
                bool canHandle = webInfoCommand.CanHandle(txt);
                Debug.WriteLine($"[CommandProcessor] webInfoCommand.CanHandle('{txt}') sonucu: {canHandle}");
                
                if (canHandle)
                {
                    Debug.WriteLine($"[CommandProcessor] Web info komutu algılandı: '{txt}'");
                    
                    var webContext = new CommandContext { RawCommand = raw };
                    Debug.WriteLine($"[CommandProcessor] WebInfoCommand.ExecuteAsync çağrılıyor...");
                    
                    var webResult = await webInfoCommand.ExecuteAsync(webContext);
                    
                    Debug.WriteLine($"[CommandProcessor] WebInfoCommand.ExecuteAsync tamamlandı");
                    Debug.WriteLine($"[CommandProcessor] webResult null mu: {webResult == null}");
                    
                    if (webResult.IsSuccess)
                    {
                        ok = true;
                        det = "Web Info Command";
                        
                        // HTML içeriği varsa SADECE WebView'a gönder
                        if (!string.IsNullOrEmpty(webResult.HtmlContent))
                        {
                            if (_webViewManager != null)
                            {
                                await _webViewManager.AppendOutput(webResult.HtmlContent);
                                Debug.WriteLine($"[CommandProcessor] HTML content sent to WebView, length: {webResult.HtmlContent.Length}");
                            }
                            else
                            {
                                Debug.WriteLine("[CommandProcessor] WebViewManager is null! Falling back to text output");
                                // WebViewManager yoksa düz metin olarak göster
                                TextToSpeechService.SendToOutput(webResult.Message ?? "Web içeriği alındı.");
                            }
                        }
                        else if (!string.IsNullOrEmpty(webResult.Message))
                        {
                            // HTML yoksa normal metin çıktısı textarea'ya
                            TextToSpeechService.SendToOutput(webResult.Message);
                        }
                        
                        // ÇÖZÜM: Bu eski WebInfoCommand kısmına TTS ekle
                        Debug.WriteLine($"[CommandProcessor] ESKİ WebInfoCommand - TTS kontrolü başlıyor");
                        Debug.WriteLine($"[CommandProcessor] webResult.VoiceOutput null mu: {webResult.VoiceOutput == null}");
                        Debug.WriteLine($"[CommandProcessor] webResult.VoiceOutput içeriği: '{webResult.VoiceOutput}'");
                        
                        if (!string.IsNullOrEmpty(webResult.VoiceOutput))
                        {
                            Debug.WriteLine($"[CommandProcessor] ESKİ WebInfoCommand - TTS başlatılıyor");
                            Debug.WriteLine($"[CommandProcessor] TTS çıktısı uzunluğu: {webResult.VoiceOutput.Length} karakter");
                            
                            // Aktif TTS varsa durdur
                            if (TextToSpeechService.IsSpeaking())
                            {
                                Debug.WriteLine($"[CommandProcessor] Aktif TTS kesiliyor");
                                TextToSpeechService.StopSpeaking();
                            }
                            
                            await TextToSpeechService.SpeakTextAsync(webResult.VoiceOutput);
                            Debug.WriteLine($"[CommandProcessor] ESKİ WebInfoCommand - TTS tamamlandı");
                        }
                        else
                        {
                            Debug.WriteLine($"[CommandProcessor] ESKİ WebInfoCommand - TTS çıktısı YOK");
                        }
                        
                        Raise(raw, true, "Komut başarılı", det);
                        return true;
                    }
                }
                
                /*----------- 2) MAPI KOMUTLARI -----------*/
                // Mail ve takvim komutları
                var mapiCommand = commandFactory.CreateCommandFromText(txt);
                if (mapiCommand != null && mapiCommand.GetType().Name == "LocalOutlookCommand")
                {
                    // Debug.WriteLine($"[CommandProcessor] MAPI komutu algılandı: {mapiCommand.GetType().Name}");
                    
                    bool mapiResult = await mapiCommand.ExecuteAsync();
                    if (mapiResult)
                    {
                        ok = true;
                        det = $"MAPI Command ({mapiCommand.GetType().Name})";
                        Raise(raw, true, "MAPI komutu başarıyla işlendi", det);
                        return true;
                    }
                    // MAPI başarısız olursa diğer komutları kontrol et
                }

                /*----------- 3) TEST KOMUTLARI ----------*/
                // Web servislerini test etmek için basit komutlar
                if (txt == "test wikipedia")
                {
                    // Debug.WriteLine($"[CommandProcessor] Wikipedia testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "Atatürk nedir?" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        ok = true;
                        det = "Wikipedia Test";
                        Raise(raw, true, "Wikipedia servisi çalışıyor: Atatürk sorgusu başarılı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Wikipedia servisi hata verdi: {testResult.Message}", "Wikipedia Test");
                        return false;
                    }
                }
                
                if (txt == "test haberler")
                {
                    // Debug.WriteLine($"[CommandProcessor] Haber servisi testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "son dakika haberleri" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        // HTML içeriği varsa göster
                        if (!string.IsNullOrEmpty(testResult.HtmlContent) && _webViewManager != null)
                        {
                            await _webViewManager.AppendOutput(testResult.HtmlContent);
                        }
                        
                        // ÇÖZÜM: Duplicate TTS çağrısı kaldırıldı - test komutları TTS'siz
                        
                        ok = true;
                        det = "Haber Test";
                        Raise(raw, true, "Haber servisi çalışıyor: Son dakika haberleri alındı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Haber servisi hata verdi: {testResult.Message}", "Haber Test");
                        return false;
                    }
                }
                
                if (txt == "test twitter")
                {
                    // Debug.WriteLine($"[CommandProcessor] Twitter servisi testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "twitter gündem" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        ok = true;
                        det = "Twitter Test";
                        Raise(raw, true, "Twitter servisi çalışıyor: Gündem konuları alındı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Twitter servisi hata verdi: {testResult.Message}", "Twitter Test");
                        return false;
                    }
                }
                
                if (txt == "test cache")
                {
                    Debug.WriteLine($"[CommandProcessor] Cache servisi testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "yapay zeka nedir?" };
                    
                    // İlk sorgu
                    var sw1 = Stopwatch.StartNew();
                    var testResult1 = await testCommand.ExecuteAsync(testContext);
                    sw1.Stop();
                    
                    if (!testResult1.IsSuccess)
                    {
                        Raise(raw, false, "Cache testi başarısız: İlk sorgu hata verdi", "Cache Test");
                        return false;
                    }
                    
                    // Aynı sorguyu tekrar yap (cache'den gelmeli)
                    var sw2 = Stopwatch.StartNew();
                    var testResult2 = await testCommand.ExecuteAsync(testContext);
                    sw2.Stop();
                    
                    if (testResult2.IsSuccess && sw2.ElapsedMilliseconds < sw1.ElapsedMilliseconds / 2)
                    {
                        ok = true;
                        det = "Cache Test";
                        Raise(raw, true, $"Cache servisi çalışıyor: İlk sorgu {sw1.ElapsedMilliseconds}ms, Cache'den {sw2.ElapsedMilliseconds}ms", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, "Cache servisi beklendiği gibi çalışmıyor", "Cache Test");
                        return false;
                    }
                }
                
                if (txt == "test google trends")
                {
                    Debug.WriteLine($"[CommandProcessor] Google Trends testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "google trends türkiye" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        ok = true;
                        det = "Google Trends Test";
                        Raise(raw, true, "Google Trends servisi çalışıyor: Trend verileri alındı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Google Trends servisi hata verdi: {testResult.Message}", "Google Trends Test");
                        return false;
                    }
                }
                
                if (txt == "test ekşi sözlük")
                {
                    Debug.WriteLine($"[CommandProcessor] Ekşi Sözlük testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "ekşi sözlük gündem" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        ok = true;
                        det = "Ekşi Sözlük Test";
                        Raise(raw, true, "Ekşi Sözlük servisi çalışıyor: Gündem başlıkları alındı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Ekşi Sözlük servisi hata verdi: {testResult.Message}", "Ekşi Sözlük Test");
                        return false;
                    }
                }
                
                if (txt == "test reddit")
                {
                    Debug.WriteLine($"[CommandProcessor] Reddit testi başlatılıyor...");
                    var testCommand = new WebInfoCommand();
                    var testContext = new CommandContext { RawCommand = "reddit turkey trend" };
                    var testResult = await testCommand.ExecuteAsync(testContext);
                    
                    if (testResult.IsSuccess)
                    {
                        ok = true;
                        det = "Reddit Test";
                        Raise(raw, true, "Reddit servisi çalışıyor: r/Turkey trendleri alındı", det);
                        return true;
                    }
                    else
                    {
                        Raise(raw, false, $"Reddit servisi hata verdi: {testResult.Message}", "Reddit Test");
                        return false;
                    }
                }

                /*----------- 4) WEB INFO KOMUTLARI ----------*/
                // Web komutlarını önce kontrol et (haber, wikipedia, twitter gündem vb.)
                var webCommand = commandFactory.CreateCommandFromText(txt);
                if (webCommand != null && webCommand.GetType().Name == "CommandWrapper")
                {
                    Debug.WriteLine($"[CommandProcessor] Web komutu algılandı (CommandWrapper)");
                    
                    // CommandWrapper içindeki WebInfoCommand'i al ve çalıştır
                    if (webCommand is CommandWrapper wrapper)
                    {
                        try
                        {
                            // WebInfoCommand'i direkt çalıştır
                            var webInfoCmd = new WebInfoCommand();
                            var context = new CommandContext { RawCommand = txt };
                            var webResult = await webInfoCmd.ExecuteAsync(context);
                            
                            if (webResult.IsSuccess)
                            {
                                // HTML içeriği varsa göster
                                if (!string.IsNullOrEmpty(webResult.HtmlContent))
                                {
                                    Debug.WriteLine($"[CommandProcessor] HTML içerik var, gösteriliyor");
                                    Debug.WriteLine($"[CommandProcessor] HTML içerik uzunluğu: {webResult.HtmlContent.Length}");
                                    Debug.WriteLine($"[CommandProcessor] HTML ilk 200 karakter: {webResult.HtmlContent.Substring(0, Math.Min(200, webResult.HtmlContent.Length))}");
                                    
                                    if (_webViewManager != null)
                                    {
                                        await _webViewManager.AppendOutput(webResult.HtmlContent);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[CommandProcessor] WebViewManager null, HTML gösterilemedi");
                                    }
                                }
                                
                                // ÇÖZÜM: Centralized TTS - aktif TTS varsa kes, sonra başlat
                                Debug.WriteLine($"[CommandProcessor] WebResult alındı - IsSuccess: {webResult.IsSuccess}");
                                Debug.WriteLine($"[CommandProcessor] VoiceOutput kontrolü - VoiceOutput null mu: {webResult.VoiceOutput == null}");
                                Debug.WriteLine($"[CommandProcessor] VoiceOutput içeriği: '{webResult.VoiceOutput}'");
                                
                                if (!string.IsNullOrEmpty(webResult.VoiceOutput))
                                {
                                    Debug.WriteLine($"[CommandProcessor] TTS çıktısı VAR, aktif TTS kontrol ediliyor");
                                    Debug.WriteLine($"[CommandProcessor] TTS çıktısı uzunluğu: {webResult.VoiceOutput.Length} karakter");
                                    
                                    // Aktif TTS varsa durdur
                                    if (TextToSpeechService.IsSpeaking())
                                    {
                                        Debug.WriteLine($"[CommandProcessor] Aktif TTS kesiliyor");
                                        TextToSpeechService.StopSpeaking();
                                    }
                                    
                                    Debug.WriteLine($"[CommandProcessor] TTS başlatılıyor...");
                                    await TextToSpeechService.SpeakTextAsync(webResult.VoiceOutput);
                                    Debug.WriteLine($"[CommandProcessor] TTS başlatma tamamlandı");
                                }
                                else
                                {
                                    Debug.WriteLine($"[CommandProcessor] TTS çıktısı YOK veya BOŞ - ses çıkışı olmayacak");
                                }
                                
                                ok = true;
                                det = "Web Info Command";
                                Raise(raw, true, webResult.Message ?? "Web komutu başarıyla işlendi", det);
                                return true;
                            }
                            else
                            {
                                Raise(raw, false, webResult.Message ?? "Web komutu başarısız", "Web Info Command");
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CommandProcessor] Web komutu hatası: {ex.Message}");
                            Raise(raw, false, $"Web komutu hatası: {ex.Message}", "Web Info Command");
                            return false;
                        }
                    }
                }
                
                /*----------- 5) SİSTEM KOMUTLARI ----------*/
                // Volume, kopyala, yapıştır gibi sistem komutları  
                var systemCommand = commandFactory.CreateCommandFromText(txt);
                if (systemCommand != null && 
                    (systemCommand.GetType().Name == "SystemWideCommand" || 
                     systemCommand.GetType().Name == "SystemCommand"))
                {
                    Debug.WriteLine($"[CommandProcessor] Sistem komutu algılandı: {systemCommand.GetType().Name}");
                    
                    bool systemResult = await systemCommand.ExecuteAsync();
                    if (systemResult)
                    {
                        ok = true;
                        det = $"System Command ({systemCommand.GetType().Name})";
                        Raise(raw, true, "Sistem komutu başarıyla işlendi", det);
                        return true;
                    }
                }

                /*----------- 5) DOSYA AÇMA --------------------------*/
                if (txt.Contains("dosyasını aç") || txt.Contains("dosyası aç"))
                {
                    string cleaned = txt.Replace("dosyasını aç", "")
                                        .Replace("dosyası aç", "")
                                        .Trim();

                    string fileType = "";
                    string fileName = cleaned;

                    // Dosya türü kelimesini tam kelime olarak bul ve çıkar
                    foreach (var t in _knownTypes)
                    {
                        // Tam kelime eşleşmesi için regex
                        var match = Regex.Match(fileName, $@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            fileType = t;
                            // Sadece ilk geçen tür kelimesini çıkar
                            fileName = (fileName.Remove(match.Index, match.Length)).Trim();
                            // Arada iki boşluk kalırsa düzelt
                            fileName = Regex.Replace(fileName, @"\s+", " ").Trim();
                            break;
                        }
                    }

                    // Eğer dosya adı boşsa, tür kelimesi dosya adı olarak kullanılabilir
                    if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(fileType))
                        fileName = fileType;

                    string ext = DetermineExtension(fileType);
                    string? path = await _fileSearchService.FindFileAsync(fileName, ext);

                    // Eğer tam kelimeyle bulunamazsa, içeren eşleşme ile tekrar dene
                    if (string.IsNullOrEmpty(path) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        Debug.WriteLine($"[CommandProcessor] Tam eşleşme bulunamadı, içeren arama deneniyor...");
                        path = await _fileSearchService.FindFileAsyncContains(fileName, ext);
                    }

                    // İçeren arama da bulunamazsa, fuzzy matching ile dene
                    if (string.IsNullOrEmpty(path) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        Debug.WriteLine($"[CommandProcessor] İçeren arama bulunamadı, fuzzy arama deneniyor...");
                        path = await _fileSearchService.FindFileAsyncFuzzy(fileName, ext);
                    }


                    if (!string.IsNullOrEmpty(path))
                    {
                        ok = await _fileSearchService.OpenFileAsync(path);
                        // Dosya türü varsa çıktıya ekle
                        string fileInfo = fileName;
                        if (!string.IsNullOrWhiteSpace(fileType))
                        {
                            fileInfo = $"{fileName} {fileType}".Trim();
                        }
                        det = $"Dosya Aç ({fileInfo})";
                        Raise(raw, ok, ok ? "" : "Dosya açılamadı", det);
                        return ok;
                    }

                    Raise(raw, false, "Dosya bulunamadı", "Dosya Bulunamadı");
                    return false;
                }

                /*----------- 4) KLASÖR AÇMA -------------------------*/
                if (ContainsAnyVerb(txt, _launchVerbs) &&
                    (txt.Contains("klasör") || txt.Contains("klasörü")))
                {
                    string folder = ExtractFolderName(txt);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        det = $"Klasör Aç ({folder})";
                        // Temel klasörler
                        var baseFolders = new[] {
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            // Downloads için özel yol
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
                        };

                        string? foundFolder = null;
                        var culture = new System.Globalization.CultureInfo("tr-TR");
                        var searchWords = folder.ToLower(culture).Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

                        // Güvenli recursive arama fonksiyonu
                        List<string> SafeGetDirectories(string root)
                        {
                            var all = new List<string>();
                            try
                            {
                                foreach (var dir in Directory.GetDirectories(root))
                                {
                                    all.Add(dir);
                                    all.AddRange(SafeGetDirectories(dir));
                                }
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                ErrorHandler.LogError(uaEx, $"SafeGetDirectories - Unauthorized access: {root}");
                            }
                            catch (Exception ex)
                            {
                                ErrorHandler.LogError(ex, $"SafeGetDirectories - Directory enumeration failed: {root}");
                            }
                            return all;
                        }

                        foreach (var baseFolder in baseFolders)
                        {
                            if (!Directory.Exists(baseFolder)) continue;
                            var dirs = SafeGetDirectories(baseFolder);
                            // Ana klasörü de dahil et
                            dirs.Insert(0, baseFolder);
                            foreach (var dir in dirs)
                            {
                                var dirName = Path.GetFileName(dir).ToLower(culture);
                                System.Diagnostics.Debug.WriteLine($"[CommandProcessor] Klasör taranıyor: {dir}");
                                
                                // Önce tam eşleşme kontrolü
                                if (searchWords.All(word => dirName.Contains(word, StringComparison.OrdinalIgnoreCase)))
                                {
                                    foundFolder = dir;
                                    System.Diagnostics.Debug.WriteLine($"[CommandProcessor] EŞLEŞEN KLASÖR (tam eşleşme): {dir}");
                                    break;
                                }
                            }
                            if (foundFolder != null) break;
                        }

                        // Tam eşleşme bulunamazsa fuzzy matching dene
                        if (foundFolder == null)
                        {
                            Debug.WriteLine($"[CommandProcessor] Tam eşleşme bulunamadı, fuzzy matching deneniyor: {folder}");
                            
                            // İlk olarak ana klasörlerde fuzzy ara
                            var bestMatch = new { Path = (string)null, Similarity = 0.0 };
                            
                            foreach (var baseFolder in baseFolders)
                            {
                                if (!Directory.Exists(baseFolder)) continue;
                                
                                // Ana klasörün kendisi
                                var baseDirName = Path.GetFileName(baseFolder);
                                if (FuzzyMatchFolder(baseDirName, folder))
                                {
                                    double similarity = CalculateSimilarity(folder, baseDirName);
                                    if (similarity > bestMatch.Similarity)
                                    {
                                        bestMatch = new { Path = baseFolder, Similarity = similarity };
                                    }
                                }
                                
                                // Alt klasörler
                                try
                                {
                                    var subDirs = Directory.GetDirectories(baseFolder, "*", SearchOption.TopDirectoryOnly);
                                    foreach (var dir in subDirs)
                                    {
                                        var dirName = Path.GetFileName(dir);
                                        if (FuzzyMatchFolder(dirName, folder))
                                        {
                                            double similarity = CalculateSimilarity(folder, dirName);
                                            if (similarity > bestMatch.Similarity)
                                            {
                                                bestMatch = new { Path = dir, Similarity = similarity };
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[CommandProcessor] Alt klasör erişim hatası: {baseFolder} - {ex.Message}");
                                }
                            }
                            
                            // En iyi eşleşme %70'ten yüksekse kullan
                            if (bestMatch.Similarity >= 0.7 && !string.IsNullOrEmpty(bestMatch.Path))
                            {
                                foundFolder = bestMatch.Path;
                                Debug.WriteLine($"[CommandProcessor] FUZZY EŞLEŞME BULUNDU: '{folder}' -> '{Path.GetFileName(foundFolder)}' (benzerlik: {bestMatch.Similarity:P})");
                            }
                        }


                        if (foundFolder != null)
                        {
                            Process.Start("explorer.exe", foundFolder);
                            ok = true;
                        }
                        else
                        {
                            ok = false;
                        }

                        Raise(raw, ok, ok ? "" : $"{folder} klasörü bulunamadı", det);
                        return ok;
                    }
                }

                /*----------- 5) UYGULAMA AÇMA -----------------------*/
                // Sadece "uygulamasını aç" veya "programını aç" gibi ifadelerle uygulama aç
                if ((txt.Contains("uygulamasını aç") || txt.Contains("programını aç")))
                {
                    // "excel uygulamasını aç" gibi ifadeden uygulama adını ayıkla
                    string appName = txt.Replace("uygulamasını aç", "")
                                        .Replace("programını aç", "")
                                        .Trim();
                    var appInfo = _appRegistry.FindApplication(appName);

                    if (appInfo != null)
                    {
                        det = $"Uygulama Aç ({appInfo.DisplayName})";
                        ok = await _applicationService.OpenOrFocusApplicationAsync(
                                  appInfo.Name, appInfo.ProcessName, appInfo.DisplayName);
                        Raise(raw, ok, ok ? "" : $"{appInfo.DisplayName} açılamadı", det);
                        return ok;
                    }

                    Raise(raw, false, $"{appName} uygulaması bulunamadı",
                                 $"Uygulama Bulunamadı ({appName})");
                    return false;
                }

                /*----------- 6) PENCERE KAPATMA ---------------------*/
                if (ContainsAnyExpression(txt, _closeExpressions))
                {
                    det = "Pencere Kapat";
                    ok = await _executor.ExecuteIntentAsync(new()
                    {
                        Type = CommandIntentType.CloseApplication,
                        Command = txt
                    });
                    Raise(raw, ok, "", det);
                    return ok;
                }

                /*----------- 6) WEB INFO REQUEST (HABER/VİKİPEDİ) ---*/
                if (aiResult?.Intent?.Type == IntentType.WebInfoRequest)
                {
                    Debug.WriteLine($"[CommandProcessor] WebInfoRequest intent algılandı: {raw}");
                    
                    try
                    {
                        var webInfoCmd = new WebInfoCommand();
                        if (webInfoCmd.CanHandle(txt))
                        {
                            var context = new CommandContext { RawCommand = raw };
                            var response = await webInfoCmd.ExecuteAsync(context);
                            
                            if (response.IsSuccess)
                            {
                                // WebView varsa HTML içeriği göster
                                if (_webViewManager != null && !string.IsNullOrEmpty(response.HtmlContent))
                                {
                                    await _webViewManager.LoadHtmlContentAsync(response.HtmlContent);
                                }
                                
                                det = "Web Bilgi Alma";
                                Raise(raw, true, response.VoiceOutput ?? response.Message, det);
                                return true;
                            }
                            else
                            {
                                det = "Web Bilgi Hatası";
                                var errorMsg = ErrorFeedbackService.GetErrorSuggestion("WebContentService");
                                Raise(raw, false, errorMsg.VoiceMessage, det);
                                return false;
                            }
                        }
                    }
                    catch (Exception webEx)
                    {
                        _logger.LogError(webEx, "WebInfoCommand execution error: {Command}", raw);
                        ErrorFeedbackService.LogError("WebInfoCommand", webEx, raw);
                        
                        var errorSuggestion = ErrorFeedbackService.GetErrorSuggestion("WebContentService", webEx);
                        Raise(raw, false, errorSuggestion.VoiceMessage, "Web Bilgi Hatası");
                        return false;
                    }
                }

                /*----------- 7) DİĞER SİSTEM KOMUTLARI -------------*/
                // CommandRegistry'de bulunamayan komutları dene
                ok = await _executor.ExecuteIntentAsync(new()
                {
                    Type = CommandIntentType.SystemCommand,
                    Command = txt
                });
                det = ok ? "Sistem Komutu" : "Bilinmiyor";
                
                // Performance logging
                stopwatch.Stop();
                LoggingService.LogCommandExecution(raw, ok, stopwatch.ElapsedMilliseconds, det);
                _logger.LogInformation("Komut işleme sonucu: {Success}, {Details}, Süre: {ElapsedMs}ms", 
                    ok, det, stopwatch.ElapsedMilliseconds);
                
                // AI'a sonucu bildir (öğrenmesi için)
                await RecordCommandResultToAI(raw, ok);
                
                Raise(raw, ok, "", det);
                return ok;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Komut işleme sırasında hata: {Command}", raw);
                GlobalExceptionHandler.HandleException(ex, "CommandProcessor.ProcessCommandAsync.Execute");
                LoggingService.LogCommandExecution(raw, false, stopwatch.ElapsedMilliseconds, ex.Message);
                
                var userMessage = ex.GetUserFriendlyMessage();
                await ErrorHandler.LogErrorAsync(ex, $"ProcessCommandAsync - Command: {raw}");
                Raise(raw, false, userMessage, det);
                return false;
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.IsRunning)
                {
                    _logger.LogInformation("Komut işleme tamamlandı: {Command}, Süre: {ElapsedMs}ms", 
                        raw, stopwatch.ElapsedMilliseconds);
                }
            }
        }

        /*--------------------------------------------------*/
        private static string DetermineExtension(string t) => t switch
        {
            "excel" => "xls,xlsx,csv,xlsm",
            "word" => "doc,docx,rtf,odt",
            "powerpoint" or "sunum" => "ppt,pptx,pps,ppsx",
            "pdf" => "pdf,xps",
            "metin" => "txt",
            "fotoğraf" or "resim" or "görsel"
                                        => "jpg,jpeg,png,gif,bmp",
            "video" => "mp4,mkv,avi,mov,wmv",
            "müzik" or "ses" => "mp3,wav,m4a",
            "zip" or "sıkıştırılmış" => "zip,rar,7z,tar,gz",
            _ => string.Empty
        };

        private static string ExtractFolderName(string cmd)
        {
            int i = cmd.IndexOf("klasör");
            if (i <= 0) return string.Empty;
            string before = cmd[..i].Trim();
            // Çoklu kelime desteği: "proje raporları klasörünü aç" gibi
            // "klasör" kelimesinden önceki tüm kelimeleri al
            var words = before.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return string.Empty;
            // Son 2 kelimeyi birleştir ("proje raporları" gibi), yoksa son kelimeyi al
            string folderName = words.Length >= 2 ? string.Join(" ", words[^2], words[^1]) : words[^1];
            return folderName;
        }

        private static string ExtractAppName(string cmd, string[] verbs)
        {
            int pos = verbs.Select(v => cmd.LastIndexOf(v)).Max();
            if (pos <= 0) return cmd;
            string app = cmd[..pos].Trim();
            return app.Replace("uygulamasını", "")
                      .Replace("programını", "")
                      .Replace("uygulaması", "")
                      .Replace("programı", "").Trim();
        }

        private static bool ContainsAnyVerb(string t, string[] v) => v.Any(t.Contains);
        private static bool ContainsAnyExpression(string t, string[] e) => e.Any(t.Contains);

        private void Raise(string c, bool s, string m, string d) =>
            CommandProcessed?.Invoke(this, new()
            {
                CommandText = c,
                Success = s,
                ResultMessage = m,
                DetectedIntent = d
            });
            
        /*--------------------------------------------------
         *  AI Helper Methods
         *-------------------------------------------------*/
        
        /// <summary>
        /// AI'a komut sonucunu bildirir
        /// </summary>
        private async Task RecordCommandResultToAI(string command, bool success)
        {
            try
            {
                await _intentDetector.RecordCommandResultAsync(command, success);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI'a komut sonucu bildirme hatası");
            }
        }
        
        /// <summary>
        /// Kullanıcıya komut önerileri sunar
        /// </summary>
        public async Task<string[]> GetCommandSuggestionsAsync(string partialCommand = null)
        {
            try
            {
                return await _intentDetector.GetCommandSuggestionsAsync(partialCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Komut önerisi alma hatası");
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Özel komut tanımlar
        /// </summary>
        public async Task AddCustomCommandAsync(string userCommand, string systemCommand)
        {
            try
            {
                await _intentDetector.AddCustomCommandAsync(userCommand, systemCommand);
                _logger.LogInformation("Özel komut tanımlandı: {User} -> {System}", userCommand, systemCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özel komut tanımlama hatası");
            }
        }
        
        #region Fuzzy Matching Helpers
        
        // Türkçe karakter normalizasyonu
        private static string NormalizeTurkish(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            
            return input
                .Replace('ı', 'i').Replace('İ', 'I')
                .Replace('ğ', 'g').Replace('Ğ', 'G')
                .Replace('ü', 'u').Replace('Ü', 'U')
                .Replace('ş', 's').Replace('Ş', 'S')
                .Replace('ö', 'o').Replace('Ö', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C');
        }
        
        // Levenshtein mesafesi hesaplama
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[source.Length, target.Length];
        }
        
        // Benzerlik hesaplama
        private static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            // Türkçe karakterleri normalize et
            string normalizedSource = NormalizeTurkish(source.ToLowerInvariant());
            string normalizedTarget = NormalizeTurkish(target.ToLowerInvariant());

            // Tam eşleşme varsa 1.0 döndür
            if (normalizedSource.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            int distance = LevenshteinDistance(normalizedSource, normalizedTarget);
            int maxLength = Math.Max(normalizedSource.Length, normalizedTarget.Length);
            
            if (maxLength == 0) return 1.0;
            
            return 1.0 - (double)distance / maxLength;
        }
        
        // Klasör adı için fuzzy matching
        private static bool FuzzyMatchFolder(string folderName, string searchTerm, double threshold = 0.7)
        {
            // Kelimelere böl
            var folderWords = folderName.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var searchWords = searchTerm.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Her arama kelimesi için en iyi eşleşmeyi bul
            foreach (var searchWord in searchWords)
            {
                bool foundMatch = false;
                foreach (var folderWord in folderWords)
                {
                    double similarity = CalculateSimilarity(searchWord, folderWord);
                    if (similarity >= threshold)
                    {
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch) return false;
            }
            
            return true;
        }
        
        #endregion
    }
}
