using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Services;
using QuadroAIPilot.State;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroAIPilot.Modes
{
    /// <summary>
    /// AI Asistan modu - Claude CLI ile etkileşim
    /// </summary>
    public class AIMode : IMode
    {
        private readonly ClaudeCLIService _claudeService;
        private readonly SemaphoreSlim _processingSemaphore;
        private readonly List<ConversationTurn> _conversationHistory;
        private readonly Queue<string> _messageQueue;
        private bool _isProcessing;

        public AIMode()
        {
            _claudeService = new ClaudeCLIService();
            _processingSemaphore = new SemaphoreSlim(1, 1);
            _conversationHistory = new List<ConversationTurn>();
            _messageQueue = new Queue<string>();
            _isProcessing = false;

            LogService.LogInfo("[AIMode] AI Mode initialized with message queue");
        }

        public void Enter()
        {
            Debug.WriteLine("[AIMode] AI Asistan moduna girildi");
            LogService.LogInfo("[AIMode] Entering AI mode");

            // Claude CLI kontrolü
            if (!ClaudeCLIService.IsClaudeCLIAvailable())
            {
                LogService.LogError("[AIMode] Claude CLI not found!");

                // Kullanıcıya uyarı
                _ = Task.Run(async () =>
                {
                    await TextToSpeechService.SpeakTextAsync(
                        "Claude CLI bulunamadı. AI modu kullanılamaz.");
                });

                // WebView'a hata mesajı
                SendToWebView("aiError", new
                {
                    message = "❌ Claude CLI kurulu değil! AI modu çalışmaz."
                });

                return;
            }

            // NOT: Selamlama mesajını ModeManager veriyor, burada tekrar vermeyelim
            // ModeManager -> "Yapay Zeka Asistan Moduna geçildi."

            // WebView'a mod aktivasyonu bildir
            SendToWebView("aiModeActivated", new
            {
                message = "🤖 AI Asistan Modu Aktif",
                timestamp = DateTime.Now
            });
        }

        public void Exit()
        {
            Debug.WriteLine("[AIMode] AI Asistan modundan çıkıldı");
            LogService.LogInfo("[AIMode] Exiting AI mode");

            // Kuyrukta bekleyen mesajları temizle
            if (_messageQueue.Count > 0)
            {
                LogService.LogInfo($"[AIMode] Clearing {_messageQueue.Count} queued messages on exit");
                _messageQueue.Clear();
            }

            // WebView'a mod deaktivasyonu bildir
            SendToWebView("aiModeDeactivated", new { });
        }

        public bool HandleSpeech(string text)
        {
            Debug.WriteLine($"[AIMode] Soru alındı: '{text}'");
            LogService.LogInfo($"[AIMode] User input: '{text}'");

            // DEBUG: Tam metin içeriğini logla
            LogService.LogInfo($"[AIMode] DEBUG - Lowercase text: '{text.ToLowerInvariant().TrimEnd('.')}'");
            LogService.LogInfo($"[AIMode] DEBUG - Contains 'yazı modu': {text.ToLowerInvariant().Contains("yazı modu")}");
            LogService.LogInfo($"[AIMode] DEBUG - Contains 'komut modu': {text.ToLowerInvariant().Contains("komut modu")}");

            // Şu anda işlem yapılıyorsa kuyruğa al
            if (_isProcessing)
            {
                _messageQueue.Enqueue(text);
                var queuePosition = _messageQueue.Count;

                LogService.LogInfo($"[AIMode] Message queued (position: {queuePosition}): '{text}'");

                SendToWebView("aiQueued", new
                {
                    message = $"⏳ Sırada bekliyor (#{queuePosition})",
                    position = queuePosition,
                    content = text
                });

                // İlk kuyrukta bekleyen mesaj için bildirim
                if (queuePosition == 1)
                {
                    _ = Task.Run(async () =>
                    {
                        await TextToSpeechService.SpeakTextAsync("Sorunuz sıraya alındı.");
                    });
                }

                return true;
            }

            // İşlem başlat
            _isProcessing = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessUserInputAsync(text);
                }
                finally
                {
                    _isProcessing = false;

                    // Kuyrukta mesaj varsa işle
                    ProcessNextInQueue();
                }
            });

            return true;
        }

        /// <summary>
        /// Kuyrukta bekleyen bir sonraki mesajı işler
        /// </summary>
        private void ProcessNextInQueue()
        {
            if (_messageQueue.Count == 0)
            {
                LogService.LogInfo("[AIMode] Queue empty, no more messages to process");
                return;
            }

            var nextMessage = _messageQueue.Dequeue();
            var remainingCount = _messageQueue.Count;

            LogService.LogInfo($"[AIMode] Processing next message from queue: '{nextMessage}' (Remaining: {remainingCount})");

            SendToWebView("aiQueueProcessing", new
            {
                message = $"🔄 Sıradaki soru işleniyor... (Kalan: {remainingCount})",
                content = nextMessage,
                remaining = remainingCount
            });

            _isProcessing = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessUserInputAsync(nextMessage);
                }
                finally
                {
                    _isProcessing = false;

                    // Recursive: kuyrukta başka mesaj varsa devam et
                    ProcessNextInQueue();
                }
            });
        }

        /// <summary>
        /// Kullanıcı inputunu Claude'a gönderir ve yanıtı işler
        /// </summary>
        private async Task ProcessUserInputAsync(string userInput)
        {
            try
            {
                // 1. User mesajını WebView'a ekle
                SendToWebView("aiUserMessage", new
                {
                    content = userInput,
                    timestamp = DateTime.Now
                });

                // Conversation history'ye ekle
                _conversationHistory.Add(new ConversationTurn
                {
                    Role = "user",
                    Content = userInput,
                    Timestamp = DateTime.Now
                });

                // 2. Kimlik sorusu kontrolü - AI'a göndermeden lokal yanıtla
                if (IsIdentityQuestion(userInput))
                {
                    LogService.LogInfo("[AIMode] Kimlik sorusu tespit edildi - lokal yanıt veriliyor (3sn gecikme)");
                    var identityResponse = GetIdentityResponse();       // Ekran için (AI yazılı)
                    var identityResponseTTS = GetIdentityResponseForTTS(); // TTS için (EyAy sesli)

                    // 3 saniye gecikme - gerçek AI yanıtı gibi görünsün
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(3000);

                        // WebView'a asistan yanıtı ekle (ekran versiyonu)
                        SendToWebView("aiAssistantMessage", new
                        {
                            content = identityResponse,
                            timestamp = DateTime.Now
                        });

                        // Conversation history'ye ekle
                        _conversationHistory.Add(new ConversationTurn
                        {
                            Role = "assistant",
                            Content = identityResponse,
                            Timestamp = DateTime.Now
                        });

                        // TTS ile seslendir (TTS versiyonu - "EyAy" telaffuzu)
                        await TextToSpeechService.SpeakTextAsync(identityResponseTTS);

                        // Düşünme durumunu kapat
                        SendToWebView("aiThinkingDone", new { });
                    });

                    return; // AI'a gönderme!
                }

                // 3. Provider'a göre "düşünüyor..." mesajı
                var currentProvider = AppState.CurrentAIProvider;

                SendToWebView("aiThinking", new
                {
                    message = "🤔 Quadro Asistan düşünüyor..."
                });

                // 3. Provider'a gönder (fallback ile)
                var providerName = currentProvider == AppState.AIProvider.ChatGPT ? "ChatGPT"
                                 : currentProvider == AppState.AIProvider.Gemini ? "Gemini"
                                 : "Claude";
                LogService.LogInfo($"[AIMode] Sending to {providerName}: '{userInput}'");
                var startTime = DateTime.Now;

                // Dinamik fallback mekanizması
                bool isError = false;
                string errorMessage = null;
                string content = null;
                bool allProvidersFailed = true;

                // Provider ve fallback zincirini belirle
                var providersToTry = GetProviderChain(currentProvider);

                foreach (var provider in providersToTry)
                {
                    var (success, providerContent, providerError) = await TrySendToProviderAsync(provider, userInput);

                    if (success)
                    {
                        content = providerContent;
                        isError = false;
                        allProvidersFailed = false;
                        break;
                    }
                    else
                    {
                        // Bu provider başarısız, bir sonrakine geç
                        errorMessage = providerError;
                        isError = true;

                        // Eğer son provider değilse, fallback bildirimi yap
                        if (provider != providersToTry[providersToTry.Length - 1])
                        {
                            LogService.LogWarning($"[AIMode] {provider} failed, trying next fallback: {providerError}");
                            await TextToSpeechService.SpeakTextAsync("Quadro Asistan alternatif sisteme geçiyor.");
                        }
                    }
                }

                // Tüm provider'lar başarısız olduysa
                if (allProvidersFailed)
                {
                    LogService.LogError("[AIMode] All AI providers failed!");
                    content = null;
                    isError = true;
                    errorMessage = "AI servisine ulaşılamadı";
                }

                var duration = (DateTime.Now - startTime).TotalSeconds;

                LogService.LogInfo($"[AIMode] AI response time: {duration:F1} seconds");
                LogService.LogInfo($"[AIMode] Response IsError: {isError}");
                LogService.LogInfo($"[AIMode] Response Content Length: {content?.Length ?? 0}");

                // 4. Thinking indicator'ü kapat
                SendToWebView("aiThinkingDone", new { });

                // 5. Yanıtı işle
                if (isError || string.IsNullOrWhiteSpace(content))
                {
                    // Hata durumu veya boş yanıt
                    var errorMsg = isError ? errorMessage : "Quadro Asistan yanıt veremedi";

                    // Hata mesajlarında ChatGPT/OpenAI/Claude kelimelerini Quadro ile değiştir
                    errorMsg = errorMsg.Replace("ChatGPT", "Quadro Asistan")
                                       .Replace("OpenAI", "Quadro")
                                       .Replace("Claude", "Quadro Asistan")
                                       .Replace("GPT", "Quadro");

                    LogService.LogError($"[AIMode] AI error: {errorMsg}");

                    SendToWebView("aiError", new
                    {
                        message = $"❌ {errorMsg}"
                    });

                    await TextToSpeechService.SpeakTextAsync(
                        "Quadro Asistan yanıt vermedi. Lütfen tekrar deneyin.");
                }
                else
                {
                    // Başarılı yanıt
                    LogService.LogInfo($"[AIMode] AI response received ({content.Length} chars, {duration:F1}s)");

                    // Conversation history'ye ekle
                    _conversationHistory.Add(new ConversationTurn
                    {
                        Role = "assistant",
                        Content = content,
                        Timestamp = DateTime.Now
                    });

                    // WebView'a yanıtı ekle
                    SendToWebView("aiAssistantMessage", new
                    {
                        content = content,
                        duration = duration,
                        timestamp = DateTime.Now
                    });

                    // TTS ile seslendir (ilk 2-3 cümle)
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var ttsText = GetTTSExcerpt(content);
                        LogService.LogInfo($"[AIMode] TTS text: '{ttsText}'");
                        await TextToSpeechService.SpeakTextAsync(ttsText);
                    }
                    else
                    {
                        LogService.LogWarning("[AIMode] Response content is empty, skipping TTS");
                        await TextToSpeechService.SpeakTextAsync("Claude yanıt veremedi.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[AIMode] ProcessUserInput error: {ex.Message}");

                SendToWebView("aiError", new
                {
                    message = $"❌ Beklenmeyen hata: {ex.Message}"
                });

                await TextToSpeechService.SpeakTextAsync("Beklenmeyen bir hata oluştu.");
            }
        }

        /// <summary>
        /// WebView'a mesaj gönderir
        /// </summary>
        private void SendToWebView(string action, object data)
        {
            try
            {
                var webViewManager = ServiceLocator.GetWebViewManager();
                if (webViewManager != null)
                {
                    var message = new
                    {
                        action,
                        data
                    };
                    webViewManager.SendMessage(message);
                }
                else
                {
                    LogService.LogWarning("[AIMode] WebViewManager is null");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[AIMode] SendToWebView error: {ex.Message}");
            }
        }

        /// <summary>
        /// Uzun yanıtlardan TTS için kısa özet çıkarır
        /// </summary>
        private string GetTTSExcerpt(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText))
                return "Yanıt hazır. Ekrandan okuyabilirsiniz.";

            // Markdown kod bloklarını temizle
            var cleaned = Regex.Replace(fullText, @"```[\s\S]*?```", "[kod bloğu]");

            // İlk 2-3 cümleyi al
            var sentences = Regex.Split(cleaned, @"(?<=[.!?])\s+");
            var excerpt = string.Join(" ", sentences.Take(3));

            // Maksimum 300 karakter
            if (excerpt.Length > 300)
            {
                excerpt = excerpt.Substring(0, 297) + "...";
            }

            // Çok kısa ise tamamını oku
            if (excerpt.Length < 50)
            {
                return cleaned.Length > 300
                    ? cleaned.Substring(0, 297) + "..."
                    : cleaned;
            }

            // Çok uzun yanıt için uyarı ekle
            if (fullText.Length > 1000)
            {
                excerpt += " Detaylar ekranda.";
            }

            return excerpt;
        }

        /// <summary>
        /// Seçili provider'a göre fallback zincirini döndürür
        /// Gemini -> ChatGPT -> Claude
        /// ChatGPT -> Gemini -> Claude
        /// Claude -> Gemini -> ChatGPT
        /// </summary>
        private AppState.AIProvider[] GetProviderChain(AppState.AIProvider primary)
        {
            return primary switch
            {
                AppState.AIProvider.Gemini => new[] { AppState.AIProvider.Gemini, AppState.AIProvider.ChatGPT, AppState.AIProvider.Claude },
                AppState.AIProvider.ChatGPT => new[] { AppState.AIProvider.ChatGPT, AppState.AIProvider.Gemini, AppState.AIProvider.Claude },
                AppState.AIProvider.Claude => new[] { AppState.AIProvider.Claude, AppState.AIProvider.Gemini, AppState.AIProvider.ChatGPT },
                _ => new[] { AppState.AIProvider.Gemini, AppState.AIProvider.ChatGPT, AppState.AIProvider.Claude }
            };
        }

        /// <summary>
        /// Belirtilen provider'a mesaj göndermeyi dener
        /// </summary>
        private async Task<(bool success, string content, string error)> TrySendToProviderAsync(AppState.AIProvider provider, string userInput)
        {
            try
            {
                switch (provider)
                {
                    case AppState.AIProvider.ChatGPT:
                        if (await ChatGPTBridgeService.IsAvailableAsync())
                        {
                            var chatgptResponse = await ChatGPTBridgeService.SendMessageAsync(userInput);
                            if (!chatgptResponse.IsError && !string.IsNullOrWhiteSpace(chatgptResponse.Content))
                            {
                                LogService.LogInfo($"[AIMode] ChatGPT responded successfully");
                                return (true, chatgptResponse.Content, null);
                            }
                            return (false, null, chatgptResponse.ErrorMessage ?? "ChatGPT yanıt vermedi");
                        }
                        return (false, null, "ChatGPT kullanılamıyor");

                    case AppState.AIProvider.Gemini:
                        if (await GeminiBridgeService.IsAvailableAsync())
                        {
                            var geminiResponse = await GeminiBridgeService.SendMessageAsync(userInput);
                            if (!geminiResponse.IsError && !string.IsNullOrWhiteSpace(geminiResponse.Content))
                            {
                                LogService.LogInfo($"[AIMode] Gemini responded successfully");
                                return (true, geminiResponse.Content, null);
                            }
                            return (false, null, geminiResponse.ErrorMessage ?? "Gemini yanıt vermedi");
                        }
                        return (false, null, "Gemini kullanılamıyor");

                    case AppState.AIProvider.Claude:
                        // Progress callback ile Claude'a gönder (dinamik timeout desteği)
                        var claudeResponse = await _claudeService.SendMessageAsync(userInput, (lastLine, elapsedSeconds) =>
                        {
                            // Progress mesajını UI'a gönder
                            var truncatedLine = lastLine.Length > 80 ? lastLine.Substring(0, 80) + "..." : lastLine;
                            SendToWebView("aiProgress", new
                            {
                                status = "working",
                                message = $"⏳ İşlem devam ediyor ({elapsedSeconds}s): {truncatedLine}",
                                elapsed = elapsedSeconds
                            });
                            LogService.LogInfo($"[AIMode] Claude CLI progress ({elapsedSeconds}s): {truncatedLine}");
                        });
                        if (!claudeResponse.IsError && !string.IsNullOrWhiteSpace(claudeResponse.Content))
                        {
                            // Claude hata mesajlarını kontrol et - bunlar başarısız yanıt sayılmalı
                            if (IsClaudeErrorResponse(claudeResponse.Content))
                            {
                                LogService.LogWarning($"[AIMode] Claude returned error in content: {claudeResponse.Content.Substring(0, Math.Min(100, claudeResponse.Content.Length))}");
                                return (false, null, "Claude authentication/token hatası - fallback devreye giriyor");
                            }

                            LogService.LogInfo($"[AIMode] Claude responded successfully");
                            return (true, claudeResponse.Content, null);
                        }
                        return (false, null, claudeResponse.ErrorMessage ?? "Claude yanıt vermedi");

                    default:
                        return (false, null, "Bilinmeyen provider");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[AIMode] Provider {provider} exception: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Claude yanıt içeriğinde hata pattern'lerini kontrol eder
        /// Token expired, authentication error gibi durumlar başarısız yanıt sayılır
        /// </summary>
        private bool IsClaudeErrorResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            var errorPatterns = new[]
            {
                "API Error:",
                "authentication_error",
                "OAuth token has expired",
                "Please run /login",
                "rate_limit",
                "invalid_api_key",
                "permission_denied",
                "token has expired",
                "401",
                "403"
            };

            return errorPatterns.Any(pattern =>
                content.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Kimlik sorularını tespit eder (sen kimsin, hangi AI, vs.)
        /// Bu sorular AI'a gönderilmeden lokal olarak yanıtlanır
        /// </summary>
        private bool IsIdentityQuestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalizedText = text.ToLowerInvariant()
                .Replace("?", "")
                .Replace("!", "")
                .Replace(".", "")
                .Trim();

            var identityPatterns = new[]
            {
                // Direkt kimlik soruları
                "sen kimsin",
                "sen nesin",
                "adın ne",
                "ismin ne",
                "adınız ne",
                "isminiz ne",
                "kendini tanıt",
                "kendinden bahset",

                // Yapımcı/geliştirici soruları
                "kim yaptı",
                "kim geliştirdi",
                "kim üretti",
                "kim yarattı",
                "kimin ürünü",
                "kimin yapay zeka",
                "seni kim yaptı",
                "seni kim geliştirdi",

                // Model/AI soruları
                "hangi yapay zeka",
                "hangi ai",
                "hangi model",
                "hangi dil modeli",
                "ne tür ai",
                "ne tür yapay zeka",
                "nasıl bir ai",
                "nasıl bir yapay zeka",
                "ne yapay zekası",

                // Spesifik AI kontrolleri
                "gpt misin",
                "chatgpt misin",
                "gemini misin",
                "claude misin",
                "bard mısın",
                "google mısın",
                "openai mısın",
                "anthropic misin",
                "microsoft misin",
                "copilot misin",
                "llama mısın",
                "meta mısın"
            };

            return identityPatterns.Any(pattern =>
                normalizedText.Contains(pattern));
        }

        /// <summary>
        /// Kimlik sorusuna verilecek standart yanıt (ekranda gösterilecek)
        /// </summary>
        private string GetIdentityResponse()
        {
            return "Ben Quadro AI Pilot'um. Quadro Computer tarafından geliştirilen yapay zeka asistanıyım. Size nasıl yardımcı olabilirim?";
        }

        /// <summary>
        /// Kimlik sorusuna verilecek TTS yanıtı (Türkçe telaffuz için "AI" → "EyAy")
        /// </summary>
        private string GetIdentityResponseForTTS()
        {
            return "Ben Quadro EyAy Pilot'um. Quadro Computer tarafından geliştirilen yapay zeka asistanıyım. Size nasıl yardımcı olabilirim?";
        }

        /// <summary>
        /// Session'ı sıfırlar
        /// </summary>
        public void ResetSession()
        {
            _claudeService.ResetSession();
            _conversationHistory.Clear();
            _messageQueue.Clear();
            _isProcessing = false;

            LogService.LogInfo("[AIMode] Session reset (conversation, queue, processing state cleared)");

            SendToWebView("aiSessionReset", new
            {
                message = "🔄 Sohbet sıfırlandı"
            });

            _ = Task.Run(async () =>
            {
                await TextToSpeechService.SpeakTextAsync("Sohbet geçmişi temizlendi.");
            });
        }

        /// <summary>
        /// Conversation history'yi döndürür
        /// </summary>
        public IReadOnlyList<ConversationTurn> GetConversationHistory()
        {
            return _conversationHistory.AsReadOnly();
        }
    }

    /// <summary>
    /// Sohbet satırı modeli
    /// </summary>
    public class ConversationTurn
    {
        public string Role { get; set; } // "user" veya "assistant"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
