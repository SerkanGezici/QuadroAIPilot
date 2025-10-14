using System;
using System.Collections.Generic;
using System.Linq;

namespace QuadroAIPilot.Services.AI
{
    /// <summary>
    /// Türkçe eş anlamlı kelimeler sözlüğü
    /// </summary>
    public class SynonymDictionary
    {
        private readonly Dictionary<string, HashSet<string>> _synonymGroups;
        private readonly Dictionary<string, string> _wordToGroupMap;
        
        public SynonymDictionary()
        {
            _synonymGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _wordToGroupMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            InitializeSynonyms();
        }
        
        private void InitializeSynonyms()
        {
            // Uygulama isimleri
            AddSynonymGroup("outlook", "mail", "email", "e-posta", "eposta", "meyil", "posta", "meil");
            AddSynonymGroup("excel", "eksel", "tablo", "hesap tablosu", "spreadsheet");
            AddSynonymGroup("word", "vörd", "kelime", "belge", "doküman", "yazı", "world");
            AddSynonymGroup("powerpoint", "sunum", "slayt", "prezentasyon", "ppt");
            AddSynonymGroup("chrome", "krom", "tarayıcı", "browser", "internet");
            AddSynonymGroup("notepad", "not defteri", "notdefteri", "metin editörü");
            AddSynonymGroup("calculator", "hesap makinesi", "hesaplayıcı", "calc");
            AddSynonymGroup("teams", "tims", "toplantı");
            
            // Eylemler
            AddSynonymGroup("aç", "başlat", "çalıştır", "getir", "göster", "yükle", "open", "start");
            AddSynonymGroup("kapat", "kapa", "sonlandır", "bitir", "durdur", "kıll", "close");
            AddSynonymGroup("ara", "bul", "search", "find", "arat");
            AddSynonymGroup("oku", "göster", "listele", "getir");
            AddSynonymGroup("yaz", "oluştur", "yarat", "ekle", "yeni");
            AddSynonymGroup("sil", "kaldır", "temizle", "remove", "delete");
            AddSynonymGroup("kaydet", "sakla", "save", "kayıt et");
            AddSynonymGroup("gönder", "yolla", "at", "ilet", "send");
            
            // Dosya türleri
            AddSynonymGroup("dosya", "file", "döküman", "belge");
            AddSynonymGroup("klasör", "dizin", "folder", "directory");
            AddSynonymGroup("resim", "görsel", "fotoğraf", "foto", "image", "picture");
            AddSynonymGroup("video", "film", "klip");
            AddSynonymGroup("müzik", "şarkı", "ses", "audio", "mp3");
            AddSynonymGroup("pdf", "pıdıef");
            
            // Zaman ifadeleri
            AddSynonymGroup("bugün", "bu gün");
            AddSynonymGroup("yarın", "ertesi gün");
            AddSynonymGroup("dün", "önceki gün");
            AddSynonymGroup("şimdi", "hemen", "anında");
            AddSynonymGroup("son", "en son", "sonuncu", "last");
            AddSynonymGroup("yeni", "taze", "fresh", "new");
            
            // Sistem komutları
            AddSynonymGroup("ses", "volume", "volüm", "sesi");
            AddSynonymGroup("ekran", "monitör", "display", "görüntü");
            AddSynonymGroup("kilitle", "lock", "kitle");
            AddSynonymGroup("kapat", "shutdown", "şatdown");
            AddSynonymGroup("yeniden başlat", "restart", "reboot", "yeniden", "restrat");
            
            // Konum/Yer
            AddSynonymGroup("masaüstü", "desktop", "masaüstüm");
            AddSynonymGroup("belgeler", "documents", "belgelerim", "dokümanlar");
            AddSynonymGroup("indirilenler", "downloads", "indirilənler", "yüklemeler");
            AddSynonymGroup("resimler", "pictures", "görseller", "fotoğraflar");
            
            // Haber ve medya terimleri
            AddSynonymGroup("haber", "haberler", "news", "yenilik", "gelişme");
            AddSynonymGroup("gündem", "trend", "trendler", "konuşulan", "popüler");
            AddSynonymGroup("son dakika", "breaking", "acil", "şimdi", "anlık");
            AddSynonymGroup("teknoloji", "tech", "teknolojik", "dijital");
            AddSynonymGroup("ekonomi", "finans", "finance", "business", "mali");
            AddSynonymGroup("spor", "sports", "futbol", "basketbol", "atletizm");
            AddSynonymGroup("sağlık", "health", "tıp", "doktor", "hasta");
            AddSynonymGroup("dünya", "world", "uluslararası", "international", "global");
            AddSynonymGroup("magazin", "entertainment", "eğlence", "şov", "star");
            AddSynonymGroup("siyaset", "politics", "politika", "hükümet", "parti");
            
            // Sosyal medya ve web
            AddSynonymGroup("twitter", "x", "tweets", "tweetler");
            AddSynonymGroup("vikipedi", "wikipedia", "ansiklopedi", "bilgi");
            AddSynonymGroup("internet", "web", "online", "çevrimiçi");
            
            // Sorular ve bilgi alma
            AddSynonymGroup("nedir", "ne demek", "tanım", "açıklama", "bilgi");
            AddSynonymGroup("kimdir", "kim", "hakkında", "who");
            AddSynonymGroup("nasıl", "how", "ne şekilde");
            AddSynonymGroup("nerede", "where", "hangi yer");
            AddSynonymGroup("ne zaman", "when", "hangi zaman");
        }
        
