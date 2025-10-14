using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QuadroAIPilot.Models.AI;

namespace QuadroAIPilot.Services.AI
{
    /// <summary>
    /// Komut kalıpları ve pattern matching
    /// </summary>
    public class IntentPatterns
    {
        private readonly List<IntentPattern> _patterns;
        private readonly SynonymDictionary _synonyms;
        
        public IntentPatterns(SynonymDictionary synonymDictionary)
        {
            _synonyms = synonymDictionary;
            _patterns = new List<IntentPattern>();
            InitializePatterns();
        }
        
        private void InitializePatterns()
        {
            // ÖNCELİKLİ: Sistem kontrolleri - en üst öncelik
            // Ses kontrolleri
            AddPattern(@"^(ses|sesi|volume|volüm)\s*(aç|kapat|artır|arttır|azalt|kıs)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(ses|sesi|volume|volüm)\s+(seviyesini|seviyeyi)\s*(artır|arttır|azalt|kıs)$", 
                IntentType.SystemControl, 
                new[] { "target", "modifier", "action" });
                
            // Pencere ve sekme kontrolleri
            AddPattern(@"^(pencereyi|pencere)\s*(kapat|kapa)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(sekmeyi|sekme)\s*(kapat|kapa)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(uygulama|uygulamayı)\s*(kapat|kapa)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Klasör açma komutları
            AddPattern(@"^(belgeler|belgelerim|resimler|resimlerim|müzik|müziğim|videolar|videolarım|indirilenler|masaüstü|desktop|downloads)\s*(aç|göster)?$", 
                IntentType.SystemControl, 
                new[] { "folder", "action" });
                
            AddPattern(@"^(dosya gezgini|dosya gezginini|file explorer)\s*(aç|göster)?$", 
                IntentType.SystemControl, 
                new[] { "app", "action" });
                
            // Outlook özel komutları
            AddPattern(@"^(takvim|takvimi|calendar)\s*(aç|göster)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Caps Lock ve diğer tuşlar
            AddPattern(@"^(caps lock|capslock)\s*(aç|kapat|değiştir)?$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Pencere kontrolleri
            AddPattern(@"^(çalıştır penceresi|çalıştır penceresini)\s*(aç|göster)?$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(görev görünümü|görev görünümünü)\s*(aç|göster)?$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Sayfa navigasyon komutları
            AddPattern(@"^(sayfa başına|sayfa sonuna|başa|sona)\s*(git|gel|gi)?$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Sayfa navigasyon - alternatif pattern'ler (yazım hataları için)
            AddPattern(@"^sayfa\s+başına\s*gi.*$", 
                IntentType.SystemControl, 
                new[] { "command" });
                
            AddPattern(@"^sayfa\s+sonuna\s*gi.*$", 
                IntentType.SystemControl, 
                new[] { "command" });
                
            // Mod değiştirme komutları
            AddPattern(@"^(yazı moduna|komut moduna|okuma moduna)\s*(geç|git)$", 
                IntentType.SystemControl, 
                new[] { "mode", "action" });
                
            // Uygulama açma kalıpları - sistem komutlarını hariç tut
            AddPattern(@"^(?!ses|sesi|volume|volüm|pencere|pencereyi|sekme|sekmeyi|uygulama|uygulamayı|belgeler|belgelerim|resimler|resimlerim|müzik|müziğim|videolar|videolarım|indirilenler|masaüstü|desktop|downloads|dosya gezgini|takvim|takvimi|calendar|caps lock|capslock|çalıştır penceresi|görev görünümü)(.+?)\s*(aç|başlat|çalıştır|getir|göster)$", 
                IntentType.OpenApplication, 
                new[] { "app", "action" });
                
            AddPattern(@"^(aç|başlat|çalıştır)\s+(?!ses|sesi|volume|volüm|pencere|pencereyi|sekme|sekmeyi|uygulama|uygulamayı|belgeler|belgelerim|resimler|resimlerim|müzik|müziğim|videolar|videolarım|indirilenler|masaüstü|desktop|downloads|dosya gezgini|takvim|takvimi|calendar|caps lock|capslock|çalıştır penceresi|görev görünümü)(.+)$", 
                IntentType.OpenApplication, 
                new[] { "action", "app" });
                
            // Uygulama kapatma kalıpları - sistem komutlarını hariç tut
            AddPattern(@"^(?!ses|sesi|volume|volüm|pencere|pencereyi|sekme|sekmeyi|uygulama|uygulamayı)(.+?)\s*(kapat|kapa|sonlandır|bitir)$", 
                IntentType.CloseApplication, 
                new[] { "app", "action" });
                
            AddPattern(@"^(kapat|kapa|sonlandır)\s+(?!ses|sesi|volume|volüm|pencere|pencereyi|sekme|sekmeyi|uygulama|uygulamayı)(.+)$", 
                IntentType.CloseApplication, 
                new[] { "action", "app" });
                
            // Dosya işlemleri
            AddPattern(@"^(.+?)\s+(dosya|file|belge)\s*(ara|bul|aç)$", 
                IntentType.FileOperation, 
                new[] { "filename", "type", "action" });
                
            AddPattern(@"^(dosya|file|belge)\s+(ara|bul)\s+(.+)$", 
                IntentType.FileOperation, 
                new[] { "type", "action", "filename" });
                
            // Email işlemleri
            AddPattern(@"^(mail|email|e-posta|eposta)\s*(oku|göster|listele|kontrol)$", 
                IntentType.EmailOperation, 
                new[] { "type", "action" });
                
            AddPattern(@"^(yeni|taze)\s*(mail|email|e-posta)\s*(yaz|oluştur|gönder)$", 
                IntentType.EmailOperation, 
                new[] { "modifier", "type", "action" });
                
            // Klasör navigasyonu
            AddPattern(@"^(.+?)\s*(klasör|dizin|folder)\s*(aç|git|göster)$", 
                IntentType.FolderNavigation, 
                new[] { "foldername", "type", "action" });
                
            AddPattern(@"^(masaüstü|belgeler|indirilenler|resimler)\s*(aç|git|göster)?$", 
                IntentType.FolderNavigation, 
                new[] { "location", "action" });
                
            // Diğer sistem kontrolleri
            AddPattern(@"^(ekran|monitör)\s*(kilitle|kapat|kitle)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(bilgisayar|sistem)\s*(kapat|yeniden başlat|restart)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(caps lock|capslock)\s*(aç|kapat|değiştir)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            AddPattern(@"^(ekran görüntüsü|screenshot)\s*(al|çek)$", 
                IntentType.SystemControl, 
                new[] { "target", "action" });
                
            // Web arama
            AddPattern(@"^(google|youtube|bing)\s*'?da\s+(.+?)\s*(ara|bul)$", 
                IntentType.WebSearch, 
                new[] { "engine", "query", "action" });
                
            AddPattern(@"^(internet|web)\s*'?de\s+(.+?)\s*(ara|bul)$", 
                IntentType.WebSearch, 
                new[] { "type", "query", "action" });

            // Haber ve bilgi alma kalıpları - fiil bazlı
            AddPattern(@"^(haber|haberler|gündem|son dakika)\s*(oku|göster|getir|listele|neler var)$", 
                IntentType.WebInfoRequest, 
                new[] { "type", "action" });
                
            AddPattern(@"^(haber|haberleri|haberlerini)\s+(oku|göster|getir|listele)$", 
                IntentType.WebInfoRequest, 
                new[] { "type", "action" });
                
            AddPattern(@"^(haberlerde|gündemde)\s+(neler var|ne var)$", 
                IntentType.WebInfoRequest, 
                new[] { "type", "query" });
                
            AddPattern(@"^(en son|son|yeni|bugünkü)\s+(haber|haberler|haberleri)\s*(oku|göster|getir|listele)$", 
                IntentType.WebInfoRequest, 
                new[] { "modifier", "type", "action" });
                
            AddPattern(@"^(teknoloji|spor|ekonomi|finans|sağlık|dünya|magazin|siyaset)\s+(haber|haberlerini|haberleri)\s+(oku|göster|getir|listele)$", 
                IntentType.WebInfoRequest, 
                new[] { "category", "type", "action" });
                
            AddPattern(@"^(teknoloji|spor|ekonomi|finans|sağlık|dünya|magazin|siyaset)\s+(haber|haberlerinde)\s+(neler var|ne var)$", 
                IntentType.WebInfoRequest, 
                new[] { "category", "type", "query" });
                
            AddPattern(@"^(twitter|x)\s+(gündem|trend|trendler)\s*(neler|göster|listele)?$", 
                IntentType.WebInfoRequest, 
                new[] { "platform", "type", "action" });
                
            AddPattern(@"^(.+?)\s+(nedir|kimdir|ne demek|hakkında|açıkla)$", 
                IntentType.WebInfoRequest, 
                new[] { "query", "action" });
                
            AddPattern(@"^(vikipedi|wikipedia)\s*'?da\s+(.+?)\s*(ara|bul|oku)$", 
                IntentType.WebInfoRequest, 
                new[] { "source", "query", "action" });

            // Son haberler özel pattern'i
            AddPattern(@"^(neler oluyor|neler var|gündemde neler var|son haberler neler)$", 
                IntentType.WebInfoRequest, 
                new[] { "general_query" });

            // Çoklu içerik istekleri  
            AddPattern(@"^(haberler ve gündem|gündem ve haberler|son haberler ve trendler)$", 
                IntentType.WebInfoRequest, 
                new[] { "combined_content" });
        }
        
        private void AddPattern(string pattern, IntentType intentType, string[] entityNames)
        {
            _patterns.Add(new IntentPattern
            {
                Pattern = pattern,
                IntentType = intentType,
                EntityNames = entityNames,
                Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
            });
        }
        
        /// <summary>
        /// Metni pattern'lerle eşleştirir
        /// </summary>
        public IntentResult MatchPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return CreateUnknownResult(text);
                
            // Önce synonym normalizasyonu yap
            var normalizedText = _synonyms.NormalizeText(text.Trim());
            
            // Pattern'leri dene
            foreach (var pattern in _patterns)
            {
                var match = pattern.Regex.Match(normalizedText);
                if (match.Success)
                {
                    var entities = ExtractEntities(match, pattern.EntityNames);
                    
                    return new IntentResult
                    {
                        Intent = new Intent(pattern.IntentType, pattern.IntentType.ToString()),
                        Confidence = CalculateConfidence(match, normalizedText),
                        OriginalText = text,
                        ProcessedText = normalizedText,
                        Entities = entities
                    };
                }
            }
            
            // Eğer pattern bulunamazsa, fuzzy matching dene
            return TryFuzzyMatch(text, normalizedText);
        }
        
        /// <summary>
        /// Regex match'inden entity'leri çıkarır
        /// </summary>
        private Dictionary<string, string> ExtractEntities(Match match, string[] entityNames)
        {
            var entities = new Dictionary<string, string>();
            
            // Group 0 tüm match, group 1'den itibaren capture group'lar
            for (int i = 1; i < match.Groups.Count && i <= entityNames.Length; i++)
            {
                if (match.Groups[i].Success)
                {
                    entities[entityNames[i - 1]] = match.Groups[i].Value.Trim();
                }
            }
            
            return entities;
        }
        
        /// <summary>
        /// Güven skorunu hesaplar
        /// </summary>
        private double CalculateConfidence(Match match, string normalizedText)
        {
            // Tam eşleşme = yüksek güven
            if (match.Value.Length == normalizedText.Length)
                return 0.95;
                
            // Kısmi eşleşme = orta güven
            var coverage = (double)match.Value.Length / normalizedText.Length;
            return Math.Min(0.9, 0.5 + (coverage * 0.4));
        }
        
        /// <summary>
        /// Pattern bulunamazsa fuzzy matching dener
        /// </summary>
        private IntentResult TryFuzzyMatch(string originalText, string normalizedText)
        {
            var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Anahtar kelime bazlı basit intent tespiti
            // Sistem kontrol kelimelerini tanımla
            var systemControlKeywords = new[] { "ses", "sesi", "volume", "volüm", "pencere", "pencereyi", 
                "sekme", "sekmeyi", "uygulama", "uygulamayı", "belgeler", "belgelerim", "resimler", 
                "resimlerim", "müzik", "müziğim", "videolar", "videolarım", "indirilenler", 
                "masaüstü", "desktop", "downloads", "dosya", "gezgini", "takvim", "takvimi", 
                "calendar", "caps", "lock", "çalıştır", "görev", "görünümü", "sayfa", "başına", "sonuna" };
            
            // Önce sistem komutlarını kontrol et
            if (ContainsAny(words, systemControlKeywords))
            {
                // Ses komutları
                if (ContainsAny(words, "ses", "sesi", "volume", "volüm") && 
                    ContainsAny(words, "aç", "kapat", "artır", "arttır", "azalt", "kıs"))
                {
                    return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.75);
                }
                
                // Pencere/sekme/uygulama kapatma
                if ((ContainsAny(words, "pencere", "pencereyi", "sekme", "sekmeyi", "uygulama", "uygulamayı") || 
                     (words.Length == 2 && words[0] == "uygulama")) && 
                    ContainsAny(words, "kapat", "kapa"))
                {
                    return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.75);
                }
                
                // Klasör açma
                if (ContainsAny(words, "belgeler", "belgelerim", "resimler", "resimlerim", "müzik", 
                    "müziğim", "videolar", "videolarım", "indirilenler", "masaüstü", "desktop", "downloads") && 
                    (ContainsAny(words, "aç") || words.Length <= 2))
                {
                    return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.75);
                }
                
                // Sayfa navigasyon komutları
                if (ContainsAny(words, "sayfa") && 
                    (ContainsAny(words, "başına", "sonuna", "başa", "sona")) && 
                    (ContainsAny(words, "git", "gel", "gi") || words.Length <= 3))
                {
                    return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.75);
                }
                
                // Diğer sistem komutları
                if ((ContainsAny(words, "caps", "lock") || ContainsAny(words, "çalıştır", "penceresi") || 
                     ContainsAny(words, "görev", "görünümü")))
                {
                    return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.7);
                }
            }
            
