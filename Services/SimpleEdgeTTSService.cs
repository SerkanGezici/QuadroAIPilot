using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Edge tarayıcısında Web Speech API kullanarak TTS yapan basit servis
    /// </summary>
    public class SimpleEdgeTTSService : IDisposable
    {
        private bool _disposed = false;
        private string _tempHtmlPath;
        
        public enum EdgeVoice
        {
            AhmetNeural,
            EmelNeural,
            TolganNeural,
            SedaNeural,
            Default
        }
        
        public EdgeVoice CurrentVoice { get; set; } = EdgeVoice.EmelNeural; // Varsayılan Emel
        
        public SimpleEdgeTTSService()
        {
            // Geçici HTML dosyasını oluştur
            _tempHtmlPath = CreateTempHtmlFile();
        }
        
        /// <summary>
        /// Metni Edge'de seslendirir
        /// </summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            try
            {
                Debug.WriteLine($"[SimpleEdgeTTS] Seslendirme başlıyor: {text.Substring(0, Math.Min(50, text.Length))}...");
                
                // Metni geçici dosyaya yaz
                UpdateHtmlWithText(text);
                
                // Edge'i başlat
                var startInfo = new ProcessStartInfo
                {
                    FileName = GetEdgePath(),
                    Arguments = $"--app=\"file:///{_tempHtmlPath.Replace('\\', '/')}\" --start-maximized",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                
                using (var process = Process.Start(startInfo))
                {
                    // Tahmini konuşma süresini bekle
                    int duration = CalculateSpeechDuration(text);
                    await Task.Delay(duration);
                    
                    // Pencereyi kapat
                    if (process != null && !process.HasExited)
                    {
                        process.CloseMainWindow();
                        await Task.Delay(500);
                        
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                }
                
                Debug.WriteLine("[SimpleEdgeTTS] Seslendirme tamamlandı");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleEdgeTTS] Hata: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Edge yolunu bulur
        /// </summary>
        private string GetEdgePath()
        {
            string[] possiblePaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    @"Microsoft\Edge\Application\msedge.exe")
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            throw new FileNotFoundException("Microsoft Edge bulunamadı");
        }
        
        /// <summary>
        /// Geçici HTML dosyası oluşturur
        /// </summary>
        private string CreateTempHtmlFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"EdgeTTS_{Guid.NewGuid()}.html");
            
            string html = @"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <title>QuadroAI TTS</title>
    <style>
        body {
            font-family: 'Segoe UI', Arial, sans-serif;
            background: #1e1e1e;
            color: #fff;
            display: flex;
            align-items: center;
            justify-content: center;
            height: 100vh;
            margin: 0;
            padding: 20px;
        }
        .container {
            text-align: center;
            max-width: 600px;
        }
        h1 {
            color: #0078d4;
            margin-bottom: 30px;
        }
        #status {
            font-size: 18px;
            margin: 20px 0;
            padding: 20px;
            background: #2d2d2d;
            border-radius: 8px;
        }
        #textToSpeak {
            display: none;
        }
        .voice-info {
            margin-top: 20px;
            font-size: 14px;
            color: #888;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>QuadroAI Pilot TTS</h1>
        <div id='status'>Sesler yükleniyor...</div>
        <div id='textToSpeak'>__TEXT_PLACEHOLDER__</div>
        <div class='voice-info' id='voiceInfo'></div>
    </div>
    
    <script>
        const textToSpeak = document.getElementById('textToSpeak').textContent;
        const requestedVoice = '__VOICE_PLACEHOLDER__';
        let selectedVoice = null;
        
        function findAndSelectVoice() {
            const voices = window.speechSynthesis.getVoices();
            console.log('Mevcut sesler:', voices.length);
            
            // Türkçe sesleri filtrele
            const turkishVoices = voices.filter(v => v.lang.includes('tr-TR'));
            console.log('Türkçe sesler:', turkishVoices.map(v => v.name));
            
            // İstenen sesi bul
            if (requestedVoice !== 'Default') {
                selectedVoice = turkishVoices.find(v => 
                    v.name.includes(requestedVoice) || 
                    v.name.toLowerCase().includes(requestedVoice.toLowerCase())
                );
            }
            
            // Bulunamazsa varsayılan Türkçe ses kullan
            if (!selectedVoice && turkishVoices.length > 0) {
                // Neural sesleri tercih et
                selectedVoice = turkishVoices.find(v => v.name.includes('Neural')) || turkishVoices[0];
            }
            
            if (selectedVoice) {
                document.getElementById('voiceInfo').textContent = 'Kullanılan ses: ' + selectedVoice.name;
                startSpeaking();
            } else {
                document.getElementById('status').textContent = 'Türkçe ses bulunamadı!';
                document.getElementById('voiceInfo').textContent = 'Mevcut ses sayısı: ' + voices.length;
            }
        }
        
        function startSpeaking() {
            if (!textToSpeak || textToSpeak === '__TEXT_PLACEHOLDER__') {
                document.getElementById('status').textContent = 'Seslendirilecek metin yok!';
                return;
            }
            
            document.getElementById('status').textContent = 'Konuşuyor...';
            
            const utterance = new SpeechSynthesisUtterance(textToSpeak);
            
            if (selectedVoice) {
                utterance.voice = selectedVoice;
            }
            
            utterance.rate = 1.0;
            utterance.pitch = 1.0;
            utterance.volume = 1.0;
            
            utterance.onstart = () => {
                console.log('Konuşma başladı');
            };
            
            utterance.onend = () => {
                document.getElementById('status').textContent = 'Tamamlandı ✓';
                console.log('Konuşma tamamlandı');
                
                // 2 saniye sonra pencereyi kapat
                setTimeout(() => {
                    window.close();
                }, 2000);
            };
            
            utterance.onerror = (event) => {
                document.getElementById('status').textContent = 'Hata: ' + event.error;
                console.error('TTS Hatası:', event);
            };
            
            window.speechSynthesis.speak(utterance);
        }
        
        // Sayfa yüklendiğinde
        window.addEventListener('load', () => {
            // Sesler yüklendiğinde
            if (window.speechSynthesis.onvoiceschanged !== undefined) {
                window.speechSynthesis.onvoiceschanged = findAndSelectVoice;
            }
            
            // İlk kontrol
            setTimeout(() => {
                if (window.speechSynthesis.getVoices().length > 0) {
                    findAndSelectVoice();
                } else {
                    document.getElementById('status').textContent = 'Sesler yükleniyor... Lütfen bekleyin.';
                    // Tekrar dene
                    setTimeout(findAndSelectVoice, 1000);
                }
            }, 100);
        });
        
        // Otomatik başlat
        document.addEventListener('DOMContentLoaded', () => {
            console.log('DOM yüklendi, metin:', textToSpeak.substring(0, 50) + '...');
        });
    </script>
</body>
</html>";
            
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            return tempPath;
        }
        
        /// <summary>
        /// HTML dosyasını günceller
        /// </summary>
        private void UpdateHtmlWithText(string text)
        {
            if (!File.Exists(_tempHtmlPath)) return;
            
            string html = File.ReadAllText(_tempHtmlPath, Encoding.UTF8);
            
            // Metni güvenli hale getir
            string safeText = text.Replace("\\", "\\\\")
                                  .Replace("'", "\\'")
                                  .Replace("\"", "\\\"")
                                  .Replace("\r", " ")
                                  .Replace("\n", " ")
                                  .Replace("<", "&lt;")
                                  .Replace(">", "&gt;");
            
            // Placeholder'ları değiştir
            html = html.Replace("__TEXT_PLACEHOLDER__", safeText);
            html = html.Replace("__VOICE_PLACEHOLDER__", CurrentVoice.ToString());
            
            File.WriteAllText(_tempHtmlPath, html, Encoding.UTF8);
        }
        
        /// <summary>
        /// Tahmini konuşma süresini hesaplar (ms)
        /// </summary>
        private int CalculateSpeechDuration(string text)
        {
            // Ortalama okuma hızı: ~150 kelime/dakika
            int wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int durationMs = (int)((wordCount / 150.0) * 60 * 1000);
            
            // Minimum 3 saniye, maksimum 60 saniye
            return Math.Max(3000, Math.Min(60000, durationMs)) + 2000; // +2 saniye tampon
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Geçici dosyayı sil
                    if (!string.IsNullOrEmpty(_tempHtmlPath) && File.Exists(_tempHtmlPath))
                    {
                        File.Delete(_tempHtmlPath);
                        Debug.WriteLine("[SimpleEdgeTTS] Geçici dosya silindi");
                    }
                    
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SimpleEdgeTTS] Dispose hatası: {ex.Message}");
                }
            }
        }
    }
}