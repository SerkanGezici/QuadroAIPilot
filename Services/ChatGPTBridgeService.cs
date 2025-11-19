using QuadroAIPilot.Infrastructure;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// ChatGPT Python Bridge ile iletişim servisi
    /// Python Playwright browser automation üzerinden ChatGPT'ye erişir
    /// </summary>
    public class ChatGPTBridgeService
    {
        // Static HttpClient (timeout değiştirme sorunu için ayrı instance'lar)
        private static readonly HttpClient _chatClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };  // 5 dakika (ChatGPT uzun yanıtlar için)
        private static readonly HttpClient _healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };  // Health check 10 saniye
        private static readonly HttpClient _resetClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };  // Reset 30 saniye

        private const string BRIDGE_URL = "http://localhost:8765/chat";
        private const string HEALTH_URL = "http://localhost:8765/health";

        /// <summary>
        /// ChatGPT'ye mesaj gönderir ve yanıt alır
        /// </summary>
        public static async Task<ChatGPTResponse> SendMessageAsync(string message)
        {
            var startTime = DateTime.Now;

            try
            {
                // Input validasyonu
                if (string.IsNullOrWhiteSpace(message))
                {
                    return new ChatGPTResponse
                    {
                        IsError = true,
                        ErrorMessage = "Boş mesaj gönderilemez"
                    };
                }

                LogService.LogInfo($"[ChatGPTBridge] Sending message: '{message.Substring(0, Math.Min(50, message.Length))}...'");

                // NOT: Sistem promptu artık uygulama başlangıcında 1 kere gönderiliyor (ChatGPTPythonBridge.cs)
                // Her mesajda tekrar gönderilmesine gerek yok, ChatGPT context'i hatırlıyor

                // JSON request oluştur (sadece user input)
                var requestBody = new
                {
                    message = message,  // Sistem promptu YOK, sadece kullanıcı mesajı
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Python Bridge'e gönder (timeout: 3 dakika)
                var response = await _chatClient.PostAsync(BRIDGE_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    LogService.LogError($"[ChatGPTBridge] HTTP error: {response.StatusCode} - {errorText}");
                    return new ChatGPTResponse
                    {
                        IsError = true,
                        ErrorMessage = $"Python Bridge hatası: HTTP {response.StatusCode}",
                        Duration = DateTime.Now - startTime
                    };
                }

                var responseText = await response.Content.ReadAsStringAsync();

                // DEBUG: Raw JSON logla
                LogService.LogInfo($"[ChatGPTBridge] Raw JSON response: {responseText}");

                // JSON parse
                var result = JsonSerializer.Deserialize<ChatGPTResponse>(responseText);

                if (result == null)
                {
                    return new ChatGPTResponse
                    {
                        IsError = true,
                        ErrorMessage = "Parse error",
                        Duration = DateTime.Now - startTime
                    };
                }

                var duration = DateTime.Now - startTime;
                LogService.LogInfo($"[ChatGPTBridge] Response received ({result.Content?.Length ?? 0} chars, {duration.TotalSeconds:F1}s)");

                result.Duration = duration;
                return result;
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"[ChatGPTBridge] Connection error: {ex.Message}");
                return new ChatGPTResponse
                {
                    IsError = true,
                    ErrorMessage = $"Python Bridge bağlantı hatası: {ex.Message}",
                    Duration = DateTime.Now - startTime
                };
            }
            catch (TaskCanceledException)
            {
                LogService.LogError("[ChatGPTBridge] Timeout (3 dakika)");
                return new ChatGPTResponse
                {
                    IsError = true,
                    ErrorMessage = "ChatGPT timeout (3 dakika)",
                    Duration = DateTime.Now - startTime
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ChatGPTBridge] Error: {ex.Message}");
                return new ChatGPTResponse
                {
                    IsError = true,
                    ErrorMessage = $"ChatGPT Bridge error: {ex.Message}",
                    Duration = DateTime.Now - startTime
                };
            }
        }

        /// <summary>
        /// Python Bridge'in çalışıp çalışmadığını kontrol et
        /// </summary>
        public static async Task<bool> IsAvailableAsync()
        {
            try
            {
                LogService.LogInfo("[ChatGPTBridge] Checking availability...");

                var response = await _healthClient.GetAsync(HEALTH_URL);

                if (!response.IsSuccessStatusCode)
                {
                    LogService.LogWarning($"[ChatGPTBridge] Health check failed: HTTP {response.StatusCode}");
                    return false;
                }

                // Python response'u parse et: {"status": "ok", "ready": true/false}
                var responseText = await response.Content.ReadAsStringAsync();
                var healthResponse = JsonSerializer.Deserialize<HealthResponse>(responseText);

                var isReady = healthResponse?.ready ?? false;
                LogService.LogInfo($"[ChatGPTBridge] Availability: HTTP OK, Ready: {isReady}");

                return isReady;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"[ChatGPTBridge] Not available: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Health check response modeli
        /// </summary>
        private class HealthResponse
        {
            public string status { get; set; }
            public bool ready { get; set; }
        }

        /// <summary>
        /// Session'ı sıfırlar (yeni konuşma başlatır)
        /// </summary>
        public static async Task ResetSessionAsync()
        {
            try
            {
                LogService.LogInfo("[ChatGPTBridge] Resetting session...");

                var requestBody = new { action = "reset" };
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _resetClient.PostAsync("http://localhost:8765/reset", content);

                if (response.IsSuccessStatusCode)
                {
                    LogService.LogInfo("[ChatGPTBridge] Session reset successful");
                }
                else
                {
                    LogService.LogWarning($"[ChatGPTBridge] Session reset failed: HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"[ChatGPTBridge] Session reset error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ChatGPT yanıt modeli (Claude modeli ile uyumlu)
    /// </summary>
    public class ChatGPTResponse
    {
        public bool IsError { get; set; }
        public string Content { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