            // Uygulama açma - sistem komutları değilse
            if (ContainsAny(words, "aç", "başlat", "çalıştır", "getir") && 
                !ContainsAny(words, systemControlKeywords))
            {
                return CreateResult(IntentType.OpenApplication, originalText, normalizedText, 0.7);
            }
            
            // Uygulama kapatma - sistem komutları değilse
            if (ContainsAny(words, "kapat", "kapa", "sonlandır") && 
                !ContainsAny(words, systemControlKeywords))
            {
                return CreateResult(IntentType.CloseApplication, originalText, normalizedText, 0.7);
            }
            
            if (ContainsAny(words, "mail", "email", "e-posta", "outlook"))
            {
                return CreateResult(IntentType.EmailOperation, originalText, normalizedText, 0.65);
            }
            
            if (ContainsAny(words, "dosya", "file", "belge") && ContainsAny(words, "ara", "bul"))
            {
                return CreateResult(IntentType.FileOperation, originalText, normalizedText, 0.65);
            }
            
            if (ContainsAny(words, "ses", "volume") || ContainsAny(words, "ekran", "kilitle"))
            {
                return CreateResult(IntentType.SystemControl, originalText, normalizedText, 0.6);
            }
            
            // Haber ve web bilgi fuzzy matching
            if (ContainsAny(words, "haber", "haberler", "gündem", "son", "dakika") || 
                ContainsAny(words, "nedir", "kimdir", "ne", "demek", "hakkında") ||
                ContainsAny(words, "twitter", "trend", "gündem", "x") ||
                ContainsAny(words, "vikipedi", "wikipedia"))
            {
                return CreateResult(IntentType.WebInfoRequest, originalText, normalizedText, 0.7);
            }
            
