using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Edge Neural TTS seslerini (Ahmet/Emel) kullanmak için köprü servisi
    /// Edge tarayıcısını otomasyon ile kontrol ederek Web Speech API'ye erişir
    /// </summary>
    public class EdgeTTSBridgeService : IDisposable
    {
        private Process _edgeProcess;
        private WebView2 _hiddenWebView;
        private bool _isInitialized = false;
        private bool _disposed = false;
        
        // Ses seçenekleri
        public enum EdgeVoice
        {
            AhmetNeural,
            EmelNeural,
            TolganNeural,
            SedaNeural
        }
        
        private EdgeVoice _currentVoice = EdgeVoice.EmelNeural; // Varsayılan Emel
        
        /// <summary>
        /// Geçerli Edge Neural sesi
        /// </summary>
        public EdgeVoice CurrentVoice 
        { 
            get => _currentVoice;
            set
            {
                _currentVoice = value;
                Debug.WriteLine($"[EdgeTTSBridge] Ses değiştirildi: {value}");
            }
        }
        
        /// <summary>
        /// Servisi başlatır
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            
            try
            {
                Debug.WriteLine("[EdgeTTSBridge] Başlatılıyor...");
                
                // Geçici HTML dosyası oluştur
                string tempHtmlPath = CreateTempHtmlFile();
                
                // Edge'i başlat (minimize edilmiş)
                var startInfo = new ProcessStartInfo
                {
                    FileName = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                    Arguments = $"--app=\"file:///{tempHtmlPath}\" --window-position=-9999,-9999 --window-size=1,1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                // Alternatif Edge yolları
                if (!File.Exists(startInfo.FileName))
                {
                    startInfo.FileName = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
                }
                
                _edgeProcess = Process.Start(startInfo);
                
                if (_edgeProcess != null)
                {
                    _isInitialized = true;
                    Debug.WriteLine("[EdgeTTSBridge] Edge başlatıldı");
                    
                    // Sayfanın yüklenmesi için bekle
                    await Task.Delay(2000);
                }
                else
                {
                    throw new Exception("Edge başlatılamadı");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EdgeTTSBridge] Başlatma hatası: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Metni Edge Neural sesleriyle seslendirir
        /// </summary>
        public async Task<bool> SpeakAsync(string text)
        {
            if (!_isInitialized) 
            {
                await InitializeAsync();
            }
            
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            try
            {
                Debug.WriteLine($"[EdgeTTSBridge] Seslendirme başlıyor: {text.Substring(0, Math.Min(50, text.Length))}...");
                
                // WebView2 kullanarak JavaScript çalıştır
                if (_hiddenWebView != null)
                {
                    string voiceName = GetVoiceName(_currentVoice);
                    string script = $@"
                        speakWithVoice('{EscapeText(text)}', '{voiceName}');
                    ";
                    
                    await _hiddenWebView.CoreWebView2.ExecuteScriptAsync(script);
                    
                    // TTS'in bitmesini bekle (basit timeout)
                    int estimatedDuration = CalculateSpeechDuration(text);
                    await Task.Delay(estimatedDuration);
                    
                    return true;
                }
                else
                {
                    // Alternatif: CDP (Chrome DevTools Protocol) kullan
                    return await SpeakWithCDPAsync(text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EdgeTTSBridge] Seslendirme hatası: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Chrome DevTools Protocol ile seslendirme
        /// </summary>
        private async Task<bool> SpeakWithCDPAsync(string text)
        {
            // Bu kısım daha karmaşık, şimdilik basit yöntem kullanalım
            Debug.WriteLine("[EdgeTTSBridge] CDP desteği henüz eklenmedi");
            return false;
        }
        
        /// <summary>
        /// Geçici HTML dosyası oluşturur
        /// </summary>
        private string CreateTempHtmlFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "EdgeTTS_Bridge.html");
            
            string html = @"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <title>Edge TTS Bridge</title>
</head>
<body>
    <h1>Edge TTS Bridge</h1>
    <div id='status'>Hazır</div>
    
    <script>
        let currentUtterance = null;
        
        // Türkçe Edge Neural sesleri
        const turkishVoices = {
            'AhmetNeural': 'Microsoft Server Speech Text to Speech Voice (tr-TR, AhmetNeural)',
            'EmelNeural': 'Microsoft Server Speech Text to Speech Voice (tr-TR, EmelNeural)',
            'TolganNeural': 'Microsoft Server Speech Text to Speech Voice (tr-TR, TolganNeural)',
            'SedaNeural': 'Microsoft Server Speech Text to Speech Voice (tr-TR, SedaNeural)'
        };
        
        function speakWithVoice(text, voiceName) {
            console.log('Speaking:', text, 'with voice:', voiceName);
            
            if (window.speechSynthesis.speaking) {
                window.speechSynthesis.cancel();
            }
            
            const utterance = new SpeechSynthesisUtterance(text);
            
            // Sesi bul ve ayarla
            const voices = window.speechSynthesis.getVoices();
            const targetVoice = voices.find(v => 
                v.name.includes(voiceName) || 
                v.name === turkishVoices[voiceName]
            );
            
            if (targetVoice) {
                utterance.voice = targetVoice;
                console.log('Voice found:', targetVoice.name);
            } else {
                console.warn('Voice not found:', voiceName);
                // Varsayılan Türkçe ses kullan
                const defaultTurkish = voices.find(v => v.lang.includes('tr-TR'));
                if (defaultTurkish) {
                    utterance.voice = defaultTurkish;
                }
            }
            
            utterance.rate = 1.0;
            utterance.pitch = 1.0;
            utterance.volume = 1.0;
            
            utterance.onstart = () => {
                document.getElementById('status').textContent = 'Konuşuyor...';
            };
            
            utterance.onend = () => {
                document.getElementById('status').textContent = 'Hazır';
                // C# tarafına bildir
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({ action: 'ttsCompleted' });
                }
            };
            
            utterance.onerror = (event) => {
                console.error('TTS Error:', event);
                document.getElementById('status').textContent = 'Hata: ' + event.error;
            };
            
            window.speechSynthesis.speak(utterance);
        }
        
        // Sesler yüklendiğinde
        window.speechSynthesis.onvoiceschanged = () => {
            const voices = window.speechSynthesis.getVoices();
            console.log('Available voices:', voices.length);
            
            // Türkçe sesleri listele
            const turkish = voices.filter(v => v.lang.includes('tr'));
            console.log('Turkish voices:', turkish.map(v => v.name));
        };
        
        // İlk yükleme
        window.addEventListener('load', () => {
            console.log('Edge TTS Bridge loaded');
            setTimeout(() => {
                window.speechSynthesis.getVoices();
            }, 100);
        });
    </script>
</body>
</html>";
            
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            return tempPath;
        }
        
        /// <summary>
        /// Ses enum'ını string'e çevirir
        /// </summary>
        private string GetVoiceName(EdgeVoice voice)
        {
            return voice switch
            {
                EdgeVoice.AhmetNeural => "AhmetNeural",
                EdgeVoice.EmelNeural => "EmelNeural",
                EdgeVoice.TolganNeural => "TolganNeural",
                EdgeVoice.SedaNeural => "SedaNeural",
                _ => "AhmetNeural"
            };
        }
        
        /// <summary>
        /// Metni JavaScript için güvenli hale getirir
        /// </summary>
        private string EscapeText(string text)
        {
            return text.Replace("\\", "\\\\")
                      .Replace("'", "\\'")
                      .Replace("\"", "\\\"")
                      .Replace("\r", "\\r")
                      .Replace("\n", "\\n")
                      .Replace("\t", "\\t");
        }
        
        /// <summary>
        /// Tahmini konuşma süresini hesaplar (ms)
        /// </summary>
        private int CalculateSpeechDuration(string text)
        {
            // Ortalama okuma hızı: ~150 kelime/dakika
            int wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            int durationMs = (int)((wordCount / 150.0) * 60 * 1000);
            
            // Minimum 1 saniye, maksimum 30 saniye
            return Math.Max(1000, Math.Min(30000, durationMs));
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
                    // Geçici HTML dosyasını sil
                    string tempPath = Path.Combine(Path.GetTempPath(), "EdgeTTS_Bridge.html");
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                    
                    // Edge process'i kapat
                    if (_edgeProcess != null && !_edgeProcess.HasExited)
                    {
                        _edgeProcess.Kill();
                        _edgeProcess.Dispose();
                    }
                    
                    // WebView2 Dispose edilemiyor, sadece null yapıyoruz
                    _hiddenWebView = null;
                    
                    _disposed = true;
                    Debug.WriteLine("[EdgeTTSBridge] Kaynaklar temizlendi");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EdgeTTSBridge] Dispose hatası: {ex.Message}");
                }
            }
        }
    }
}