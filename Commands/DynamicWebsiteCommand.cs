using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Dinamik olarak web sitelerini bulan ve açan komut sınıfı
    /// Önceden tanımlı site listesi yerine gerçek zamanlı arama yapar
    /// </summary>
    public class DynamicWebsiteCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _siteName;

        /// <summary>
        /// Dinamik web sitesi açma komutu oluşturur
        /// </summary>
        public DynamicWebsiteCommand(string commandText, string siteName)
        {
            CommandText = commandText;
            _siteName = siteName;
        }

        /// <summary>
        /// Komutu çalıştırır - web sitesini dinamik olarak bulup açar
        /// </summary>
        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[DynamicWebsiteCommand] Web sitesi aranıyor: {_siteName}");

                // Ses geri bildirimi
                await TextToSpeechService.SpeakTextAsync($"{_siteName} web sitesi aranıyor");

                // Web sitesini dinamik olarak bul
                var url = await WebSearchService.FindWebsiteUrlAsync(_siteName);

                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Site bulunamadı: {_siteName}");
                    
                    // Site bulunamadı mesajı
                    await TextToSpeechService.SpeakTextAsync($"{_siteName} web sitesi bulunamadı. İnternet bağlantınızı kontrol edin veya site adını doğru söylediğinizden emin olun.");
                    
                    // Öneriler sun
                    var suggestions = SiteHistoryManager.GetSuggestions(_siteName, 3);
                    if (suggestions.Count > 0)
                    {
                        var suggestionNames = string.Join(", ", suggestions.ConvertAll(s => s.Name));
                        await TextToSpeechService.SpeakTextAsync($"Şunları mı demek istediniz: {suggestionNames}");
                    }
                    
                    return false;
                }

                // Web sitesini aç
                Debug.WriteLine($"[DynamicWebsiteCommand] Açılıyor: {url}");

                // Ses geri bildirimi
                await TextToSpeechService.SpeakTextAsync($"{_siteName} açılıyor");

                // Tarayıcıda aç
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                Debug.WriteLine($"[DynamicWebsiteCommand] {_siteName} başarıyla açıldı: {url}");

                // Tab tracking bildirimi (browser extension için)
                try
                {
                    var browserService = Infrastructure.ServiceContainer.GetOptionalService<BrowserIntegrationService>();
                    if (browserService != null)
                    {
                        await browserService.NotifyTabOpenedAsync(_siteName, url);
                        Debug.WriteLine($"[DynamicWebsiteCommand] Tab tracking bildirimi gönderildi: {_siteName}");
                    }
                }
                catch (Exception tabEx)
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Tab tracking hatası: {tabEx.Message}");
                    // Hata olsa bile devam et
                }

                // Geçmişe kaydet (WebSearchService zaten kaydediyor ama emin olmak için)
                await SiteHistoryManager.SaveToHistoryAsync(_siteName, url);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DynamicWebsiteCommand] Hata: {ex.Message}");
                
                await TextToSpeechService.SpeakTextAsync("Web sitesi açılırken bir hata oluştu");
                
                return false;
            }
        }

        /// <summary>
        /// Verilen metin bir web sitesi açma komutu mu kontrol eder
        /// KATILIMLI PATTERN KONTROLÜ: "aç" kelimesi zorunlu + 2 şarttan biri gerekli
        /// </summary>
        public static bool IsWebsiteCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var lowerText = text.ToLowerInvariant().Trim();

            // ÖNCELİK 1: "aç" kelimesi ZORUNLU - yoksa kesinlikle website komutu değil
            if (!lowerText.Contains("aç"))
            {
                Debug.WriteLine($"[DynamicWebsiteCommand] 'aç' kelimesi yok, website komutu DEĞİL: {text}");
                return false;
            }

            // ÖNCELİK 2: Windows özel klasörü blacklist kontrolü
            // Bu klasörler asla website olarak algılanmamalı
            var systemFolderBlacklist = new[]
            {
                "bilgisayarımı", "bilgisayarım", "bilgisayarimi", "bilgisayarim",
                "belgelerim", "belgeler", "belge",
                "resimlerim", "resimler", "resim",
                "müziğim", "müzik", "muziğim", "muzik",
                "videolarım", "videolar", "video", "videolarim",
                "indirilenler", "downloads", "indirilenleri",
                "masaüstü", "masaüstünü", "masaustu", "desktop"
            };

            foreach (var folder in systemFolderBlacklist)
            {
                if (lowerText.Contains(folder))
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Sistem klasörü algılandı: {folder}, website komutu DEĞİL");
                    return false;
                }
            }

            // ÖNCELİK 3: Sistem uygulamaları blacklist kontrolü
            var systemAppBlacklist = new[]
            {
                "dosya", "klasör", "pencere", "uygulama", "program",
                "ayarlar", "ayarları", "takvim", "takvimi",
                "mail", "e-posta", "e posta", "outlook", "word", "excel", "powerpoint",
                "not defteri", "notepad", "hesap makinesi", "calculator", "paint",
                "cmd", "terminal", "powershell", "görev yöneticisi", "task manager"
            };

            foreach (var app in systemAppBlacklist)
            {
                if (lowerText.Contains(app))
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Sistem uygulaması algılandı: {app}, website komutu DEĞİL");
                    return false;
                }
            }

            // ÖNCELİK 4: Pattern 1 - "web sitesini aç" veya varyantları
            var webSitePatterns = new[]
            {
                "web sitesini aç", "web sitesi aç",
                "veb sitesini aç", "veb sitesi aç",
                "websitesini aç", "websitesi aç",
                "wep sitesini aç", "wep sitesi aç" // Dikte hataları için
            };

            foreach (var pattern in webSitePatterns)
            {
                if (lowerText.Contains(pattern))
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Website pattern algılandı: {pattern}");
                    return true;
                }
            }

            // ÖNCELİK 5: Pattern 2 - Domain uzantısı + "aç"
            var domainExtensions = new[]
            {
                " com aç", " com tr aç", " com.tr aç",
                " org aç", " org tr aç", " org.tr aç",
                " net aç", " net tr aç", " net.tr aç",
                " io aç", " dev aç", " co aç", " tv aç",
                " gov aç", " gov tr aç", " edu aç", " edu tr aç",
                ".com aç", ".com.tr aç", ".org aç", ".net aç",
                ".io aç", ".dev aç", ".co aç", ".tv aç"
            };

            foreach (var extension in domainExtensions)
            {
                if (lowerText.Contains(extension))
                {
                    Debug.WriteLine($"[DynamicWebsiteCommand] Domain uzantısı algılandı: {extension}");
                    return true;
                }
            }

            // Hiçbir pattern uymazsa kesinlikle website komutu DEĞİL
            Debug.WriteLine($"[DynamicWebsiteCommand] Hiçbir website pattern'i eşleşmedi, website komutu DEĞİL: {text}");
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
            var originalText = text;

            // Önce "web sitesini aç" ve benzeri kalıpları bul
            var websitePatterns = new[]
            {
                "web sitesini aç", "web sitesi aç", "websitesini aç", "websitesi aç",
                "sitesini aç", "sitesi aç", "sayfasını aç", "sayfası aç"
            };

            string siteName = null;

            // Her pattern için kontrol et
            foreach (var pattern in websitePatterns)
            {
                if (text.Contains(pattern))
                {
                    // Pattern'den önceki kısmı al (site adı burada)
                    var index = text.IndexOf(pattern);
                    if (index > 0)
                    {
                        siteName = text.Substring(0, index).Trim();
                        break;
                    }
                }
            }

            // Eğer pattern bulunamadıysa, alternatif yöntem
            if (string.IsNullOrWhiteSpace(siteName))
            {
                // Gereksiz kelimeleri temizle
                var removeWords = new[]
                {
                    "web sitesini aç", "web sitesi aç", "websitesini aç", "websitesi aç",
                    "sitesini aç", "sitesi aç", "sayfasını aç", "sayfası aç",
                    "web", "internet", "online", "www",
                    "lütfen", "hemen", "şimdi", "bana", "için", "adresini",
                    ".com aç", ".com.tr aç", ".org aç", ".net aç", ".gov aç", ".edu aç",
                    " aç"
                };

                // En uzun pattern'den başlayarak temizle
                foreach (var word in removeWords.OrderByDescending(w => w.Length))
                {
                    text = text.Replace(word, " ");
                }

                siteName = text;
            }

            // Fazla boşlukları temizle
            siteName = System.Text.RegularExpressions.Regex.Replace(siteName, @"\s+", " ").Trim();

            // Eğer boş kaldıysa null döndür
            if (string.IsNullOrWhiteSpace(siteName))
                return null;

            // Boşluklu kelimeleri birleştir (oda tv -> odatv, hepsi burada -> hepsiburada)
            siteName = NormalizeWebsiteName(siteName);

            return siteName;
        }

        /// <summary>
        /// Web sitesi adını normalize eder (boşlukları kaldırır, yaygın hataları düzeltir)
        /// </summary>
        private static string NormalizeWebsiteName(string siteName)
        {
            if (string.IsNullOrWhiteSpace(siteName))
                return siteName;

            // Bilinen site isimlerinin boşluklu hallerini düzelt
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

            // Önce bilinen düzeltmeleri kontrol et
            foreach (var correction in knownCorrections)
            {
                if (siteName.Equals(correction.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return correction.Value;
                }
            }

            // AKILLI TLD İŞLEME
            // "sozcu com tr" → "sozcu.com.tr"
            // "hurriyet com" → "hurriyet.com"
            siteName = SmartProcessTLD(siteName);

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

            // TLD bulunamadıysa, boşlukları kaldırarak birleştir
            if (detectedTlds.Count == 0)
            {
                // Eğer 2 kelime varsa, boşluğu kaldır
                if (parts.Length == 2)
                {
                    return string.Concat(parts);
                }
                return siteName;
            }

            // Ana site adını al (TLD öncesi kısım)
            string baseName = string.Join("", parts.Take(baseNameEndIndex));

            // TLD'leri nokta ile birleştir
            string finalTld = string.Join(".", detectedTlds);

            // Sonucu oluştur: "sozcu" + "." + "com.tr"
            return $"{baseName}.{finalTld}";
        }

        /// <summary>
        /// En çok kullanılan siteleri gösterir
        /// </summary>
        public static async Task ShowTopSitesAsync()
        {
            var topSites = SiteHistoryManager.GetTopSites(5);
            if (topSites.Count > 0)
            {
                var message = "En çok kullanılan siteler: ";
                foreach (var site in topSites)
                {
                    message += $"{site.Name} ({site.AccessCount} kez), ";
                }
                
                await TextToSpeechService.SpeakTextAsync(message.TrimEnd(',', ' '));
            }
            else
            {
                await TextToSpeechService.SpeakTextAsync("Henüz site geçmişi yok");
            }
        }

        /// <summary>
        /// Son kullanılan siteleri gösterir
        /// </summary>
        public static async Task ShowRecentSitesAsync()
        {
            var recentSites = SiteHistoryManager.GetRecentSites(5);
            if (recentSites.Count > 0)
            {
                var message = "Son kullanılan siteler: ";
                foreach (var site in recentSites)
                {
                    message += $"{site.Name}, ";
                }
                
                await TextToSpeechService.SpeakTextAsync(message.TrimEnd(',', ' '));
            }
            else
            {
                await TextToSpeechService.SpeakTextAsync("Henüz site geçmişi yok");
            }
        }
    }
}