            return CreateUnknownResult(originalText);
        }
        
        private bool ContainsAny(string[] words, params string[] keywords)
        {
            return keywords.Any(k => words.Contains(k, StringComparer.OrdinalIgnoreCase));
        }
        
        private IntentResult CreateResult(IntentType type, string original, string processed, double confidence)
        {
            return new IntentResult
            {
                Intent = new Intent(type, type.ToString()),
                Confidence = confidence,
                OriginalText = original,
                ProcessedText = processed,
                Entities = new Dictionary<string, string>()
            };
        }
        
        private IntentResult CreateUnknownResult(string text)
        {
            return new IntentResult
            {
                Intent = new Intent(IntentType.Unknown, "Unknown"),
                Confidence = 0.0,
                OriginalText = text,
                ProcessedText = text,
                Entities = new Dictionary<string, string>()
            };
        }
        
        /// <summary>
        /// Kullanıcı tanımlı pattern ekler
        /// </summary>
        public void AddCustomPattern(string pattern, IntentType intentType, string[] entityNames)
        {
            AddPattern(pattern, intentType, entityNames);
        }
    }
    
    /// <summary>
    /// Intent pattern tanımı
    /// </summary>
    internal class IntentPattern
    {
        public string Pattern { get; set; }
        public IntentType IntentType { get; set; }
        public string[] EntityNames { get; set; }
        public Regex Regex { get; set; }
    }
}