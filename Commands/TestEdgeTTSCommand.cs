using System;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Edge TTS test komutu
    /// </summary>
    public class TestEdgeTTSCommand : ICommand
    {
        public string CommandText { get; private set; }

        public TestEdgeTTSCommand()
        {
            CommandText = "edge tts";
        }

        public TestEdgeTTSCommand(string commandText)
        {
            CommandText = commandText;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                // CommandText'ten parametreleri çıkar
                string fullCommand = CommandText.ToLower();
                
                // Ses seçimi
                if (fullCommand.Contains("emel"))
                {
                    // Emel sesi seçildi
                    TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                    TextToSpeechService.UseEdgeTTS = true;
                    TextToSpeechService.SendToOutput("Emel sesi aktif edildi");
                    // UseEdgeTTS: {TextToSpeechService.UseEdgeTTS}, CurrentVoice: {TextToSpeechService.CurrentEdgeVoice}
                    await TextToSpeechService.SpeakTextAsync("Merhaba, ben Emel. Edge Neural ses teknolojisini kullanıyorum.");
                    return true;
                }
                else if (fullCommand.Contains("ahmet"))
                {
                    TextToSpeechService.CurrentEdgeVoice = "tr-TR-AhmetNeural";
                    TextToSpeechService.UseEdgeTTS = true;
                    TextToSpeechService.SendToOutput("Ahmet sesi aktif edildi");
                    await TextToSpeechService.SpeakTextAsync("Merhaba, ben Ahmet. Edge Neural ses teknolojisini kullanıyorum.");
                    return true;
                }
                else if (fullCommand.Contains("tolga"))
                {
                    TextToSpeechService.UseEdgeTTS = false;
                    TextToSpeechService.SendToOutput("Tolga sesi aktif edildi");
                    await TextToSpeechService.SpeakTextAsync("Merhaba, ben Tolga. Windows yerleşik ses sistemini kullanıyorum.");
                    return true;
                }
                else
                {
                    // Varsayılan test
                    string currentSystem = TextToSpeechService.UseEdgeTTS ? "Edge Neural" : "Windows";
                    string currentVoice = TextToSpeechService.UseEdgeTTS 
                        ? TextToSpeechService.CurrentEdgeVoice.ToString() 
                        : "Tolga";
                    
                    string testMessage = $"QuadroAI Pilot TTS testi. Şu anda {currentSystem} sisteminde {currentVoice} sesini kullanıyorum.";
                    
                    TextToSpeechService.SendToOutput(testMessage);
                    await TextToSpeechService.SpeakTextAsync(testMessage);
                    
                    TextToSpeechService.SendToOutput($"Kullanım: 'edge tts ahmet', 'edge tts emel' veya 'edge tts tolga'");
                    return true;
                }
            }
            catch (Exception ex)
            {
                TextToSpeechService.SendToOutput($"Edge TTS test hatası: {ex.Message}");
                return false;
            }
        }
    }
}