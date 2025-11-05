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

                // 2. "Claude düşünüyor..." göster
                SendToWebView("aiThinking", new
                {
                    message = "🤔 Claude düşünüyor..."
                });

                // 3. Claude'a gönder
                LogService.LogInfo($"[AIMode] Sending to Claude CLI: '{userInput}'");
                var startTime = DateTime.Now;
                var response = await _claudeService.SendMessageAsync(userInput);
                var duration = (DateTime.Now - startTime).TotalSeconds;

                LogService.LogInfo($"[AIMode] Claude response time: {duration:F1} seconds");
                LogService.LogInfo($"[AIMode] Response IsError: {response.IsError}");
                LogService.LogInfo($"[AIMode] Response Content Length: {response.Content?.Length ?? 0}");

                // 4. Thinking indicator'ü kapat
                SendToWebView("aiThinkingDone", new { });

                // 5. Yanıtı işle
                if (response.IsError)
                {
                    // Hata durumu
                    LogService.LogError($"[AIMode] Claude error: {response.ErrorMessage}");

                    SendToWebView("aiError", new
                    {
                        message = $"❌ {response.ErrorMessage}"
                    });

                    await TextToSpeechService.SpeakTextAsync(
                        "Claude yanıt vermedi. Lütfen tekrar deneyin.");
                }
                else
                {
                    // Başarılı yanıt
                    LogService.LogInfo($"[AIMode] Claude response received ({response.Content.Length} chars, {duration:F1}s)");

                    // Conversation history'ye ekle
                    _conversationHistory.Add(new ConversationTurn
                    {
                        Role = "assistant",
                        Content = response.Content,
                        Timestamp = DateTime.Now
                    });

                    // WebView'a yanıtı ekle
                    SendToWebView("aiAssistantMessage", new
                    {
                        content = response.Content,
                        duration = duration,
                        timestamp = DateTime.Now
                    });

                    // TTS ile seslendir (ilk 2-3 cümle)
                    if (!string.IsNullOrWhiteSpace(response.Content))
                    {
                        var ttsText = GetTTSExcerpt(response.Content);
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
