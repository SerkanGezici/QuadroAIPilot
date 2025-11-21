using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Managers;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Google Gemini API ile iletişim kuran servis
    /// </summary>
    public class GeminiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

        /// <summary>
        /// Gemini API'ye mesaj gönderir ve yanıt alır
        /// </summary>
        public async Task<GeminiResponse> SendMessageAsync(string userInput)
        {
            var startTime = DateTime.Now;

            try
            {
                // API Key kontrolü
                var apiKey = SettingsManager.Instance.Settings.GeminiApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = "Gemini API anahtarı eksik. Lütfen ayarlardan ekleyin."
                    };
                }

                // Input validasyonu
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = "Boş mesaj gönderilemez"
                    };
                }

                LogService.LogInfo($"[GeminiService] Sending message to Gemini API...");

                // Request body oluştur
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = userInput }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // API isteği gönder
                var response = await _httpClient.PostAsync($"{API_URL}?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    LogService.LogError($"[GeminiService] HTTP error: {response.StatusCode} - {errorText}");
                    return new GeminiResponse
                    {
                        IsError = true,
                        ErrorMessage = $"Gemini API hatası: HTTP {response.StatusCode}",
                        Duration = DateTime.Now - startTime
                    };
                }

                var responseText = await response.Content.ReadAsStringAsync();
                
                // Yanıtı parse et
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var candidateContent) && 
                        candidateContent.TryGetProperty("parts", out var parts) && 
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        var duration = DateTime.Now - startTime;
                        
                        LogService.LogInfo($"[GeminiService] Response received ({text.Length} chars, {duration.TotalSeconds:F1}s)");

                        return new GeminiResponse
                        {
                            Content = text,
                            Duration = duration,
                            IsError = false
                        };
                    }
                }

                return new GeminiResponse
                {
                    IsError = true,
                    ErrorMessage = "Gemini API yanıtı işlenemedi (beklenen formatta değil)",
                    Duration = DateTime.Now - startTime
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"[GeminiService] Error: {ex.Message}");
                return new GeminiResponse
                {
                    IsError = true,
                    ErrorMessage = $"Gemini hatası: {ex.Message}",
                    Duration = DateTime.Now - startTime
                };
            }
        }
    }

    public class GeminiResponse
    {
        public string Content { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
