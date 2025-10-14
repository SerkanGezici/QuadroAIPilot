using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Windows'a Edge Neural seslerini yüklemek için komut
    /// </summary>
    public class InstallEdgeVoicesCommand : ICommand
    {
        public string CommandText { get; private set; }

        public InstallEdgeVoicesCommand()
        {
            CommandText = "edge seslerini yükle";
        }

        public InstallEdgeVoicesCommand(string commandText)
        {
            CommandText = commandText;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                TextToSpeechService.SendToOutput("Edge Neural sesleri kontrol ediliyor...");
                
                // Windows ayarlarında TTS seslerini aç
                TextToSpeechService.SendToOutput("Windows Ayarları açılıyor...");
                TextToSpeechService.SendToOutput("Lütfen şu adımları takip edin:");
                TextToSpeechService.SendToOutput("1. Zaman ve Dil > Konuşma sekmesine gidin");
                TextToSpeechService.SendToOutput("2. 'Sesler yönet' bölümünden Türkçe sesler ekleyin");
                TextToSpeechService.SendToOutput("3. 'Ahmet' ve 'Emel' Neural seslerini indirin");
                
                // Windows ayarlarını aç
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ms-settings:speech",
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                
                TextToSpeechService.SendToOutput("Windows Konuşma ayarları açıldı");
                TextToSpeechService.SendToOutput("Edge Neural seslerini indirdikten sonra uygulamayı yeniden başlatın");
                
                await TextToSpeechService.SpeakTextAsync("Windows konuşma ayarları açıldı. Lütfen Türkçe Neural seslerini yükleyin.");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstallEdgeVoicesCommand] Hata: {ex.Message}");
                TextToSpeechService.SendToOutput($"Hata: {ex.Message}");
                return false;
            }
        }
    }
}