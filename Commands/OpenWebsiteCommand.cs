using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Web sitesi açma komutlarını işleyen sınıf
    /// </summary>
    public class OpenWebsiteCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _siteName;
        private readonly WebsiteRegistry.WebsiteInfo _websiteInfo;

        /// <summary>
        /// Web sitesi açma komutu oluşturur
        /// </summary>
        public OpenWebsiteCommand(string commandText, string siteName)
        {
            CommandText = commandText;
            _siteName = siteName;

            // Site bilgisini bul
            _websiteInfo = WebsiteRegistry.FindWebsite(siteName);
        }

        /// <summary>
        /// Komutu çalıştırır - web sitesini varsayılan tarayıcıda açar
        /// </summary>
        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[OpenWebsiteCommand] Web sitesi açma komutu: {_siteName}");

                if (_websiteInfo == null)
                {
                    Debug.WriteLine($"[OpenWebsiteCommand] Site bulunamadı: {_siteName}");
                    
                    // Kategori bazlı arama yap
                    if (TryOpenCategoryWebsite())
                    {
                        return true;
                    }

                    // Site bulunamadı mesajı
                    await TextToSpeechService.SpeakTextAsync($"{_siteName} web sitesi bulunamadı");
                    return false;
                }

                // Web sitesini aç
                Debug.WriteLine($"[OpenWebsiteCommand] Açılıyor: {_websiteInfo.Name} - {_websiteInfo.Url}");

                // Ses geri bildirimi
                await TextToSpeechService.SpeakTextAsync($"{_websiteInfo.Name} web sitesi açılıyor");

                // Tarayıcıda aç
                Process.Start(new ProcessStartInfo
                {
                    FileName = _websiteInfo.Url,
                    UseShellExecute = true
                });

                Debug.WriteLine($"[OpenWebsiteCommand] {_websiteInfo.Name} başarıyla açıldı");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenWebsiteCommand] Hata: {ex.Message}");
                
                await TextToSpeechService.SpeakTextAsync("Web sitesi açılırken bir hata oluştu");
                
                return false;
            }
        }

        /// <summary>
        /// Kategori bazlı web sitesi açmayı dener
        /// </summary>
        private bool TryOpenCategoryWebsite()
        {
            try
            {
                // Kategori anahtar kelimeleri kontrol et
                var lowerCommand = _siteName.ToLowerInvariant();
                WebsiteRegistry.WebsiteCategory? category = null;

                if (lowerCommand.Contains("haber") || lowerCommand.Contains("gazete"))
                {
                    category = WebsiteRegistry.WebsiteCategory.News;
                }
                else if (lowerCommand.Contains("alışveriş") || lowerCommand.Contains("market"))
                {
                    category = WebsiteRegistry.WebsiteCategory.Shopping;
                }
                else if (lowerCommand.Contains("sosyal") || lowerCommand.Contains("medya"))
                {
                    category = WebsiteRegistry.WebsiteCategory.SocialMedia;
                }
                else if (lowerCommand.Contains("arama") || lowerCommand.Contains("search"))
                {
                    category = WebsiteRegistry.WebsiteCategory.Search;
                }
                else if (lowerCommand.Contains("film") || lowerCommand.Contains("dizi") || 
                         lowerCommand.Contains("müzik") || lowerCommand.Contains("eğlence"))
                {
                    category = WebsiteRegistry.WebsiteCategory.Entertainment;
                }
                else if (lowerCommand.Contains("banka") || lowerCommand.Contains("finans"))
                {
                    category = WebsiteRegistry.WebsiteCategory.Banking;
                }
                else if (lowerCommand.Contains("devlet") || lowerCommand.Contains("kamu"))
                {
                    category = WebsiteRegistry.WebsiteCategory.Government;
                }

                if (category.HasValue)
                {
                    var defaultSite = WebsiteRegistry.GetDefaultSiteForCategory(category.Value);
                    if (defaultSite != null)
                    {
                        Debug.WriteLine($"[OpenWebsiteCommand] Kategori varsayılanı açılıyor: {defaultSite.Name}");
                        
                        TextToSpeechService.SpeakTextAsync($"{defaultSite.Name} açılıyor").Wait();

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = defaultSite.Url,
                            UseShellExecute = true
                        });

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenWebsiteCommand] Kategori açma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verilen metin bir web sitesi açma komutu mu kontrol eder
        /// </summary>
        public static bool IsWebsiteCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var lowerText = text.ToLowerInvariant();

            // Web sitesi açma pattern'leri
            var patterns = new[]
            {
                "web sitesini aç",
                "web sitesi aç",
                "sitesini aç",
                "sitesi aç",
                "sayfasını aç",
                "sayfası aç",
                ".com aç",
                ".com.tr aç",
                ".org aç",
                ".net aç"
            };

            // Pattern kontrolü
            foreach (var pattern in patterns)
            {
                if (lowerText.Contains(pattern))
                    return true;
            }

            // Popüler site adları ile direkt "aç" komutu
            var popularSites = new[]
            {
                "google", "youtube", "facebook", "instagram", "twitter",
                "hürriyet", "milliyet", "sabah", "sözcü",
                "trendyol", "hepsiburada", "n11", 
                "netflix", "spotify", "whatsapp",
                "e-devlet", "edevlet"
            };

            foreach (var site in popularSites)
            {
                if (lowerText.Contains(site) && lowerText.Contains("aç"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Komut metninden site adını çıkarır
        /// </summary>
        public static string ExtractSiteName(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return null;

            var text = commandText.ToLowerInvariant();

            // Gereksiz kelimeleri temizle
            var removeWords = new[]
            {
                "web sitesini aç", "web sitesi aç", "sitesini aç", "sitesi aç",
                "sayfasını aç", "sayfası aç", "web", "internet", "online",
                "lütfen", "hemen", "şimdi", "bana", "için", "adresini",
                ".com aç", ".com.tr aç", ".org aç", ".net aç", "aç"
            };

            foreach (var word in removeWords)
            {
                text = text.Replace(word, " ");
            }

            // Fazla boşlukları temizle
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            // Ses tanıma düzeltmeleri - yaygın boşluk hatalarını birleştir
            text = ApplySpeechRecognitionCorrections(text);

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// Ses tanıma sisteminin yaygın hatalarını düzeltir
        /// </summary>
        private static string ApplySpeechRecognitionCorrections(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Bilinen site isimlerinin boşluklu hallerini düzelt
            var corrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "oda tv", "odatv" },
                { "oda televizyon", "odatv" },
                { "hepsi burada", "hepsiburada" },
                { "hepsi burda", "hepsiburada" },
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
                { "whats app", "whatsapp" }
            };

            foreach (var correction in corrections)
            {
                if (text.Contains(correction.Key))
                {
                    text = text.Replace(correction.Key, correction.Value);
                }
            }

            return text;
        }
    }
}