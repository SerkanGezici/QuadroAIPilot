using QuadroAIPilot.Commands;
using QuadroAIPilot.Services;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QuadroAIPilot.Modes
{
    public class CommandMode : IMode
    {
        private readonly CommandProcessor _processor;

        public CommandMode(CommandProcessor processor) => _processor = processor;

        public void Enter() => Debug.WriteLine("[Mode] Komut moduna girildi.");
        public void Exit() => Debug.WriteLine("[Mode] Komut modundan çıkıldı.");

        public bool HandleSpeech(string text)
        {
            Debug.WriteLine($"[CommandMode] HandleSpeech received: '{text}'");
            LogService.LogInfo($"[CommandMode] HandleSpeech received: '{text}'");
            _ = Task.Run(async () => await _processor.ProcessCommandAsync(text));
            return true; // ele aldım
        }
    }
}