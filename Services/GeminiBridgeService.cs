using QuadroAIPilot.Infrastructure;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Gemini Python Bridge ile iletişim servisi
    /// Python Playwright browser automation üzerinden Gemini'ye erişir
    /// ChatGPT bridge pattern'i kullanılarak uyarlanmıştır
    /// </summary>
    public class GeminiBridgeService
    {
        // Static HttpClient (ChatGPT pattern ile aynı)
        private static readonly HttpClient _chatClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };  // 5 dakika
        private static readonly HttpClient _healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };  // Health check 45 saniye
        private static readonly HttpClient _resetClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };  // Reset 30 saniye

        private const string BRIDGE_URL = "http://localhost:8766/chat";  // Port: 8766 (ChatGPT: 8765)
        private const string HEALTH_URL = "http://localhost:8766/health";

        /// <summary>
        /// Gemini'ye mesaj gönderir ve yanıt alır
        /// </summary>
        public static async Task<GeminiResponse> SendMessageAsync(string message)
        {
            var startTime = DateTime.Now;

            try
            {
                // Input validasyonu
                if (string.IsNullOrWhiteSpace(message))
                {
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = "Boş mesaj gönderilemez"
                    };
                }

                LogService.LogInfo($"[GeminiBridge] Sending message: '{message.Substring(0, Math.Min(50, message.Length))}...'");

                // JSON request oluştur
                var requestBody = new
                {
                    message = message,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Python Bridge'e gönder
                var response = await _chatClient.PostAsync(BRIDGE_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    LogService.LogError($"[GeminiBridge] HTTP error: {response.StatusCode} - {errorText}");
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = $"Python Bridge hatası: HTTP {response.StatusCode}",
                        Duration = DateTime.Now - startTime
                    };
                }

                var responseText = await response.Content.ReadAsStringAsync();

                // DEBUG: Raw JSON logla
                LogService.LogInfo($"[GeminiBridge] Raw JSON response: {responseText}");

                // JSON parse
                var result = JsonSerializer.Deserialize<GeminiResponse>(responseText);

                if (result == null)
                {
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = "Parse error",
                        Duration = DateTime.Now - startTime
                    };
                }

                var duration = DateTime.Now - startTime;
                LogService.LogInfo($"[GeminiBridge] Response received ({result.Content?.Length ?? 0} chars, {duration.TotalSeconds:F1}s)");

                result.Duration = duration;
                return result;
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"[GeminiBridge] Connection error: {ex.Message}");
                return new GeminiResponse
                {
                    IsError = true,
                    ErrorMessage = $"Python Bridge bağlantı hatası: {ex.Message}",
                    Duration = DateTime.Now - startTime
                };
            }
            catch (TaskCanceledException)
            {
                LogService.LogError("[GeminiBridge] Timeout (5 dakika)");
                return new GeminiResponse
                {
                    IsError = true,
                    ErrorMessage = "Gemini timeout (5 dakika)",
                    Duration = DateTime.Now - startTime
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"[GeminiBridge] Error: {ex.Message}");
                return new GeminiResponse
                {
                    IsError = true,
                    ErrorMessage = $"Gemini Bridge error: {ex.Message}",
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
                LogService.LogInfo("[GeminiBridge] Checking availability...");

                var response = await _healthClient.GetAsync(HEALTH_URL);

                if (!response.IsSuccessStatusCode)
                {
                    LogService.LogWarning($"[GeminiBridge] Health check failed: HTTP {response.StatusCode}");
                    return false;
                }

                // Python response'u parse et: {"status": "ok", "ready": true/false}
                var responseText = await response.Content.ReadAsStringAsync();
                var healthResponse = JsonSerializer.Deserialize<HealthResponse>(responseText);

                var isReady = healthResponse?.ready ?? false;
                LogService.LogInfo($"[GeminiBridge] Availability: HTTP OK, Ready: {isReady}");

                return isReady;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"[GeminiBridge] Not available: {ex.Message}");
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
                LogService.LogInfo("[GeminiBridge] Resetting session...");

                var requestBody = new { action = "reset" };
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _resetClient.PostAsync("http://localhost:8766/reset", content);

                if (response.IsSuccessStatusCode)
                {
                    LogService.LogInfo("[GeminiBridge] Session reset successful");
                }
                else
                {
                    LogService.LogWarning($"[GeminiBridge] Session reset failed: HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"[GeminiBridge] Session reset error: {ex.Message}");
            }
        }
    }
}
