using System;
using System.Threading.Tasks;
using System.Diagnostics;
using QuadroAIPilot.Services;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Infrastructure;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// WebView2 ses çıkışını test eden komut
    /// </summary>
    public class TestAudioCommand : ICommand
    {
        private string _commandText = "";
        
        public string CommandText => _commandText;
        
        public void SetCommandText(string text)
        {
            _commandText = text;
        }
        
        public async Task<bool> ExecuteAsync()
        {
            return await ExecuteAsync(_commandText);
        }
        
        public async Task<bool> ExecuteAsync(string text)
        {
            try
            {
                _commandText = text;
                Debug.WriteLine($"[TestAudioCommand] Ses testi başlatılıyor");
                
                // Çıktıyı göster
                TextToSpeechService.SendToOutput("🔊 WebView2 ses testi başlatılıyor...");
                
                // WebViewManager'ı MainWindow'dan al
                var app = Microsoft.UI.Xaml.Application.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;
                var webViewManager = mainWindow?.WebViewManager;
                
                if (webViewManager == null)
                {
                    Debug.WriteLine("[TestAudioCommand] WebViewManager bulunamadı!");
                    TextToSpeechService.SendToOutput("❌ WebViewManager bulunamadı!");
                    return false;
                }
                
                // Test butonları hakkında bilgi ver
                TextToSpeechService.SendToOutput("🔊 WebView2 ses testi hazır!");
                
                await webViewManager.AppendFeedback("📊 Edge TTS Test Butonları:");
                await webViewManager.AppendFeedback("🔴 TEST: Edge TTS - Sistem seslerini listeler");
                await webViewManager.AppendFeedback("🔵 TEST: Edge Online TTS - Microsoft seslerini arar");
                await webViewManager.AppendFeedback("🟢 TEST: Direkt Edge TTS - Otomatik ses seçimi yapar");
                await webViewManager.AppendFeedback("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                await webViewManager.AppendFeedback("💡 İpucu: HTML alanında test butonlarını kullanın");
                
                // Kısa bir test mesajı
                await webViewManager.AppendOutput("✅ Ses sistemi hazır. Test butonlarını kullanabilirsiniz.");
                
                TextToSpeechService.SendToOutput("✅ Ses testi tamamlandı. HTML alanındaki butonları kullanın.");
                Debug.WriteLine("[TestAudioCommand] Ses testi tamamlandı");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TestAudioCommand] Hata: {ex.Message}");
                TextToSpeechService.SendToOutput($"❌ Test ses hatası: {ex.Message}");
                return false;
            }
        }
    }
}