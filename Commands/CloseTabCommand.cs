using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Tarayıcıda açık bir tab'ı isminden/keyword'ünden bulup kapatır
    /// "hürriyet sekmesini kapat", "youtube tab kapat" gibi komutları işler
    /// </summary>
    public class CloseTabCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _keyword;
        private readonly BrowserIntegrationService _browserService;

        public CloseTabCommand(string commandText, string keyword, BrowserIntegrationService browserService)
        {
            CommandText = commandText;
            _keyword = keyword;
            _browserService = browserService;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[CloseTabCommand] Tab kapatılıyor: {_keyword}");

                // Keyword'den URL pattern oluştur
                string urlPattern = GetUrlPattern(_keyword);
                Debug.WriteLine($"[CloseTabCommand] URL Pattern: {urlPattern}");

                // Browser extension'a tab kapatma isteği gönder
                var result = await _browserService.CloseTabAsync(_keyword, urlPattern);

                if (result.Success)
                {
                    string message = result.Source == "tracked"
                        ? $"{_keyword} sekmesi kapatıldı"
                        : $"{_keyword} sekmesi bulunup kapatıldı";

                    await TextToSpeechService.SpeakTextAsync(message);
                    Debug.WriteLine($"[CloseTabCommand] ✓ Tab başarıyla kapatıldı: {result.TabTitle}");
                    return true;
                }
                else
                {
                    await TextToSpeechService.SpeakTextAsync($"{_keyword} sekmesi bulunamadı");
                    Debug.WriteLine($"[CloseTabCommand] ✗ Tab bulunamadı: {_keyword}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CloseTabCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Sekme kapatılırken hata oluştu");
                return false;
            }
        }

        /// <summary>
        /// Keyword'den URL pattern oluşturur
        /// </summary>
        private string GetUrlPattern(string keyword)
        {
            // Normalize Turkish characters
            var normalized = NormalizeTurkish(keyword.ToLowerInvariant());

            // Bilinen site mapping'leri
            var sitePatterns = new Dictionary<string, string>
            {
                // Haber siteleri
                ["hurriyet"] = "*://*.hurriyet.com.tr/*",
                ["sabah"] = "*://*.sabah.com.tr/*",
                ["milliyet"] = "*://*.milliyet.com.tr/*",
                ["haberturk"] = "*://*.haberturk.com/*",
                ["ntv"] = "*://*.ntv.com.tr/*",
                ["cnn"] = "*://*.cnnturk.com/*",
                ["sozcu"] = "*://*.sozcu.com.tr/*",
                ["cumhuriyet"] = "*://*.cumhuriyet.com.tr/*",

                // Sosyal medya
                ["youtube"] = "*://*.youtube.com/*",
                ["twitter"] = "*://*.twitter.com/*",
                ["x"] = "*://*.x.com/*",
                ["facebook"] = "*://*.facebook.com/*",
                ["instagram"] = "*://*.instagram.com/*",
                ["linkedin"] = "*://*.linkedin.com/*",
                ["tiktok"] = "*://*.tiktok.com/*",

                // E-posta
                ["gmail"] = "*://mail.google.com/*",
                ["outlook"] = "*://outlook.live.com/*",
                ["hotmail"] = "*://outlook.live.com/*",

                // Arama motorları
                ["google"] = "*://*.google.com/*",
                ["bing"] = "*://*.bing.com/*",
                ["yandex"] = "*://*.yandex.com.tr/*",

                // E-ticaret
                ["amazon"] = "*://*.amazon.com.tr/*",
                ["trendyol"] = "*://*.trendyol.com/*",
                ["hepsiburada"] = "*://*.hepsiburada.com/*",
                ["n11"] = "*://*.n11.com/*",

                // Geliştirici siteleri
                ["github"] = "*://*.github.com/*",
                ["stackoverflow"] = "*://*.stackoverflow.com/*",
                ["chatgpt"] = "*://chat.openai.com/*",
                ["claude"] = "*://claude.ai/*",

                // Video/Müzik
                ["netflix"] = "*://*.netflix.com/*",
                ["spotify"] = "*://*.spotify.com/*",
                ["twitch"] = "*://*.twitch.tv/*",

                // Wikipedia
                ["wikipedia"] = "*://*.wikipedia.org/*",
                ["vikipedi"] = "*://*.wikipedia.org/*",

                // Diğer
                ["reddit"] = "*://*.reddit.com/*",
                ["medium"] = "*://*.medium.com/*",
                ["imdb"] = "*://*.imdb.com/*",
            };

            // Önce bilinen sitelerden ara
            if (sitePatterns.TryGetValue(normalized, out string pattern))
            {
                return pattern;
            }

            // Bilinen site değilse, genel pattern oluştur
            // "ekşi sözlük" → "*://*.eksi*sozluk*/*" veya "*://*eksi*sozluk*/*"
            var words = normalized.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 1)
            {
                // Çok kelimeli: tüm kelimeleri içermeli
                return $"*://*{string.Join("*", words)}*/*";
            }
            else
            {
                // Tek kelime: domain veya path'de geçebilir
                return $"*://*.{normalized}.*/*";
            }
        }

        /// <summary>
        /// Türkçe karakterleri normalize eder
        /// </summary>
        private string NormalizeTurkish(string input)
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
    }
}
