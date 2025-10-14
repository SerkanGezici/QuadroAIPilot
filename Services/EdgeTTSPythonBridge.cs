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
        
        public EdgeTTSPythonBridge()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "QuadroAI_EdgeTTS");
            Directory.CreateDirectory(_tempDir);
        }
        
        public async Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "tr-TR-EmelNeural")
        {
            try
            {
                LoggingService.LogWarning($"[EdgeTTSPython] TTS başlatılıyor - Voice: {voice}");
                
                // Geçici dosya adları
                var outputFile = Path.Combine(_tempDir, $"tts_{Guid.NewGuid()}.webm");
                
                // edge-tts komutunu çalıştır
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c edge-tts --voice \"{voice}\" --text \"{text.Replace("\"", "\\\"")}\" --write-media \"{outputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                if (!string.IsNullOrEmpty(error))
                {
                    LoggingService.LogWarning($"[EdgeTTSPython] Stderr: {error}");
                }
                
                if (process.ExitCode != 0)
                {
                    throw new Exception($"edge-tts failed with exit code {process.ExitCode}");
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