using System;

namespace QuadroAIPilot.Models.AI
{
    /// <summary>
    /// Kullanıcı komutlarının niyet kategorileri
    /// </summary>
    public enum IntentType
    {
        Unknown = 0,
        OpenApplication,      // "Outlook'u aç", "Word'ü başlat"
        CloseApplication,     // "Kapat", "Sonlandır"
        FileOperation,        // "Dosya ara", "PDF bul"
        EmailOperation,       // "Mail oku", "Email gönder"
        SystemControl,        // "Sesi kapat", "Ekranı kilitle"
        FolderNavigation,     // "Klasör aç", "Masaüstüne git"
        WebSearch,           // "Google'da ara", "YouTube'da bul"
        WebInfoRequest,      // "Haberler oku", "Nedir", "Twitter gündem"
        MediaControl,        // "Müziği durdur", "Sesi aç"
        WindowControl,       // "Pencereyi küçült", "Tam ekran"
        Custom               // Kullanıcı tanımlı özel komutlar
    }

    /// <summary>
    /// Algılanan komut niyeti
    /// </summary>
    public class Intent
    {
        public IntentType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        public Intent(IntentType type, string name, string description = "")
        {
            Type = type;
            Name = name;
            Description = description;
        }
    }
}