        /// <summary>
        /// Eş anlamlı grup ekler
        /// </summary>
        private void AddSynonymGroup(params string[] synonyms)
        {
            if (synonyms.Length < 2) return;
            
            var groupKey = synonyms[0].ToLowerInvariant();
            var synonymSet = new HashSet<string>(synonyms.Select(s => s.ToLowerInvariant()));
            
            _synonymGroups[groupKey] = synonymSet;
            
            // Her kelimeyi grup anahtarına map'le
            foreach (var synonym in synonymSet)
            {
                _wordToGroupMap[synonym] = groupKey;
            }
        }
        
        /// <summary>
        /// Bir kelimenin eş anlamlısını bulur
        /// </summary>
        public string GetCanonicalForm(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return word;
            
            word = word.ToLowerInvariant().Trim();
            
            // Eğer kelime bir gruba aitse, grup anahtarını döndür
            if (_wordToGroupMap.TryGetValue(word, out var canonical))
            {
                return canonical;
            }
            
            return word;
        }
        
        /// <summary>
        /// Bir metindeki tüm kelimeleri canonical formlarına çevirir
        /// </summary>
        public string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var normalizedWords = words.Select(w => GetCanonicalForm(w));
            
            return string.Join(" ", normalizedWords);
        }
        
        /// <summary>
        /// İki kelimenin eş anlamlı olup olmadığını kontrol eder
        /// </summary>
        public bool AreSynonyms(string word1, string word2)
        {
            if (string.IsNullOrWhiteSpace(word1) || string.IsNullOrWhiteSpace(word2))
                return false;
                
            var canonical1 = GetCanonicalForm(word1);
            var canonical2 = GetCanonicalForm(word2);
            
            return canonical1.Equals(canonical2, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Bir kelimenin tüm eş anlamlılarını getirir
        /// </summary>
        public HashSet<string> GetSynonyms(string word)
        {
            var canonical = GetCanonicalForm(word);
            
            if (_synonymGroups.TryGetValue(canonical, out var synonyms))
            {
                return new HashSet<string>(synonyms);
            }
            
            return new HashSet<string> { word };
        }
        
        /// <summary>
        /// Kullanıcı tanımlı eş anlamlı ekler
        /// </summary>
        public void AddCustomSynonym(string word, string synonym)
        {
            var canonical = GetCanonicalForm(word);
            
            if (_synonymGroups.TryGetValue(canonical, out var group))
            {
                group.Add(synonym.ToLowerInvariant());
                _wordToGroupMap[synonym.ToLowerInvariant()] = canonical;
            }
            else
            {
                AddSynonymGroup(word, synonym);
            }
        }
    }
}