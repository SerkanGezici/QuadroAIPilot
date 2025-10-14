using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Sistem komutları için fuzzy matching registry
    /// </summary>
    public class SystemCommandRegistry
    {
        private static SystemCommandRegistry _instance;
        private readonly Dictionary<string, List<string>> _commandAliases;
        private readonly Dictionary<string, string> _commandMappings;
        
        public static SystemCommandRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SystemCommandRegistry();
                }
                return _instance;
            }
        }
        
        private SystemCommandRegistry()
        {
            _commandAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _commandMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            InitializeSystemCommands();
            InitializeFolderCommands();
            InitializeNavigationCommands();
            InitializeOutlookCommands();
            InitializeWebCommands();
            InitializeEdgeTTSCommands();
        }
        
        /// <summary>
        /// Komut araması - önce tam eşleşme, sonra alias, sonra fuzzy matching
        /// </summary>
        public string FindCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            
            input = input.Trim().ToLowerInvariant();
            
            // 1. Tam eşleşme kontrolü
            if (_commandMappings.ContainsKey(input))
            {
                Debug.WriteLine($"[SystemCommandRegistry] Tam eşleşme bulundu: '{input}'");
                return _commandMappings[input];
            }
            
            // 2. Alias kontrolü
            foreach (var kvp in _commandAliases)
            {
                if (kvp.Value.Any(alias => alias.Equals(input, StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"[SystemCommandRegistry] Alias eşleşmesi bulundu: '{input}' -> '{kvp.Key}'");
                    return kvp.Key;
                }
            }
            
            // 3. Fuzzy matching (%80 eşik)
            var bestMatch = _commandMappings.Keys
                .Select(key => new { Key = key, Similarity = CalculateSimilarity(input, key) })
                .Where(x => x.Similarity >= 0.8)
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();
                
            if (bestMatch != null)
            {
                Debug.WriteLine($"[SystemCommandRegistry] Fuzzy match bulundu: '{input}' -> '{bestMatch.Key}' (benzerlik: {bestMatch.Similarity:P})");
                return _commandMappings[bestMatch.Key];
            }
            
            Debug.WriteLine($"[SystemCommandRegistry] Komut bulunamadı: '{input}'");
            return null;
        }
        
        private void InitializeSystemCommands()
        {
            // Kopyala komutu
            _commandMappings["kopyala"] = "kopyala";
            _commandAliases["kopyala"] = new List<string> { "kopyale", "copy", "kopyalar", "kopyalaa", "kopya", "kopyala" };
            
            // Yapıştır komutu
            _commandMappings["yapıştır"] = "yapıştır";
            _commandAliases["yapıştır"] = new List<string> { "yapistir", "yapıstır", "paste", "yapistirr", "yapıştırr", "yapistir" };
            
            // Kes komutu
            _commandMappings["kes"] = "kes";
            _commandAliases["kes"] = new List<string> { "cut", "kess", "kees", "kse" };
            
            // Kaydet komutu
            _commandMappings["kaydet"] = "kaydet";
            _commandAliases["kaydet"] = new List<string> { "save", "kaydt", "kaydett", "kayded" };
            
            // Yenile komutu
            _commandMappings["yenile"] = "yenile";
            _commandAliases["yenile"] = new List<string> { "refresh", "yenıle", "yenilee", "yenılee" };
            
            // Edge TTS Test komutu
            _commandMappings["edge tts"] = "edge tts";
            _commandAliases["edge tts"] = new List<string> { "edge ses", "test edge", "emel", "ahmet", "edge tts test" };
            
            // Geri al komutu
            _commandMappings["geri al"] = "geri al";
            _commandAliases["geri al"] = new List<string> { "undo", "geri", "gerı al", "geri all", "gerıal" };
            
            // İleri al komutu
            _commandMappings["ileri al"] = "ileri al";
            _commandAliases["ileri al"] = new List<string> { "redo", "ileri", "ilerı al", "ileri all", "ilerıal" };
            
            // Tümünü seç komutu
            _commandMappings["tümünü seç"] = "tümünü seç";
            _commandAliases["tümünü seç"] = new List<string> { "select all", "tümünü sec", "tumunu sec", "hepsini seç", "hepsini sec" };
            
            // Yazdır komutu
            _commandMappings["yazdır"] = "yazdır";
            _commandAliases["yazdır"] = new List<string> { "print", "yazdir", "yazdırr", "yazdirr" };
            
            // Bul komutu
            _commandMappings["bul"] = "bul";
            _commandAliases["bul"] = new List<string> { "find", "ara", "bull", "bulu", "search" };
            
            // Enter tuşu
            _commandMappings["enter"] = "enter";
            _commandAliases["enter"] = new List<string> { "enter tuşu", "enter tusu", "entr", "enteer", "return" };
            
            // Escape tuşu
            _commandMappings["escape"] = "escape";
            _commandAliases["escape"] = new List<string> { "esc", "iptal", "vazgeç", "vazgec", "cancel" };
            
            // Tab tuşu
            _commandMappings["tab"] = "tab";
            _commandAliases["tab"] = new List<string> { "sonraki", "tabb", "taab", "next" };
            
            // Boşluk tuşu
            _commandMappings["boşluk"] = "boşluk";
            _commandAliases["boşluk"] = new List<string> { "bosluk", "space", "boşluuk", "bosluuk" };
            
            // Ses kontrolleri
            _commandMappings["sesi arttır"] = "sesi arttır";
            _commandAliases["sesi arttır"] = new List<string> { "sesi artır", "sesi arttir", "sesi artir", "ses arttır", "ses artır" };
            
            _commandMappings["sesi azalt"] = "sesi azalt";
            _commandAliases["sesi azalt"] = new List<string> { "sesi azalt", "ses azalt", "sesi düşür", "ses düşür" };
            
            _commandMappings["sesi kapat"] = "sesi kapat";
            _commandAliases["sesi kapat"] = new List<string> { "ses kapat", "sesi kapa", "ses kapa", "mute" };
            
            _commandMappings["sesi aç"] = "sesi aç";
            _commandAliases["sesi aç"] = new List<string> { "ses aç", "sesi ac", "ses ac", "unmute" };
            
            // Pencere kontrolleri
            _commandMappings["pencereyi kapat"] = "pencereyi kapat";
            _commandAliases["pencereyi kapat"] = new List<string> { "pencere kapat", "pencereyi kapa", "pencere kapa", "kapat" };
            
            _commandMappings["uygulamayı kapat"] = "uygulamayı kapat";
            _commandAliases["uygulamayı kapat"] = new List<string> { "uygulama kapat", "uygulamayi kapat", "app kapat", "programı kapat" };
            
            _commandMappings["pencereyi sağa hizala"] = "pencereyi sağa hizala";
            _commandAliases["pencereyi sağa hizala"] = new List<string> { "sağa hizala", "saga hizala", "pencere sağa", "pencere saga" };
            
            _commandMappings["pencereyi sola hizala"] = "pencereyi sola hizala";
            _commandAliases["pencereyi sola hizala"] = new List<string> { "sola hizala", "pencere sola", "pencereyi sola" };
            
            // Bilgisayar kontrolleri
            _commandMappings["bilgisayarı kilitle"] = "bilgisayarı kilitle";
            _commandAliases["bilgisayarı kilitle"] = new List<string> { "bilgisayari kilitle", "pc kilitle", "kilitle", "lock" };
            
            _commandMappings["masaüstünü göster"] = "masaüstünü göster";
            _commandAliases["masaüstünü göster"] = new List<string> { "masaüstü göster", "masaustu goster", "desktop göster", "masaüstü" };
            
            _commandMappings["ekran görüntüsü al"] = "ekran görüntüsü al";
            _commandAliases["ekran görüntüsü al"] = new List<string> { "screenshot", "ekran goruntusu", "ss al", "ekran yakala" };
            
            // Onay/İptal komutları
            _commandMappings["kabul et"] = "kabul et";
            _commandAliases["kabul et"] = new List<string> { "kabul", "onayla", "tamam", "evet", "onay", "onaylıyorum", "ok" };
            
            _commandMappings["vazgeç"] = "vazgeç";
            _commandAliases["vazgeç"] = new List<string> { "vazgec", "iptal", "iptal et", "hayır", "hayir", "cancel" };
        }
        
        private void InitializeFolderCommands()
        {
            // Belgeler klasörü
            _commandMappings["belgeler aç"] = "belgeler aç";
            _commandAliases["belgeler aç"] = new List<string> { "belgelerim aç", "belgeler ac", "belgelerim ac", "documents aç" };
            
            // Resimler klasörü
            _commandMappings["resimler aç"] = "resimler aç";
            _commandAliases["resimler aç"] = new List<string> { "resimlerim aç", "resimler ac", "resimlerim ac", "pictures aç" };
            
            // Müzik klasörü
            _commandMappings["müzik aç"] = "müzik aç";
            _commandAliases["müzik aç"] = new List<string> { "müziğim aç", "muzik ac", "müziklerim aç", "music aç" };
            
            // Videolar klasörü
            _commandMappings["videolar aç"] = "videolar aç";
            _commandAliases["videolar aç"] = new List<string> { "videolarım aç", "videolar ac", "videolarim ac", "videos aç" };
            
            // İndirilenler klasörü
            _commandMappings["indirilenler aç"] = "indirilenler aç";
            _commandAliases["indirilenler aç"] = new List<string> { "İndirilenler aç", "downloads aç", "indirilenler ac", "downloads ac" };
            
            // Masaüstü klasörü
            _commandMappings["masaüstü aç"] = "masaüstü aç";
            _commandAliases["masaüstü aç"] = new List<string> { "masaustu ac", "desktop aç", "masaüstüm aç", "desktop ac" };
            
            // Dosya gezgini
            _commandMappings["dosya gezginini aç"] = "dosya gezginini aç";
            _commandAliases["dosya gezginini aç"] = new List<string> { "dosya gezgini aç", "explorer aç", "gezgin aç", "file explorer" };
        }
        
        private void InitializeNavigationCommands()
        {
            // Yön tuşları
            _commandMappings["sağ"] = "sağ";
            _commandAliases["sağ"] = new List<string> { "sag", "right", "saağ", "saga" };
            
            _commandMappings["sol"] = "sol";
            _commandAliases["sol"] = new List<string> { "left", "sool", "sola" };
            
            _commandMappings["yukarı"] = "yukarı";
            _commandAliases["yukarı"] = new List<string> { "yukari", "up", "yukarıı", "yukarii" };
            
            _commandMappings["aşağı"] = "aşağı";
            _commandAliases["aşağı"] = new List<string> { "asagi", "down", "aşağıı", "asagii" };
            
            // Sayfa navigasyonu
            _commandMappings["sayfayı aşağı kaydır"] = "sayfayı aşağı kaydır";
            _commandAliases["sayfayı aşağı kaydır"] = new List<string> { "sayfa aşağı", "page down", "aşağı kaydır" };
            
            _commandMappings["sayfayı yukarı kaydır"] = "sayfayı yukarı kaydır";
            _commandAliases["sayfayı yukarı kaydır"] = new List<string> { "sayfa yukarı", "page up", "yukarı kaydır" };
            
            _commandMappings["sayfa başına git"] = "sayfa başına git";
            _commandAliases["sayfa başına git"] = new List<string> { "sayfa başı", "home", "başa git", "en üst" };
            
            _commandMappings["sayfa sonuna git"] = "sayfa sonuna git";
            _commandAliases["sayfa sonuna git"] = new List<string> { "sayfa sonu", "end", "sona git", "en alt" };
            
            // Tarayıcı navigasyonu
            _commandMappings["tarayıcıda geri git"] = "tarayıcıda geri git";
            _commandAliases["tarayıcıda geri git"] = new List<string> { "geri", "back", "önceki sayfa", "geri git" };
            
            _commandMappings["tarayıcıda ileri git"] = "tarayıcıda ileri git";
            _commandAliases["tarayıcıda ileri git"] = new List<string> { "ileri", "forward", "sonraki sayfa", "ileri git" };
            
            // Sekme/Pencere komutları
            _commandMappings["yeni sekme"] = "yeni sekme";
            _commandAliases["yeni sekme"] = new List<string> { "new tab", "sekme aç", "yeni tab" };
            
            _commandMappings["sekmeyi kapat"] = "sekmeyi kapat";
            _commandAliases["sekmeyi kapat"] = new List<string> { "sekme kapat", "tab kapat", "close tab" };
            
            _commandMappings["yeni pencere"] = "yeni pencere";
            _commandAliases["yeni pencere"] = new List<string> { "new window", "pencere aç", "yeni window" };
            
            _commandMappings["görev görünümünü aç"] = "görev görünümünü aç";
            _commandAliases["görev görünümünü aç"] = new List<string> { "görev görünümü", "task view", "görevler" };
            
            _commandMappings["çalıştır penceresini aç"] = "çalıştır penceresini aç";
            _commandAliases["çalıştır penceresini aç"] = new List<string> { "çalıştır", "run", "çalıştır penceresi" };
        }
        
        private void InitializeOutlookCommands()
        {
            // E-posta komutları
            _commandMappings["yeni e-posta oluştur"] = "yeni e-posta oluştur";
            _commandAliases["yeni e-posta oluştur"] = new List<string> { "yeni mail", "yeni eposta", "yeni email", "mail oluştur" };
            
            _commandMappings["e-postayı yanıtla"] = "e-postayı yanıtla";
            _commandAliases["e-postayı yanıtla"] = new List<string> { "yanıtla", "reply", "maili yanıtla", "cevapla" };
            
            _commandMappings["herkese yanıtla"] = "herkese yanıtla";
            _commandAliases["herkese yanıtla"] = new List<string> { "tümünü yanıtla", "reply all", "herkesi yanıtla" };
            
            _commandMappings["e-postayı ilet"] = "e-postayı ilet";
            _commandAliases["e-postayı ilet"] = new List<string> { "ilet", "forward", "maili ilet", "yönlendir" };
            
            _commandMappings["e-postayı gönder"] = "e-postayı gönder";
            _commandAliases["e-postayı gönder"] = new List<string> { "gönder", "send", "mail gönder", "yolla" };
            
            // Outlook navigasyon
            _commandMappings["posta kutusuna git"] = "posta kutusuna git";
            _commandAliases["posta kutusuna git"] = new List<string> { "inbox", "gelen kutusu", "posta kutusu", "gelen kutusuna git" };
            
            _commandMappings["takvim aç"] = "takvim aç";
            _commandAliases["takvim aç"] = new List<string> { "takvime git", "calendar", "takvimi aç", "takvim göster" };
            
            _commandMappings["kişilere git"] = "kişilere git";
            _commandAliases["kişilere git"] = new List<string> { "kişiler", "contacts", "rehber", "adres defteri" };
            
            _commandMappings["yapılacaklara git"] = "yapılacaklara git";
            _commandAliases["yapılacaklara git"] = new List<string> { "görevler", "tasks", "yapılacaklar", "görev listesi" };
            
            // Mail işaretleme
            _commandMappings["okunmadı olarak işaretle"] = "okunmadı olarak işaretle";
            _commandAliases["okunmadı olarak işaretle"] = new List<string> { "okunmadı yap", "mark unread", "okunmamış yap" };
            
            _commandMappings["okundu olarak işaretle"] = "okundu olarak işaretle";
            _commandAliases["okundu olarak işaretle"] = new List<string> { "okundu yap", "mark read", "okunmuş yap" };
            
            _commandMappings["bayrak ekle"] = "bayrak ekle";
            _commandAliases["bayrak ekle"] = new List<string> { "işaretle", "bayrakla", "flag", "bayrak koy" };
            
            // Diğer Outlook komutları
            _commandMappings["dosya ekle"] = "dosya ekle";
            _commandAliases["dosya ekle"] = new List<string> { "ek ekle", "attachment", "dosya iliştir", "eklenti" };
            
            _commandMappings["yeni toplantı"] = "yeni toplantı";
            _commandAliases["yeni toplantı"] = new List<string> { "toplantı oluştur", "meeting", "toplantı ekle", "randevu oluştur" };
            
            _commandMappings["e-posta ara"] = "e-posta ara";
            _commandAliases["e-posta ara"] = new List<string> { "mail ara", "outlook ara", "posta ara", "search mail" };
            
            _commandMappings["gönderilenler"] = "gönderilenler";
            _commandAliases["gönderilenler"] = new List<string> { "gönderilenlere git", "sent items", "gönderilen postalar", "sent" };
            
            _commandMappings["taslaklar"] = "taslaklar";
            _commandAliases["taslaklar"] = new List<string> { "taslaklara git", "drafts", "taslak mailler", "draft" };
            
            _commandMappings["okunmamış postalar"] = "okunmamış postalar";
            _commandAliases["okunmamış postalar"] = new List<string> { "okunmamış mailler", "unread", "okunmamışlar", "okunmamış" };
        }
        
        private void InitializeWebCommands()
        {
            // Wikipedia komutları
            _commandMappings["nedir"] = "nedir";
            _commandAliases["nedir"] = new List<string> { "ne", "tanım", "açıklama", "hakkında" };
            
            _commandMappings["kimdir"] = "kimdir";
            _commandAliases["kimdir"] = new List<string> { "kim", "kişi", "biyografi" };
            
            _commandMappings["vikipedi"] = "vikipedi";
            _commandAliases["vikipedi"] = new List<string> { "wikipedia", "wiki", "ansiklopedi" };
            
            // Haber komutları
            _commandMappings["haberler"] = "haberler";
            _commandAliases["haberler"] = new List<string> { "haber", "gündem", "son dakika", "gelişmeler" };
            
            _commandMappings["teknoloji haberleri"] = "teknoloji haberleri";
            _commandAliases["teknoloji haberleri"] = new List<string> { "teknoloji haber", "tech news", "bilim teknoloji" };
            
            _commandMappings["spor haberleri"] = "spor haberleri";
            _commandAliases["spor haberleri"] = new List<string> { "spor haber", "sports news", "maç sonuçları" };
            
            _commandMappings["ekonomi haberleri"] = "ekonomi haberleri";
            _commandAliases["ekonomi haberleri"] = new List<string> { "ekonomi haber", "borsa", "finans haberleri" };
            
            // Twitter/Trend komutları
            _commandMappings["twitter gündem"] = "twitter gündem";
            _commandAliases["twitter gündem"] = new List<string> { "twitter", "trendler", "gündem konuları", "trend topic" };
            
            _commandMappings["popüler konular"] = "popüler konular";
            _commandAliases["popüler konular"] = new List<string> { "popüler", "viral", "trend", "gündemde ne var" };
            
            // Genel web araması
            _commandMappings["web araması"] = "web araması";
            _commandAliases["web araması"] = new List<string> { "internette ara", "web ara", "ara", "araştır" };
        }
        
        private void InitializeEdgeTTSCommands()
        {
            // Edge TTS komutları
            _commandMappings["edge tts"] = "edge tts";
            _commandAliases["edge tts"] = new List<string> { "edge ses", "edge seslendirme", "edge konuş", "edge tts emel", "edge tts ahmet" };
            
            _commandMappings["edge tts emel"] = "edge tts emel";
            _commandAliases["edge tts emel"] = new List<string> { "emel seslendir", "emel ile oku", "emel konuş", "edge emel" };
            
            _commandMappings["edge tts ahmet"] = "edge tts ahmet";
            _commandAliases["edge tts ahmet"] = new List<string> { "ahmet seslendir", "ahmet ile oku", "ahmet konuş", "edge ahmet" };
            
            // Tolga devre dışı olduğu için sadece uyarı için
            _commandMappings["edge tts tolga"] = "edge tts tolga";
            _commandAliases["edge tts tolga"] = new List<string> { "tolga seslendir", "tolga ile oku", "tolga konuş", "edge tolga" };
            
            // Test ses komutu
            _commandMappings["test ses"] = "test ses";
            _commandAliases["test ses"] = new List<string> { "ses test", "ses testi", "sesi test et", "test audio", "audio test" };
        }
        
        // Türkçe karakter normalizasyonu
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
        
        // Levenshtein mesafesi hesaplama
        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[source.Length, target.Length];
        }
        
        // Benzerlik hesaplama
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            // Türkçe karakterleri normalize et
            string normalizedSource = NormalizeTurkish(source);
            string normalizedTarget = NormalizeTurkish(target);

            // Tam eşleşme varsa 1.0 döndür
            if (normalizedSource.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            int distance = LevenshteinDistance(normalizedSource, normalizedTarget);
            int maxLength = Math.Max(normalizedSource.Length, normalizedTarget.Length);
            
            if (maxLength == 0) return 1.0;
            
            return 1.0 - (double)distance / maxLength;
        }
        
        /// <summary>
        /// Özel alias ekleme metodu
        /// </summary>
        public void AddAlias(string command, string alias)
        {
            if (!_commandAliases.ContainsKey(command))
            {
                _commandAliases[command] = new List<string>();
            }
            
            if (!_commandAliases[command].Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                _commandAliases[command].Add(alias);
                Debug.WriteLine($"[SystemCommandRegistry] Alias eklendi: '{alias}' -> '{command}'");
            }
        }
    }
}