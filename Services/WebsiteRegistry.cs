using System;
using System.Collections.Generic;
using System.Linq;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Popüler web sitelerinin kayıt ve yönetim servisi
    /// </summary>
    public class WebsiteRegistry
    {
        // Web sitesi bilgilerini tutan sınıf
        public class WebsiteInfo
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string[] Aliases { get; set; }
            public string[] Keywords { get; set; }
            public WebsiteCategory Category { get; set; }
        }

        // Web sitesi kategorileri
        public enum WebsiteCategory
        {
            News,           // Haber siteleri
            Shopping,       // Alışveriş siteleri
            SocialMedia,    // Sosyal medya
            Search,         // Arama motorları
            Entertainment,  // Eğlence
            Banking,        // Bankacılık
            Government,     // Devlet kurumları
            Education,      // Eğitim
            Technology,     // Teknoloji
            Sports          // Spor
        }

        // Popüler web siteleri veritabanı
        private static readonly Dictionary<string, WebsiteInfo> _websites = new Dictionary<string, WebsiteInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // HABER SİTELERİ
            { "hürriyet", new WebsiteInfo 
                { 
                    Name = "Hürriyet", 
                    Url = "https://www.hurriyet.com.tr",
                    Aliases = new[] { "hurriyet", "hüriyet", "huriyet" },
                    Keywords = new[] { "haber", "gazete", "gündem" },
                    Category = WebsiteCategory.News
                } 
            },
            { "milliyet", new WebsiteInfo 
                { 
                    Name = "Milliyet", 
                    Url = "https://www.milliyet.com.tr",
                    Aliases = new[] { "miliyet", "millyet" },
                    Keywords = new[] { "haber", "gazete" },
                    Category = WebsiteCategory.News
                } 
            },
            { "sabah", new WebsiteInfo 
                { 
                    Name = "Sabah", 
                    Url = "https://www.sabah.com.tr",
                    Aliases = new[] { "sabah gazetesi" },
                    Keywords = new[] { "haber", "gazete" },
                    Category = WebsiteCategory.News
                } 
            },
            { "sözcü", new WebsiteInfo 
                { 
                    Name = "Sözcü", 
                    Url = "https://www.sozcu.com.tr",
                    Aliases = new[] { "sozcu", "sozcü", "sözcu" },
                    Keywords = new[] { "haber", "gazete" },
                    Category = WebsiteCategory.News
                } 
            },
            { "habertürk", new WebsiteInfo 
                { 
                    Name = "Habertürk", 
                    Url = "https://www.haberturk.com",
                    Aliases = new[] { "haberturk", "haber türk", "haber turk" },
                    Keywords = new[] { "haber", "son dakika" },
                    Category = WebsiteCategory.News
                } 
            },
            { "cnn türk", new WebsiteInfo 
                { 
                    Name = "CNN Türk", 
                    Url = "https://www.cnnturk.com",
                    Aliases = new[] { "cnnturk", "cnn turk", "cnn" },
                    Keywords = new[] { "haber", "son dakika" },
                    Category = WebsiteCategory.News
                } 
            },
            { "ntv", new WebsiteInfo 
                { 
                    Name = "NTV", 
                    Url = "https://www.ntv.com.tr",
                    Aliases = new[] { "ntv haber" },
                    Keywords = new[] { "haber", "son dakika" },
                    Category = WebsiteCategory.News
                } 
            },
            { "ensonhaber", new WebsiteInfo 
                { 
                    Name = "Ensonhaber", 
                    Url = "https://www.ensonhaber.com",
                    Aliases = new[] { "en son haber", "enson haber" },
                    Keywords = new[] { "haber", "son dakika" },
                    Category = WebsiteCategory.News
                } 
            },
            { "mynet", new WebsiteInfo
                {
                    Name = "Mynet",
                    Url = "https://www.mynet.com",
                    Aliases = new[] { "my net", "maynet" },
                    Keywords = new[] { "haber", "portal" },
                    Category = WebsiteCategory.News
                }
            },
            { "odatv", new WebsiteInfo
                {
                    Name = "OdaTV",
                    Url = "https://www.odatv4.com",
                    Aliases = new[] { "oda tv", "odatv4", "oda televizyonu", "odatelevizyon" },
                    Keywords = new[] { "haber", "gündem", "politika" },
                    Category = WebsiteCategory.News
                }
            },

            // ALIŞVERİŞ SİTELERİ
            { "trendyol", new WebsiteInfo 
                { 
                    Name = "Trendyol", 
                    Url = "https://www.trendyol.com",
                    Aliases = new[] { "trend yol", "trendiol" },
                    Keywords = new[] { "alışveriş", "moda", "online" },
                    Category = WebsiteCategory.Shopping
                } 
            },
            { "hepsiburada", new WebsiteInfo
                {
                    Name = "Hepsiburada",
                    Url = "https://www.hepsiburada.com",
                    Aliases = new[] { "hepsi burada", "hb", "hepsiburda", "hepsi burda", "hepsiburadacom" },
                    Keywords = new[] { "alışveriş", "online", "market" },
                    Category = WebsiteCategory.Shopping
                }
            },
            { "n11", new WebsiteInfo 
                { 
                    Name = "N11", 
                    Url = "https://www.n11.com",
                    Aliases = new[] { "n 11", "en11" },
                    Keywords = new[] { "alışveriş", "online" },
                    Category = WebsiteCategory.Shopping
                } 
            },
            { "gittigidiyor", new WebsiteInfo 
                { 
                    Name = "GittiGidiyor", 
                    Url = "https://www.gittigidiyor.com",
                    Aliases = new[] { "gitti gidiyor", "gg" },
                    Keywords = new[] { "alışveriş", "açık artırma" },
                    Category = WebsiteCategory.Shopping
                } 
            },
            { "amazon", new WebsiteInfo 
                { 
                    Name = "Amazon", 
                    Url = "https://www.amazon.com.tr",
                    Aliases = new[] { "amazon türkiye", "amazon tr" },
                    Keywords = new[] { "alışveriş", "online" },
                    Category = WebsiteCategory.Shopping
                } 
            },

            // SOSYAL MEDYA
            { "youtube", new WebsiteInfo 
                { 
                    Name = "YouTube", 
                    Url = "https://www.youtube.com",
                    Aliases = new[] { "you tube", "yutup", "youtub" },
                    Keywords = new[] { "video", "müzik", "sosyal" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },
            { "instagram", new WebsiteInfo 
                { 
                    Name = "Instagram", 
                    Url = "https://www.instagram.com",
                    Aliases = new[] { "insta", "ig" },
                    Keywords = new[] { "sosyal", "fotoğraf" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },
            { "twitter", new WebsiteInfo 
                { 
                    Name = "Twitter/X", 
                    Url = "https://www.twitter.com",
                    Aliases = new[] { "x", "x.com", "tvitter" },
                    Keywords = new[] { "sosyal", "haber", "gündem" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },
            { "facebook", new WebsiteInfo 
                { 
                    Name = "Facebook", 
                    Url = "https://www.facebook.com",
                    Aliases = new[] { "face", "fb", "feysbuk" },
                    Keywords = new[] { "sosyal", "arkadaş" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },
            { "whatsapp", new WebsiteInfo 
                { 
                    Name = "WhatsApp Web", 
                    Url = "https://web.whatsapp.com",
                    Aliases = new[] { "whatsapp web", "wp", "watsap" },
                    Keywords = new[] { "mesaj", "sohbet" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },
            { "linkedin", new WebsiteInfo 
                { 
                    Name = "LinkedIn", 
                    Url = "https://www.linkedin.com",
                    Aliases = new[] { "linked in", "linktin" },
                    Keywords = new[] { "iş", "kariyer", "profesyonel" },
                    Category = WebsiteCategory.SocialMedia
                } 
            },

            // ARAMA MOTORLARI
            { "google", new WebsiteInfo 
                { 
                    Name = "Google", 
                    Url = "https://www.google.com.tr",
                    Aliases = new[] { "gugıl", "gugl" },
                    Keywords = new[] { "arama", "search" },
                    Category = WebsiteCategory.Search
                } 
            },
            { "yandex", new WebsiteInfo 
                { 
                    Name = "Yandex", 
                    Url = "https://www.yandex.com.tr",
                    Aliases = new[] { "yandeks" },
                    Keywords = new[] { "arama", "rusya" },
                    Category = WebsiteCategory.Search
                } 
            },
            { "bing", new WebsiteInfo 
                { 
                    Name = "Bing", 
                    Url = "https://www.bing.com",
                    Aliases = new[] { "microsoft bing" },
                    Keywords = new[] { "arama", "microsoft" },
                    Category = WebsiteCategory.Search
                } 
            },

            // EĞLENCE
            { "netflix", new WebsiteInfo 
                { 
                    Name = "Netflix", 
                    Url = "https://www.netflix.com",
                    Aliases = new[] { "netfliks" },
                    Keywords = new[] { "film", "dizi", "eğlence" },
                    Category = WebsiteCategory.Entertainment
                } 
            },
            { "spotify", new WebsiteInfo 
                { 
                    Name = "Spotify", 
                    Url = "https://open.spotify.com",
                    Aliases = new[] { "spotifay" },
                    Keywords = new[] { "müzik", "podcast" },
                    Category = WebsiteCategory.Entertainment
                } 
            },
            { "twitch", new WebsiteInfo 
                { 
                    Name = "Twitch", 
                    Url = "https://www.twitch.tv",
                    Aliases = new[] { "twich", "tviç" },
                    Keywords = new[] { "yayın", "oyun", "stream" },
                    Category = WebsiteCategory.Entertainment
                } 
            },

            // DEVLET KURUMLARI
            { "e-devlet", new WebsiteInfo 
                { 
                    Name = "e-Devlet", 
                    Url = "https://www.turkiye.gov.tr",
                    Aliases = new[] { "edevlet", "turkiye.gov", "e devlet" },
                    Keywords = new[] { "devlet", "kamu", "hizmet" },
                    Category = WebsiteCategory.Government
                } 
            },
            { "sgk", new WebsiteInfo 
                { 
                    Name = "SGK", 
                    Url = "https://www.sgk.gov.tr",
                    Aliases = new[] { "sosyal güvenlik" },
                    Keywords = new[] { "sigorta", "emeklilik" },
                    Category = WebsiteCategory.Government
                } 
            },
            { "gib", new WebsiteInfo 
                { 
                    Name = "GİB", 
                    Url = "https://www.gib.gov.tr",
                    Aliases = new[] { "gelir idaresi", "vergi dairesi" },
                    Keywords = new[] { "vergi", "maliye" },
                    Category = WebsiteCategory.Government
                } 
            },

            // BANKACILIK
            { "garanti", new WebsiteInfo 
                { 
                    Name = "Garanti BBVA", 
                    Url = "https://www.garantibbva.com.tr",
                    Aliases = new[] { "garanti bankası", "garanti bbva" },
                    Keywords = new[] { "banka", "finans" },
                    Category = WebsiteCategory.Banking
                } 
            },
            { "akbank", new WebsiteInfo 
                { 
                    Name = "Akbank", 
                    Url = "https://www.akbank.com",
                    Aliases = new[] { "ak bank" },
                    Keywords = new[] { "banka", "finans" },
                    Category = WebsiteCategory.Banking
                } 
            },
            { "iş bankası", new WebsiteInfo 
                { 
                    Name = "İş Bankası", 
                    Url = "https://www.isbank.com.tr",
                    Aliases = new[] { "isbank", "is bankasi", "işbank" },
                    Keywords = new[] { "banka", "finans" },
                    Category = WebsiteCategory.Banking
                } 
            },
            { "yapı kredi", new WebsiteInfo 
                { 
                    Name = "Yapı Kredi", 
                    Url = "https://www.yapikredi.com.tr",
                    Aliases = new[] { "yapıkredi", "yapi kredi", "ykb" },
                    Keywords = new[] { "banka", "finans" },
                    Category = WebsiteCategory.Banking
                } 
            },
            { "ziraat", new WebsiteInfo 
                { 
                    Name = "Ziraat Bankası", 
                    Url = "https://www.ziraatbank.com.tr",
                    Aliases = new[] { "ziraat bankası", "ziraat bank" },
                    Keywords = new[] { "banka", "finans", "devlet" },
                    Category = WebsiteCategory.Banking
                } 
            }
        };

        /// <summary>
        /// Verilen site adı veya komut için en uygun web sitesini bulur
        /// </summary>
        public static WebsiteInfo FindWebsite(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = NormalizeTurkishCharacters(input.ToLowerInvariant().Trim());

            // 1. Direkt eşleşme kontrolü
            if (_websites.TryGetValue(input, out var exactMatch))
                return exactMatch;

            // 2. Alias kontrolü
            foreach (var site in _websites.Values)
            {
                if (site.Aliases?.Any(alias => 
                    NormalizeTurkishCharacters(alias.ToLowerInvariant()) == input) == true)
                {
                    return site;
                }
            }

            // 3. Kısmi eşleşme kontrolü (site adı içinde geçiyor mu)
            foreach (var kvp in _websites)
            {
                if (input.Contains(NormalizeTurkishCharacters(kvp.Key.ToLowerInvariant())) ||
                    NormalizeTurkishCharacters(kvp.Key.ToLowerInvariant()).Contains(input))
                {
                    return kvp.Value;
                }
            }

            // 4. Keyword eşleşmesi
            foreach (var site in _websites.Values)
            {
                if (site.Keywords?.Any(keyword => 
                    input.Contains(NormalizeTurkishCharacters(keyword.ToLowerInvariant()))) == true)
                {
                    return site;
                }
            }

            // 5. Fuzzy matching - en yakın eşleşmeyi bul
            return FindBestFuzzyMatch(input);
        }

        /// <summary>
        /// Kategori bazlı varsayılan site döndürür
        /// </summary>
        public static WebsiteInfo GetDefaultSiteForCategory(WebsiteCategory category)
        {
            return category switch
            {
                WebsiteCategory.News => _websites["hürriyet"],
                WebsiteCategory.Shopping => _websites["trendyol"],
                WebsiteCategory.SocialMedia => _websites["youtube"],
                WebsiteCategory.Search => _websites["google"],
                WebsiteCategory.Entertainment => _websites["netflix"],
                WebsiteCategory.Banking => _websites["garanti"],
                WebsiteCategory.Government => _websites["e-devlet"],
                _ => null
            };
        }

        /// <summary>
        /// Türkçe karakterleri normalize eder
        /// </summary>
        private static string NormalizeTurkishCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Replace("Ğ", "G")
                .Replace("Ü", "U")
                .Replace("Ş", "S")
                .Replace("İ", "I")
                .Replace("Ö", "O")
                .Replace("Ç", "C");
        }

        /// <summary>
        /// Levenshtein mesafesi kullanarak en yakın eşleşmeyi bulur
        /// </summary>
        private static WebsiteInfo FindBestFuzzyMatch(string input)
        {
            WebsiteInfo bestMatch = null;
            double bestScore = 0.0;

            foreach (var kvp in _websites)
            {
                var score = CalculateSimilarity(input, 
                    NormalizeTurkishCharacters(kvp.Key.ToLowerInvariant()));
                
                if (score > bestScore && score > 0.6) // %60 eşik değeri
                {
                    bestScore = score;
                    bestMatch = kvp.Value;
                }

                // Alias'ları da kontrol et
                if (kvp.Value.Aliases != null)
                {
                    foreach (var alias in kvp.Value.Aliases)
                    {
                        score = CalculateSimilarity(input, 
                            NormalizeTurkishCharacters(alias.ToLowerInvariant()));
                        
                        if (score > bestScore && score > 0.6)
                        {
                            bestScore = score;
                            bestMatch = kvp.Value;
                        }
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// İki string arasındaki benzerlik oranını hesaplar
        /// </summary>
        private static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            if (source == target)
                return 1.0;

            int stepsToSame = ComputeLevenshteinDistance(source, target);
            return 1.0 - (double)stepsToSame / Math.Max(source.Length, target.Length);
        }

        /// <summary>
        /// Levenshtein mesafesini hesaplar
        /// </summary>
        private static int ComputeLevenshteinDistance(string source, string target)
        {
            int sourceLength = source.Length;
            int targetLength = target.Length;
            int[,] distance = new int[sourceLength + 1, targetLength + 1];

            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) ;
            for (int j = 0; j <= targetLength; distance[0, j] = j++) ;

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceLength, targetLength];
        }
    }
}