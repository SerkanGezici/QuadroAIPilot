using QuadroAIPilot.Services;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QuadroAIPilot.Modes
{
    public class ReadingMode : IMode
    {
        public void Enter() => Debug.WriteLine("[Mode] Okuma moduna girildi.");
        public void Exit() => Debug.WriteLine("[Mode] Okuma modundan çıkıldı.");

        public bool HandleSpeech(string text)
        {
            _ = Task.Run(async () =>
            {
                await TextToSpeechService.SpeakTextAsync(text, 0.95f, true);
            });
            return true;
        }
    }
}