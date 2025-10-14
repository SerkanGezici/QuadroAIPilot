using System.Text.RegularExpressions;

namespace QuadroAIPilot.Constants
{
    /// <summary>
    /// Uygulama genelinde kullanılan sabitler
    /// </summary>
    public static class AppConstants
    {
        // Pencere ve AppBar ayarları
        public const int DefaultAppBarWidth = 300;
        public const int DefaultDebounceDelayMs = 500;
        public const int MaxDictationAttempts = 3;
        public const int CommandResultDisplayDelayMs = 2000;
        public const int ProcessCheckDelayMs = 100;

        // Web mesaj action'ları
        public static class WebActions
        {
            public const string StartDikte = "startDikte";
            public const string Speak = "speak";
            public const string Execute = "execute";
            public const string TextChanged = "textChanged";
            public const string TextareaPosition = "textareaPosition";
        }

        // Regex pattern'ları - static readonly olarak tanımlanmış
        public static readonly Regex VerbRegex = new Regex(
            @"\b(aç|kapat|başlat|sonlandır|kilitle|göster|al|hizala|git|seç|oluştur|yenile|kaydır|arttır|azalt|kopyala|yapıştır|kes|oku|listele|özetle|gönder)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex VolumeRegex = new Regex(
            @"\b(ses|volume|sesli|sessiz|yükselt|alçalt|kıs|artır|azalt|arttır)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex MailRegex = new Regex(
            @"\b(e posta|e-posta|eposta|posta|mail)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // MAPI komutları için regex pattern - sadece spesifik mail/toplantı komutları (tam komut eşleşmesi)
        public static readonly Regex MAPIRegex = new Regex(
            @"(okunmamış e postalarımı göster|e postalarımı göster|gönderilmiş e postaları göster|detaylı oku \d+|bugünkü toplantılarım neler|bu haftaki toplantılarım neler)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Practical MAPI komutları için regex pattern
        public static readonly Regex PracticalMAPIRegex = new Regex(
            @"\b(son e posta|son mesaj|bugünkü randevu|bugün randevu|okunmamış e posta|yeni e posta|kişi ara|e posta özet)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Takvim/Toplantı komutları için regex pattern
        public static readonly Regex CalendarRegex = new Regex(
            @"\b(takvim|toplantı|randevu|bugün toplantı|yarın toplantı|bu hafta toplantı|toplantı oluştur|takvim listele)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Not komutları için regex pattern  
        public static readonly Regex NoteRegex = new Regex(
            @"\b(not oluştur|not yaz|not ekle|notlar listele|not)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Edge TTS komutları için regex pattern
        public static readonly Regex EdgeTTSRegex = new Regex(
            @"\b(edge tts|edge seslendirme|edge ses|edge konuş)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Test ses komutları için regex pattern
        public static readonly Regex TestAudioRegex = new Regex(
            @"\b(test ses|ses test|ses testi|sesi test et|test audio|audio test)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Görev komutları için regex pattern
        public static readonly Regex TaskRegex = new Regex(
            @"\b(görev oluştur|görev ekle|görev listele|görevler|görev)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Web komutları için regex pattern'ler
        public static readonly Regex WikipediaRegex = new Regex(
            @"\b(nedir|kimdir|ne demek|kim|nasıl|neden|ne zaman|hangi)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex NewsRegex = new Regex(
            @"\b(haber|haberler|son dakika|gündem|güncel|son gelişme|duyuru|finans|ekonomi|business|bloomberg|reuters|bbc|cnn|spor haberleri|ekonomi haberleri|teknoloji haberleri|sağlık haberleri|dünya haberleri|magazin haberleri|siyaset haberleri|haberlerde|haberlerini|haberlerinde)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex TwitterRegex = new Regex(
            @"\b(twitter|tweet|trend|gündem|popüler|sosyal medya)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex WeatherRegex = new Regex(
            @"\b(hava|hava durumu|sıcaklık|yağmur|kar|güneş|bulut|rüzgar|nem)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Özel kısa komutlar
        public static readonly string[] SpecialShortCommands = {
            "tamam", "kabul", "onayla", "enter", "enter tuşu",
            "vazgeç", "iptal", "iptal et", "esc", "escape",
            "tab", "boşluk", "onay", "onaylıyorum", "evet",
            "hayır", "kabul et", "devam et", "onayla",
            "test wikipedia", "test haberler", "test twitter", "test cache",
            "test google trends", "test ekşi sözlük", "test reddit"
        };

        // Tek kelimelik komutlar
        public static readonly string[] SingleWordCommands = {
            "kopyala", "yapıştır", "kes", "enter", "sağ", "sol",
            "yukarı", "aşağı", "escape", "esc", "tab", "sonraki",
            "önceki", "yenile", "geri", "ileri", "kaydet"
        };

        // Command verbs array - geliştirilmiş verb listesi
        public static readonly string[] CommandVerbs = {
            "aç", "kapat", "başlat", "durdur", "bul", "ara", "göster", "gizle", "yükle", "kaldır",
            "oluştur", "sil", "kopyala", "taşı", "düzenle", "kaydet", "açık", "kapalı", "aktif",
            "pasif", "büyük", "küçük", "tam", "yarım", "hızlı", "yavaş", "yüksek", "alçak",
            "sağ", "sol", "üst", "alt", "orta", "ilk", "son", "yeni", "eski", "boş", "dolu",
            "varolan", "mevcut", "geçmiş", "gelecek", "bugünkü", "dünkü", "yarınkı",
            "test", "et", "kontrol", "doğrula", "hesap", "profil", "oku", "listele", "göster",
            "e posta", "mesaj", "randevu", "toplantı", "kişi", "takvim", "özet", "okunmamış"
        };

        // Sistem klasörleri
        public static readonly string[] SystemFolders = {
            "klasör", "belgeler", "resimler", "müzik", "videolar",
            "indirilenler", "masaüstü", "dosya gezgini"
        };

        // Mod geçiş komutları
        public static readonly string[] ModeCommands = {
            "komut modu", "yazı modu", "okuma modu"
        };
    }
}
