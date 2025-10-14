using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    public class EdgeTTSCommand : ICommand
    {
        private readonly EdgeTTSPythonBridge _edgeTTSPythonBridge;
        public string CommandText { get; }
        
        public EdgeTTSCommand(string commandText)
        {
            CommandText = commandText;
            _edgeTTSPythonBridge = new EdgeTTSPythonBridge();
        }
        
        public async Task<bool> ExecuteAsync()
        {
            try
            {
                // Komutu parse et
                var parts = CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Parametreleri kontrol et (edge tts <ses> <metin>)
                if (parts.Length < 4)
                {
                    TextToSpeechService.SendToOutput("❌ Kullanım: edge tts <ses> <metin>");
                    TextToSpeechService.SendToOutput("💡 Örnekler:");
                    TextToSpeechService.SendToOutput("   edge tts emel Merhaba");
                    TextToSpeechService.SendToOutput("   edge tts ahmet Günaydın");
                    return false;
                }
                
                // Ses tipini al
                string voiceType = parts[2].ToLower();
                string text = string.Join(" ", parts.Skip(3));
                
                // Ses adını belirle
                string voiceName;
                if (EdgeTTSPythonBridge.Voices.All.TryGetValue(voiceType, out var mappedVoice))
                {
                    voiceName = mappedVoice;
                }
                else
                {
                    TextToSpeechService.SendToOutput($"❌ Geçersiz ses: {voiceType}");
                    TextToSpeechService.SendToOutput("✅ Geçerli sesler: emel, ahmet, kadın, erkek");
                    return false;
                }
                
                // WebViewManager'ı al
                var app = Microsoft.UI.Xaml.Application.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;
                var webViewManager = mainWindow?.WebViewManager;
                
                if (webViewManager == null)
                {
                    TextToSpeechService.SendToOutput("❌ WebViewManager bulunamadı!");
                    return false;
                }
                
                // Kullanıcıya bilgi ver
                string displayName = voiceType == "emel" || voiceType == "kadın" ? "Emel (Kadın)" : "Ahmet (Erkek)";
                TextToSpeechService.SendToOutput($"🔊 Edge TTS başlatılıyor: {displayName}");
                await webViewManager.AppendFeedback($"[Edge TTS] {displayName} sesi ile konuşuluyor...");
                
                // Python Edge TTS ile sesi üret
                Debug.WriteLine($"[EdgeTTSCommand] Python TTS başlatılıyor - Ses: {voiceName}, Metin: {text}");
                var audioData = await _edgeTTSPythonBridge.SynthesizeSpeechAsync(text, voiceName);
                
                if (audioData == null || audioData.Length == 0)
                {
                    TextToSpeechService.SendToOutput("❌ Ses üretilemedi!");
                    await webViewManager.AppendFeedback("[ERROR] Edge TTS ses üretemedi");
                    return false;
                }
                
                // Audio data'yı WebView'a gönder
                Debug.WriteLine($"[EdgeTTSCommand] Audio data alındı: {audioData.Length} bytes");
                await webViewManager.SendAudioStreamAsync(audioData, "webm", text);
                
                TextToSpeechService.SendToOutput($"✅ Edge TTS tamamlandı ({audioData.Length:N0} bytes)");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EdgeTTSCommand] Hata: {ex.Message}");
                TextToSpeechService.SendToOutput($"❌ Edge TTS hatası: {ex.Message}");
                
                // WebViewManager varsa hata mesajını göster
                var app = Microsoft.UI.Xaml.Application.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;
                var webViewManager = mainWindow?.WebViewManager;
                
                if (webViewManager != null)
                {
                    await webViewManager.AppendFeedback($"[ERROR] {ex.Message}");
                }
                
                return false;
            }
        }
    }
}