using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace QuadroAIPilot.Services
{
    public interface IGoogleTranslateService
    {
        Task<string> TranslateAsync(string text, string targetLanguage = "tr", string sourceLanguage = "auto");
        Task<string> DetectLanguageAsync(string text);
    }

    /// <summary>
    /// Google Translate'in unofficial API'sini kullanarak çeviri yapan servis
    /// EdgeTTSService'e benzer şekilde çalışır
    /// </summary>
    public class GoogleTranslateService : IGoogleTranslateService
    {
        private readonly ILogger<GoogleTranslateService> _logger;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        
        private const string TRANSLATE_URL = "https://translate.googleapis.com/translate_a/single";
        private const int CACHE_DURATION_MINUTES = 60;
        private const int MAX_TEXT_LENGTH = 5000;
        
        public GoogleTranslateService(
            ILogger<GoogleTranslateService> logger,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _cache = cache;
            _httpClient = httpClientFactory.CreateClient();
            
            // User-Agent ayarla (Edge gibi görün)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.59");
        }
        
        /// <summary>
        /// Metni hedef dile çevirir
        /// </summary>
        public async Task<string> TranslateAsync(string text, string targetLanguage = "tr", string sourceLanguage = "auto")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Boş metin çeviri isteği");
                return string.Empty;
            }
            
            // Metin çok uzunsa kısalt
            if (text.Length > MAX_TEXT_LENGTH)
            {
                _logger.LogWarning($"Metin çok uzun ({text.Length} karakter), {MAX_TEXT_LENGTH} karaktere kısaltılıyor");
                text = text.Substring(0, MAX_TEXT_LENGTH);
            }
            
            // Cache key oluştur
            var cacheKey = $"translate_{sourceLanguage}_{targetLanguage}_{ComputeHash(text)}";
            
            // Cache'de varsa döndür
            if (_cache.TryGetValue<string>(cacheKey, out var cachedTranslation))
            {
                _logger.LogDebug("Çeviri cache'den alındı");
                return cachedTranslation;
            }
            
            try
            {
                // URL parametrelerini oluştur
                var queryParams = new Dictionary<string, string>
                {
                    ["client"] = "gtx",
                    ["sl"] = sourceLanguage,
                    ["tl"] = targetLanguage,
                    ["dt"] = "t",  // Çeviri
                    ["q"] = text
                };
                
                var url = BuildUrl(TRANSLATE_URL, queryParams);
                
                _logger.LogDebug($"Google Translate API çağrısı: {sourceLanguage} -> {targetLanguage}");
                
                // API çağrısı yap
                var response = await _httpClient.GetStringAsync(url);
                
                // JSON response'u parse et
                var translatedText = ParseTranslationResponse(response);
                
                if (!string.IsNullOrEmpty(translatedText))
                {
                    // Cache'e ekle
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    
                    _cache.Set(cacheKey, translatedText, cacheOptions);
                    
                    _logger.LogInformation($"Çeviri tamamlandı: {text.Length} karakter -> {translatedText.Length} karakter");
                }
                
                return translatedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Translate API hatası");
                return text; // Hata durumunda orijinal metni döndür
            }
        }
        
        /// <summary>
        /// Metnin dilini algılar
        /// </summary>
        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "unknown";
            
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["client"] = "gtx",
                    ["sl"] = "auto",
                    ["tl"] = "en",
                    ["dt"] = "sl",  // Source language detection
                    ["q"] = text.Substring(0, Math.Min(text.Length, 100)) // İlk 100 karakter yeterli
                };
                
                var url = BuildUrl(TRANSLATE_URL, queryParams);
                var response = await _httpClient.GetStringAsync(url);
                
                // Dil kodunu parse et
                var jsonDoc = JsonDocument.Parse(response);
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array && 
                    jsonDoc.RootElement.GetArrayLength() > 2)
                {
                    var langCode = jsonDoc.RootElement[2].GetString();
                    _logger.LogDebug($"Algılanan dil: {langCode}");
                    return langCode ?? "unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil algılama hatası");
            }
            
            return "unknown";
        }
        
        /// <summary>
        /// Google Translate API response'unu parse eder
        /// </summary>
        private string ParseTranslationResponse(string jsonResponse)
        {
            try
            {
                // Google Translate response formatı: [[["çeviri","orijinal",null,null,10]],...]
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                
                if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Beklenmeyen JSON formatı");
                    return string.Empty;
                }
                
                var translatedParts = new StringBuilder();
                
                // İlk array element'i çeviri sonuçlarını içerir
                var translationArray = jsonDoc.RootElement[0];
                
                if (translationArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in translationArray.EnumerateArray())
                    {
                        if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0)
                        {
                            var translatedText = part[0].GetString();
                            if (!string.IsNullOrEmpty(translatedText))
                            {
                                translatedParts.Append(translatedText);
                            }
                        }
                    }
                }
                
                return translatedParts.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON parse hatası");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// URL ve query parametrelerini birleştirir
        /// </summary>
        private string BuildUrl(string baseUrl, Dictionary<string, string> queryParams)
        {
            var queryString = new StringBuilder();
            
            foreach (var param in queryParams)
            {
                if (queryString.Length > 0)
                    queryString.Append('&');
                
                queryString.Append($"{param.Key}={Uri.EscapeDataString(param.Value)}");
            }
            
            return $"{baseUrl}?{queryString}";
        }
        
        /// <summary>
        /// Metin için basit hash hesaplar (cache key için)
        /// </summary>
        private string ComputeHash(string text)
        {
            var hash = 0;
            foreach (char c in text)
            {
                hash = ((hash << 5) - hash) + c;
            }
            return Math.Abs(hash).ToString();
        }
    }
}