using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Python edge-tts komut satırı aracını kullanarak TTS yapan servis
    /// </summary>
    public class EdgeTTSPythonBridge : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _pythonPath;
        private readonly string _edgeTtsScript;

        public EdgeTTSPythonBridge()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "QuadroAI_EdgeTTS");
            Directory.CreateDirectory(_tempDir);

            // Python ve edge-tts path'leri
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _pythonPath = Path.Combine(localAppData, "QuadroAIPilot", "Python", "python.exe");
            _edgeTtsScript = Path.Combine(localAppData, "QuadroAIPilot", "Python", "Scripts", "edge-tts-nossl.py");

            LoggingService.LogWarning($"[EdgeTTSPython] Python path: {_pythonPath}");
            LoggingService.LogWarning($"[EdgeTTSPython] edge-tts-nossl script: {_edgeTtsScript}");
            LoggingService.LogWarning($"[EdgeTTSPython] Python exists: {File.Exists(_pythonPath)}");
            LoggingService.LogWarning($"[EdgeTTSPython] Script exists: {File.Exists(_edgeTtsScript)}");
        }
        
        public async Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "tr-TR-EmelNeural")
        {
            try
            {
                LoggingService.LogWarning($"[EdgeTTSPython] TTS başlatılıyor - Voice: {voice}");

                // Geçici dosya adları
                var outputFile = Path.Combine(_tempDir, $"tts_{Guid.NewGuid()}.webm");

                // Metni escape et (sadece double quote)
                var escapedText = text.Replace("\"", "\\\"");

                // Python script ile edge-tts çalıştır (SSL bypass ile)
                var arguments = $"/c \"\"{_pythonPath}\" \"{_edgeTtsScript}\" --voice {voice} --text \"{escapedText}\" --write-media \"{outputFile}\"\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                LoggingService.LogWarning($"[EdgeTTSPython] CMD Arguments: {arguments}");

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    LoggingService.LogVerbose($"[EdgeTTSPython] Stdout: {output}");
                }

                if (!string.IsNullOrEmpty(error))
                {
                    LoggingService.LogWarning($"[EdgeTTSPython] Stderr: {error}");
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception($"edge-tts failed with exit code {process.ExitCode}. Error: {error}");
                }
                
                // Audio dosyasını oku
                if (File.Exists(outputFile))
                {
                    var audioData = await File.ReadAllBytesAsync(outputFile);
                    
                    // Geçici dosyayı sil
                    try { File.Delete(outputFile); } catch { }
                    
                    LoggingService.LogVerbose($"[EdgeTTSPython] Audio data alındı: {audioData.Length} bytes");
                    
                    return audioData;
                }
                else
                {
                    throw new Exception("Audio dosyası oluşturulamadı");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[EdgeTTSPython] Hata: {ex.Message}");
                throw;
            }
        }
        
        // Desteklenen sesler
        public static class Voices
        {
            public const string TurkishFemale = "tr-TR-EmelNeural";
            public const string TurkishMale = "tr-TR-AhmetNeural";
            
            public static readonly Dictionary<string, string> All = new()
            {
                { "emel", TurkishFemale },
                { "ahmet", TurkishMale },
                { "female", TurkishFemale },
                { "male", TurkishMale },
                { "kadın", TurkishFemale },
                { "erkek", TurkishMale }
            };
        }
        
        public void Dispose()
        {
            // Temp dizini temizle
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }
    }
}