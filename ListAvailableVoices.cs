using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;

namespace QuadroAIPilot.Utilities
{
    /// <summary>
    /// Windows sisteminde mevcut TTS seslerini listeleyen yardımcı sınıf
    /// </summary>
    public static class VoiceListingUtility
    {
        /// <summary>
        /// Sistemde mevcut tüm TTS seslerini listeler
        /// </summary>
        public static async Task ListAllAvailableVoicesAsync()
        {
            try
            {
                using (var synthesizer = new SpeechSynthesizer())
                {
                    // Tüm mevcut sesleri al
                    var voices = SpeechSynthesizer.AllVoices;
                    
                    Console.WriteLine($"\n=== TOPLAM {voices.Count} SES BULUNDU ===\n");
                    
                    // Tüm sesleri listele
                    foreach (var voice in voices.OrderBy(v => v.Language))
                    {
                        Console.WriteLine($"Ses Adı: {voice.DisplayName}");
                        Console.WriteLine($"  - Dil: {voice.Language}");
                        Console.WriteLine($"  - Cinsiyet: {voice.Gender}");
                        Console.WriteLine($"  - Açıklama: {voice.Description}");
                        Console.WriteLine($"  - ID: {voice.Id}");
                        Console.WriteLine("---");
                    }
                    
                    // Türkçe sesleri filtrele ve listele
                    Console.WriteLine("\n=== TÜRKÇE SESLER ===\n");
                    var turkishVoices = voices.Where(v => v.Language.StartsWith("tr-TR")).ToList();
                    
                    if (turkishVoices.Any())
                    {
                        foreach (var voice in turkishVoices)
                        {
                            Console.WriteLine($"✓ {voice.DisplayName} ({voice.Gender})");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Sistemde Türkçe TTS sesi bulunamadı!");
                    }
                    
                    // Varsayılan ses bilgisi
                    Console.WriteLine("\n=== VARSAYILAN SES ===");
                    Console.WriteLine($"Varsayılan Ses: {synthesizer.Voice.DisplayName}");
                    Console.WriteLine($"Varsayılan Dil: {synthesizer.Voice.Language}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata oluştu: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Belirli bir dile ait sesleri listeler
        /// </summary>
        /// <param name="languageCode">Dil kodu (örn: "tr-TR", "en-US")</param>
        public static void ListVoicesByLanguage(string languageCode)
        {
            try
            {
                var voices = SpeechSynthesizer.AllVoices
                    .Where(v => v.Language.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                Console.WriteLine($"\n=== {languageCode} DİLİNE AİT SESLER ({voices.Count} adet) ===\n");
                
                if (voices.Any())
                {
                    foreach (var voice in voices)
                    {
                        Console.WriteLine($"• {voice.DisplayName}");
                        Console.WriteLine($"  Cinsiyet: {voice.Gender}");
                        Console.WriteLine($"  Tam Dil Kodu: {voice.Language}");
                        Console.WriteLine($"  ID: {voice.Id}");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine($"'{languageCode}' dil koduna ait ses bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Belirli bir sesi test eder
        /// </summary>
        /// <param name="voiceName">Test edilecek sesin adı</param>
        /// <param name="testText">Okunacak test metni</param>
        public static async Task TestVoiceAsync(string voiceName, string testText = "Merhaba, bu bir test konuşmasıdır.")
        {
            try
            {
                using (var synthesizer = new SpeechSynthesizer())
                {
                    // İstenilen sesi bul
                    var voice = SpeechSynthesizer.AllVoices
                        .FirstOrDefault(v => v.DisplayName.Contains(voiceName, StringComparison.OrdinalIgnoreCase));
                    
                    if (voice != null)
                    {
                        // Sesi ayarla
                        synthesizer.Voice = voice;
                        
                        Console.WriteLine($"\n'{voice.DisplayName}' sesi test ediliyor...");
                        
                        // SSML formatında metin oluştur
                        string ssml = $@"
                        <speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{voice.Language}'>
                            <voice name='{voice.Id}'>
                                {testText}
                            </voice>
                        </speak>";
                        
                        // Metni sentezle
                        var stream = await synthesizer.SynthesizeSsmlToStreamAsync(ssml);
                        
                        if (stream != null)
                        {
                            Console.WriteLine("✓ Ses sentezleme başarılı!");
                            Console.WriteLine($"  - Ses boyutu: {stream.Size} byte");
                            Console.WriteLine($"  - İçerik türü: {stream.ContentType}");
                            stream.Dispose();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"'{voiceName}' adında bir ses bulunamadı.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sistemdeki sesleri dil gruplarına göre özetler
        /// </summary>
        public static void SummarizeVoicesByLanguage()
        {
            try
            {
                var voices = SpeechSynthesizer.AllVoices;
                var languageGroups = voices.GroupBy(v => v.Language.Substring(0, Math.Min(5, v.Language.Length)))
                    .OrderBy(g => g.Key);
                
                Console.WriteLine("\n=== DİLLERE GÖRE SES ÖZETİ ===\n");
                
                foreach (var group in languageGroups)
                {
                    var maleCount = group.Count(v => v.Gender == VoiceGender.Male);
                    var femaleCount = group.Count(v => v.Gender == VoiceGender.Female);
                    
                    Console.WriteLine($"{group.Key}: {group.Count()} ses (Erkek: {maleCount}, Kadın: {femaleCount})");
                    
                    foreach (var voice in group)
                    {
                        Console.WriteLine($"  - {voice.DisplayName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Özet hatası: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Örnek kullanım için Program sınıfı - Main çakışması nedeniyle yorumlandı
    /// </summary>
    /*
    public class VoiceListingExample
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Windows TTS Ses Listesi Yardımcı Programı");
            Console.WriteLine("========================================\n");
            
            // Tüm sesleri listele
            await VoiceListingUtility.ListAllAvailableVoicesAsync();
            
            // Türkçe sesleri listele
            Console.WriteLine("\n\n");
            VoiceListingUtility.ListVoicesByLanguage("tr-TR");
            
            // İngilizce sesleri listele
            Console.WriteLine("\n\n");
            VoiceListingUtility.ListVoicesByLanguage("en-US");
            
            // Dillere göre özet
            Console.WriteLine("\n\n");
            VoiceListingUtility.SummarizeVoicesByLanguage();
            
            // Örnek ses testi (eğer Tolga sesi varsa)
            Console.WriteLine("\n\nTolga sesini test etmek ister misiniz? (E/H)");
            if (Console.ReadLine()?.ToUpper() == "E")
            {
                await VoiceListingUtility.TestVoiceAsync("Tolga", "Merhaba, ben Tolga. Türkçe konuşan bir yapay zeka asistanıyım.");
            }
            
            Console.WriteLine("\n\nProgram tamamlandı. Çıkmak için bir tuşa basın...");
            Console.ReadKey();
        }
    }
    */
}