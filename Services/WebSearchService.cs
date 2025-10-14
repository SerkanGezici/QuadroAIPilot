using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Web sitelerini dinamik olarak bulan ve URL'lerini çözümleyen servis
    /// </summary>
    public class WebSearchService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly List<string> _commonTlds = new List<string>
        {
            ".com", ".com.tr", ".org", ".net", ".gov.tr", ".edu.tr",
            ".org.tr", ".net.tr", ".co", ".io", ".tv", ".info", ".biz"
        };

        static WebSearchService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Verilen site adı için en uygun URL'yi bulur
        /// </summary>
        public static async Task<string> FindWebsiteUrlAsync(string siteName)
        {
            if (string.IsNullOrWhiteSpace(siteName))
                return null;

            Debug.WriteLine($"[WebSearchService] '{siteName}' için URL aranıyor...");

            // 1. Önce site geçmişine bak
            var historyUrl = await SiteHistoryManager.GetUrlFromHistoryAsync(siteName);
            if (!string.IsNullOrEmpty(historyUrl))
            {
                Debug.WriteLine($"[WebSearchService] Geçmişten bulundu: {historyUrl}");

                // VALIDATION 1: Domain matching kontrolü - URL'deki domain site adıyla eşleşmeli
                if (!IsUrlMatchingSiteName(historyUrl, siteName))
                {
                    Debug.WriteLine($"[WebSearchService] ⚠️ Domain uyuşmazlığı! Aranan: '{siteName}', Cache'deki URL: '{historyUrl}' - Cache siliniyor");
                    await SiteHistoryManager.RemoveFromHistoryAsync(siteName);
                }
                // VALIDATION 2: URL'yi doğrula (erişilebilir mi?)
                else if (await IsUrlValidAsync(historyUrl))
                {
                    Debug.WriteLine($"[WebSearchService] ✓ Cache URL geçerli ve eşleşiyor: {historyUrl}");
                    return historyUrl;
                }
                else
                {
                    Debug.WriteLine($"[WebSearchService] ⚠️ Cache URL erişilemiyor: {historyUrl} - Cache siliniyor");
                    await SiteHistoryManager.RemoveFromHistoryAsync(siteName);
                }
            }

            // 2. URL pattern tahminleri yap
            var guessedUrl = await TryGuessUrlAsync(siteName);
            if (!string.IsNullOrEmpty(guessedUrl))
            {
                Debug.WriteLine($"[WebSearchService] Tahmin başarılı: {guessedUrl}");
                await SiteHistoryManager.SaveToHistoryAsync(siteName, guessedUrl);
                return guessedUrl;
            }

            // 3. DuckDuckGo Instant Answer API kullan
            var duckDuckGoUrl = await SearchWithDuckDuckGoAsync(siteName);
            if (!string.IsNullOrEmpty(duckDuckGoUrl))
            {
                Debug.WriteLine($"[WebSearchService] DuckDuckGo'dan bulundu: {duckDuckGoUrl}");
                await SiteHistoryManager.SaveToHistoryAsync(siteName, duckDuckGoUrl);
                return duckDuckGoUrl;
            }

            // 4. Google arama sonuçlarını parse et (fallback)
            var googleUrl = await SearchWithGoogleScrapingAsync(siteName);
            if (!string.IsNullOrEmpty(googleUrl))
            {
                Debug.WriteLine($"[WebSearchService] Google'dan bulundu: {googleUrl}");
                await SiteHistoryManager.SaveToHistoryAsync(siteName, googleUrl);
                return googleUrl;
            }

            Debug.WriteLine($"[WebSearchService] '{siteName}' için URL bulunamadı");
            return null;
        }

        /// <summary>
        /// URL tahmin algoritması - yaygın TLD'lerle dener
        /// </summary>
        private static async Task<string> TryGuessUrlAsync(string siteName)
        {
            // Site adını normalize et
            var normalizedName = NormalizeSiteName(siteName);

            // Eğer zaten TLD içeriyorsa (nokta varsa), direkt dene
            if (normalizedName.Contains("."))
            {
                Debug.WriteLine($"[WebSearchService] Normalize edilmiş ad zaten TLD içeriyor: {normalizedName}");

                var directUrls = new List<string>
                {
                    $"https://www.{normalizedName}",
                    $"https://{normalizedName}",
                    $"http://www.{normalizedName}",
                    $"http://{normalizedName}"
                };

                foreach (var url in directUrls)
                {
                    if (await IsUrlValidAsync(url))
                    {
                        Debug.WriteLine($"[WebSearchService] Geçerli URL bulundu (direkt): {url}");
                        return url;
                    }
                }

                // Direkt deneme başarısız, TLD ekleyerek de dene
                Debug.WriteLine($"[WebSearchService] Direkt deneme başarısız, TLD ekleyerek deneniyor");
            }

            // Yaygın URL pattern'lerini dene
            foreach (var tld in _commonTlds)
            {
                var urls = new List<string>
                {
                    $"https://www.{normalizedName}{tld}",
                    $"https://{normalizedName}{tld}",
                    $"http://www.{normalizedName}{tld}",
                    $"http://{normalizedName}{tld}"
                };

                foreach (var url in urls)
                {
                    if (await IsUrlValidAsync(url))
                    {
                        Debug.WriteLine($"[WebSearchService] Geçerli URL bulundu: {url}");
                        return url;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// URL'nin geçerli ve erişilebilir olup olmadığını kontrol eder
        /// </summary>
        private static async Task<bool> IsUrlValidAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.Add("Accept", "text/html");
                
                var response = await _httpClient.SendAsync(request);
                
                // 200-399 arası status code'lar başarılı sayılır
                return (int)response.StatusCode >= 200 && (int)response.StatusCode < 400;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSearchService] URL kontrol hatası ({url}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// DuckDuckGo Instant Answer API ile arama yapar
        /// </summary>
        private static async Task<string> SearchWithDuckDuckGoAsync(string siteName)
        {
            try
            {
                var query = HttpUtility.UrlEncode($"{siteName} official website");
                var apiUrl = $"https://api.duckduckgo.com/?q={query}&format=json&no_redirect=1&skip_disambig=1";
                
                var response = await _httpClient.GetStringAsync(apiUrl);
                var json = JsonDocument.Parse(response);
                
                // Abstract URL'yi kontrol et
                if (json.RootElement.TryGetProperty("AbstractURL", out var abstractUrl))
                {
                    var url = abstractUrl.GetString();
                    if (!string.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        return url;
                    }
                }

                // Related Topics'leri kontrol et
                if (json.RootElement.TryGetProperty("RelatedTopics", out var topics))
                {
                    foreach (var topic in topics.EnumerateArray())
                    {
                        if (topic.TryGetProperty("FirstURL", out var firstUrl))
                        {
                            var url = firstUrl.GetString();
                            if (!string.IsNullOrEmpty(url) && url.Contains(siteName.ToLowerInvariant()))
                            {
                                // DuckDuckGo URL'sinden gerçek URL'yi çıkar
                                var match = Regex.Match(url, @"https?://[^/]+");
                                if (match.Success)
                                {
                                    return match.Value;
                                }
                            }
                        }
                    }
                }

                // Results'ları kontrol et
                if (json.RootElement.TryGetProperty("Results", out var results))
                {
                    foreach (var result in results.EnumerateArray())
                    {
                        if (result.TryGetProperty("FirstURL", out var url))
                        {
                            var urlString = url.GetString();
                            if (!string.IsNullOrEmpty(urlString))
                            {
                                return ExtractDomainFromUrl(urlString);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSearchService] DuckDuckGo arama hatası: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Google arama sonuçlarını web scraping ile parse eder
        /// </summary>
        private static async Task<string> SearchWithGoogleScrapingAsync(string siteName)
        {
            try
            {
                // Google'a fazla yük bindirmemek için throttle
                await Task.Delay(1000);

                var query = HttpUtility.UrlEncode($"{siteName} official website");
                var searchUrl = $"https://www.google.com/search?q={query}&hl=tr";
                
                var html = await _httpClient.GetStringAsync(searchUrl);
                
                // Google sonuç linklerini parse et
                var linkPattern = @"<a[^>]*href=""(/url\?q=|)(https?://[^""&]+)";
                var matches = Regex.Matches(html, linkPattern);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 2)
                    {
                        var url = match.Groups[2].Value;
                        url = HttpUtility.UrlDecode(url);
                        
                        // Google'ın kendi URL'lerini filtrele
                        if (!url.Contains("google.com") && 
                            !url.Contains("googleusercontent.com") &&
                            !url.Contains("youtube.com") && // YouTube hariç
                            Uri.IsWellFormedUriString(url, UriKind.Absolute))
                        {
                            // Sadece domain kısmını al
                            var domain = ExtractDomainFromUrl(url);
                            if (!string.IsNullOrEmpty(domain))
                            {
                                Debug.WriteLine($"[WebSearchService] Google'dan çıkarılan URL: {domain}");
                                
                                // URL'nin erişilebilir olduğunu kontrol et
                                if (await IsUrlValidAsync(domain))
                                {
                                    return domain;
                                }
                            }
                        }
                    }
                }

                // Alternatif pattern dene
                var altPattern = @"<cite[^>]*>([^<]+)</cite>";
                var citeMatches = Regex.Matches(html, altPattern);
                
                foreach (Match match in citeMatches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var cite = match.Groups[1].Value;
                        cite = HttpUtility.HtmlDecode(cite);
                        
                        // URL formatına dönüştür
                        if (!cite.StartsWith("http"))
                        {
                            cite = "https://" + cite;
                        }
                        
                        if (Uri.IsWellFormedUriString(cite, UriKind.Absolute))
                        {
                            if (await IsUrlValidAsync(cite))
                            {
                                return cite;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSearchService] Google scraping hatası: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// URL'den sadece domain kısmını çıkarır
        /// </summary>
        private static string ExtractDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}";
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// Site adını URL formatına uygun hale getirir
        /// </summary>
        private static string NormalizeSiteName(string siteName)
        {
            if (string.IsNullOrWhiteSpace(siteName))
                return siteName;

            // Önce bilinen site isimlerinin boşluklu hallerini düzelt
            var knownCorrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "oda tv", "odatv" },
                { "hepsi burada", "hepsiburada" },
                { "gitti gidiyor", "gittigidiyor" },
                { "en son haber", "ensonhaber" },
                { "cnn türk", "cnnturk" },
                { "cnn turk", "cnnturk" },
                { "haber türk", "haberturk" },
                { "haber turk", "haberturk" },
                { "my net", "mynet" },
                { "linked in", "linkedin" },
                { "you tube", "youtube" },
                { "face book", "facebook" },
                { "whats app", "whatsapp" },
                { "git hub", "github" },
                { "stack overflow", "stackoverflow" }
            };

            // Bilinen düzeltmeleri uygula
            foreach (var correction in knownCorrections)
            {
                if (siteName.Equals(correction.Key, StringComparison.OrdinalIgnoreCase))
                {
                    siteName = correction.Value;
                    break;
                }
            }

            // Türkçe karakterleri dönüştür
            siteName = siteName.ToLowerInvariant()
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ö", "o")
                .Replace("ç", "c");

            // AKILLI TLD İŞLEME
            // "sozcu com tr" → "sozcu.com.tr"
            // "hurriyet com" → "hurriyet.com"
            siteName = SmartProcessTLD(siteName);

            // Yaygın ön ekleri kaldır
            var prefixes = new[] { "www.", "http://", "https://" };
            foreach (var prefix in prefixes)
            {
                if (siteName.StartsWith(prefix))
                {
                    siteName = siteName.Substring(prefix.Length);
                }
            }

            return siteName;
        }

        /// <summary>
        /// TLD'leri akıllıca işler - "sozcu com tr" → "sozcu.com.tr"
        /// </summary>
        private static string SmartProcessTLD(string siteName)
        {
            if (string.IsNullOrWhiteSpace(siteName))
                return siteName;

            // TLD listesi (sık kullanılan uzantılar)
            var tlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "com", "tr", "org", "net", "gov", "edu", "mil", "int",
                "co", "io", "tv", "me", "info", "biz", "name", "pro",
                "mobi", "asia", "tel", "travel", "aero", "cat", "jobs",
                "museum", "coop", "uk", "de", "fr", "jp", "cn", "ru"
            };

            // Boşluklarla ayrılmış kelimelere böl
            var parts = siteName.Split(new[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length <= 1)
                return siteName; // Tek kelime, işlem gerekmiyor

            // Son kelimelerden başlayarak TLD'leri topla
            List<string> detectedTlds = new List<string>();
            int baseNameEndIndex = parts.Length;

            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (tlds.Contains(parts[i]))
                {
                    detectedTlds.Insert(0, parts[i]);
                    baseNameEndIndex = i;
                }
                else
                {
                    break; // İlk TLD olmayan kelimede dur
                }
            }

            // TLD bulunamadıysa orijinal metni döndür
            if (detectedTlds.Count == 0)
            {
                // Özel karakterleri temizle (nokta, tire hariç sadece izin verilen karakterler)
                return Regex.Replace(siteName, @"[^a-z0-9\-\.]", "");
            }

            // Ana site adını al (TLD öncesi kısım)
            string baseName = string.Join("", parts.Take(baseNameEndIndex));

            // Özel karakterleri temizle
            baseName = Regex.Replace(baseName, @"[^a-z0-9\-]", "");

            // TLD'leri nokta ile birleştir
            string finalTld = string.Join(".", detectedTlds);

            // Sonucu oluştur: "sozcu" + "." + "com.tr"
            return $"{baseName}.{finalTld}";
        }

        /// <summary>
        /// Bing Web Search API kullanarak arama yapar (API key gerektirir)
        /// </summary>
        private static async Task<string> SearchWithBingApiAsync(string siteName, string apiKey = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                // API key yoksa bu metodu kullanma
                return null;
            }

            try
            {
                var query = HttpUtility.UrlEncode($"{siteName} official website");
                var url = $"https://api.bing.microsoft.com/v7.0/search?q={query}&count=5";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("webPages", out var webPages) &&
                        webPages.TryGetProperty("value", out var results))
                    {
                        foreach (var result in results.EnumerateArray())
                        {
                            if (result.TryGetProperty("url", out var urlProp))
                            {
                                var resultUrl = urlProp.GetString();
                                if (!string.IsNullOrEmpty(resultUrl))
                                {
                                    return ExtractDomainFromUrl(resultUrl);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSearchService] Bing API hatası: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// URL'deki domain'in site adıyla eşleşip eşleşmediğini kontrol eder
        /// Örnek: "sozcu.com.tr" arayan kullanıcı için "sozcu.com.tr.io" YANLIŞ
        /// </summary>
        private static bool IsUrlMatchingSiteName(string url, string siteName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(siteName))
                    return false;

                // URL'den domain çıkar
                var uri = new Uri(url);
                var urlDomain = uri.Host.ToLowerInvariant();

                // Site adını normalize et (TLD işleme dahil)
                var normalizedSiteName = NormalizeSiteName(siteName).ToLowerInvariant();

                // "www." öneklerini temizle
                urlDomain = urlDomain.Replace("www.", "");
                normalizedSiteName = normalizedSiteName.Replace("www.", "");

                Debug.WriteLine($"[WebSearchService] Domain matching: URL domain='{urlDomain}' vs Aranan='{normalizedSiteName}'");

                // TAM EŞLEŞME
                if (urlDomain == normalizedSiteName)
                {
                    Debug.WriteLine($"[WebSearchService] ✓ Tam domain eşleşmesi");
                    return true;
                }

                // KURAL: URL domain, aranan site adını içermeli AMA sonuna ekstra TLD eklenmemiş olmalı
                // sozcu.com.tr ✓ sozcu.com.tr içinde
                // sozcu.com.tr.io ✗ sozcu.com.tr'yi içeriyor ama sonuna .io eklenmiş (YANLIŞ!)

                // Eğer URL domain normalized site name ile başlıyorsa
                if (urlDomain.StartsWith(normalizedSiteName))
                {
                    // Sonuna ne eklenmiş?
                    var extraPart = urlDomain.Substring(normalizedSiteName.Length);

                    // Eğer hiçbir şey eklenmemişse, eşleşme var
                    if (string.IsNullOrEmpty(extraPart))
                    {
                        Debug.WriteLine($"[WebSearchService] ✓ Prefix eşleşmesi (ekstra TLD yok)");
                        return true;
                    }

                    // Eğer ekstra bir TLD eklenmişse (örn: .io, .com, .net), bu YANLIŞ!
                    if (extraPart.StartsWith("."))
                    {
                        Debug.WriteLine($"[WebSearchService] ✗ Yanıltıcı domain! Aranan: '{normalizedSiteName}', URL: '{urlDomain}', Ekstra kısım: '{extraPart}'");
                        return false;
                    }
                }

                // TERSİ: Aranan site adı URL domain'i içeriyorsa (örn: "hurriyet.com.tr" URL'i, "hurriyet" araması için geçerli)
                if (normalizedSiteName.Contains(urlDomain))
                {
                    Debug.WriteLine($"[WebSearchService] ✓ Aranan site adı URL domain'i içeriyor");
                    return true;
                }

                // KISMI EŞLEŞME: Her iki yönde de içerme kontrolü (daha esnek)
                // "sozcu" araması "sozcu.com.tr" için geçerli
                // "sozcu.com.tr" araması "sozcu.com.tr.io" için GEÇERSİZ (yukarıda engellendi)
                if (urlDomain.Contains(normalizedSiteName.Replace(".com.tr", "").Replace(".com", "").Replace(".net", "").Replace(".org", "")))
                {
                    Debug.WriteLine($"[WebSearchService] ✓ Kısmi domain eşleşmesi");
                    return true;
                }

                Debug.WriteLine($"[WebSearchService] ✗ Domain eşleşmedi");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSearchService] Domain matching hatası: {ex.Message}");
                return false;
            }
        }
    }
}