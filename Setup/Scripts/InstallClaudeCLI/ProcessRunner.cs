using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroAIPilot.Setup
{
    /// <summary>
    /// Async process runner with timeout control and output capture
    /// </summary>
    public class ProcessRunner
    {
        private readonly Logger _logger;

        public ProcessRunner(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Asenkron olarak process çalıştırır ve timeout kontrolü yapar
        /// </summary>
        /// <param name="fileName">Çalıştırılacak dosya/komut</param>
        /// <param name="arguments">Komut argümanları</param>
        /// <param name="timeoutSeconds">Timeout süresi (saniye) - varsayılan 300s</param>
        /// <returns>ProcessResult ile çıkış kodu, stdout, stderr</returns>
        public async Task<ProcessResult> RunAsync(
            string fileName,
            string arguments,
            int timeoutSeconds = 300)
        {
            // v62: npm/node için cmd.exe wrapper (npm.cmd direkt çalıştırılamıyor)
            string actualFileName = fileName;
            string actualArguments = arguments;

            if (fileName.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("node", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("claude", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log($"[v62] npm/node/claude komutu tespit edildi, cmd.exe wrapper ekleniyor...");
                actualFileName = "cmd.exe";
                actualArguments = $"/c \"{fileName} {arguments}\"";
            }

            _logger.Log($"Process başlatılıyor: {actualFileName} {actualArguments}");
            _logger.Log($"Timeout: {timeoutSeconds} saniye");

            var startTime = DateTime.Now;
            var result = new ProcessResult
            {
                Command = $"{fileName} {arguments}" // Orijinal komut
            };

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var process = new Process();

                // Process başlangıç ayarları
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = actualFileName,
                    Arguments = actualArguments,
                    UseShellExecute = false, // KRITIK: Shell kullanma, direkt process çalıştır
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                // Output bufferları
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                // Async event handler'lar (deadlock önleme)
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                // Process başlat
                if (!process.Start())
                {
                    result.Success = false;
                    result.ExitCode = -1;
                    result.Error = "Process başlatılamadı";
                    _logger.Log("Process başlatma hatası!", LogLevel.Error);
                    return result;
                }

                // Async output okuma başlat
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _logger.Log($"Process başlatıldı (PID: {process.Id})");

                try
                {
                    // Process'in bitmesini bekle (timeout ile)
                    await process.WaitForExitAsync(cts.Token);

                    // Biraz bekle ki output buffer'lar boşalsın
                    await Task.Delay(100);

                    result.ExitCode = process.ExitCode;
                    result.Output = outputBuilder.ToString();
                    result.Error = errorBuilder.ToString();
                    result.Success = process.ExitCode == 0;

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    _logger.Log($"Process tamamlandı: Exit Code {process.ExitCode} ({duration:F1}s)",
                        result.Success ? LogLevel.Success : LogLevel.Error);

                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        _logger.Log($"STDOUT: {result.Output.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(result.Error))
                    {
                        _logger.Log($"STDERR: {result.Error.Trim()}", LogLevel.Warning);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout oluştu
                    _logger.Log($"Process timeout! ({timeoutSeconds}s aşıldı)", LogLevel.Error);

                    try
                    {
                        // Process'i zorla sonlandır
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true); // Tüm child process'leri de öldür
                            _logger.Log("Process zorla sonlandırıldı (kill)", LogLevel.Warning);
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logger.Log($"Process kill hatası: {killEx.Message}", LogLevel.Warning);
                    }

                    result.Success = false;
                    result.ExitCode = -2;
                    result.Error = $"Timeout: {timeoutSeconds} saniye içinde tamamlanamadı";
                    result.Output = outputBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Process çalıştırma hatası: {ex.Message}", LogLevel.Error);
                _logger.Log($"Exception Type: {ex.GetType().Name}", LogLevel.Error);

                result.Success = false;
                result.ExitCode = -3;
                result.Error = $"Exception: {ex.Message}\n{ex.StackTrace}";
            }

            return result;
        }

        /// <summary>
        /// Komutun çıktısını kontrol eder (version check için)
        /// </summary>
        public async Task<bool> CheckCommandExistsAsync(string command, string args = "--version")
        {
            _logger.Log($"Komut varlık kontrolü: {command} {args}");

            var result = await RunAsync(command, args, timeoutSeconds: 30);

            if (result.Success)
            {
                _logger.Log($"{command} bulundu: {result.Output.Trim()}", LogLevel.Success);
                return true;
            }
            else
            {
                _logger.Log($"{command} bulunamadı veya çalışmıyor", LogLevel.Warning);
                return false;
            }
        }
    }

    /// <summary>
    /// Process çalıştırma sonucu
    /// </summary>
    public class ProcessResult
    {
        public string Command { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;

        public override string ToString()
        {
            var outputPreview = Output?.Length > 200
                ? Output.Substring(0, 200)
                : Output ?? string.Empty;

            return $"Command: {Command}\n" +
                   $"Success: {Success}\n" +
                   $"Exit Code: {ExitCode}\n" +
                   $"Output: {outputPreview}\n" +
                   $"Error: {Error}";
        }
    }
}
