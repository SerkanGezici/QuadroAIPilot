using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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

                // v68: npm için polling-based wait ile output completion detection
                bool isNpmCommand = fileName.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
                                    actualArguments.Contains("npm", StringComparison.OrdinalIgnoreCase);

                if (isNpmCommand)
                {
                    _logger.Log("[v68] npm komutu tespit edildi, polling-based wait aktif");

                    // Polling loop - npm için output-based completion detection
                    var pollInterval = TimeSpan.FromSeconds(2);
                    var elapsed = TimeSpan.Zero;
                    var maxWait = TimeSpan.FromSeconds(timeoutSeconds);
                    var completionDetected = false;
                    var lastOutputLength = 0;
                    var noNewOutputCounter = 0;

                    while (elapsed < maxWait)
                    {
                        // Process normal çıktı mı?
                        if (process.HasExited)
                        {
                            _logger.Log("Process normal çıkış yaptı", LogLevel.Success);
                            break;
                        }

                        // Output'ta npm completion pattern var mı?
                        var currentOutput = outputBuilder.ToString();
                        if (CheckNpmCompletionInOutput(currentOutput))
                        {
                            _logger.Log("[v68] npm kurulum tamamlandı (output pattern tespit edildi)", LogLevel.Success);
                            completionDetected = true;

                            // npm'in son işlemlerini bitirmesi için kısa bekle
                            await Task.Delay(3000);
                            break;
                        }

                        // Output değişmedi mi? (takılma tespiti)
                        if (currentOutput.Length == lastOutputLength)
                        {
                            noNewOutputCounter++;
                            if (noNewOutputCounter >= 30) // 60 saniye output yok
                            {
                                _logger.Log("[v68] 60 saniyedir yeni output yok, takılma tespit edildi", LogLevel.Warning);

                                // Son output'u kontrol et, belki kurulum bitti ama sinyal gelmedi
                                if (CheckNpmCompletionInOutput(currentOutput))
                                {
                                    _logger.Log("[v68] Takılma ama kurulum başarılı görünüyor", LogLevel.Success);
                                    completionDetected = true;
                                }
                                break;
                            }
                        }
                        else
                        {
                            noNewOutputCounter = 0;
                            lastOutputLength = currentOutput.Length;
                        }

                        await Task.Delay(pollInterval);
                        elapsed += pollInterval;
                    }

                    // Process'i sonlandır (hala çalışıyorsa)
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                            _logger.Log("[v68] Process sonlandırıldı", LogLevel.Info);
                        }
                        catch (Exception killEx)
                        {
                            _logger.Log($"Process kill hatası: {killEx.Message}", LogLevel.Warning);
                        }
                    }

                    // Sonuçları ata
                    result.Output = outputBuilder.ToString();
                    result.Error = errorBuilder.ToString();

                    if (completionDetected || CheckNpmCompletionInOutput(result.Output))
                    {
                        result.Success = true;
                        result.ExitCode = 0;
                        _logger.Log("[v68] npm kurulum BAŞARILI (output-based)", LogLevel.Success);
                    }
                    else if (elapsed >= maxWait)
                    {
                        result.Success = false;
                        result.ExitCode = -2;
                        result.Error = $"Timeout: {timeoutSeconds} saniye içinde tamamlanamadı";
                        _logger.Log($"Process timeout! ({timeoutSeconds}s aşıldı)", LogLevel.Error);
                    }
                    else
                    {
                        result.ExitCode = process.HasExited ? process.ExitCode : -1;
                        result.Success = result.ExitCode == 0;
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    _logger.Log($"npm işlemi tamamlandı: Exit Code {result.ExitCode} ({duration:F1}s)",
                        result.Success ? LogLevel.Success : LogLevel.Error);
                }
                else
                {
                    // Normal (npm olmayan) komutlar için standart WaitForExitAsync
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
                                process.Kill(entireProcessTree: true);
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

                // Output logla (her iki durumda da)
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    _logger.Log($"STDOUT: {result.Output.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    _logger.Log($"STDERR: {result.Error.Trim()}", LogLevel.Warning);
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
        /// v68: npm output'unda kurulum tamamlanma pattern'lerini kontrol eder
        /// </summary>
        private bool CheckNpmCompletionInOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            // npm success patterns (case insensitive)
            var successPatterns = new[]
            {
                @"added \d+ packages?",           // "added 123 packages"
                @"up to date",                    // "up to date"
                @"removed \d+ packages?",         // "removed 5 packages"
                @"changed \d+ packages?",         // "changed 10 packages"
                @"@anthropic-ai/claude-code@",    // paket adı görünüyorsa kurulum olmuştur
                @"npm warn",                      // uyarı varsa da kurulum tamamlanmıştır
            };

            foreach (var pattern in successPatterns)
            {
                if (Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase))
                {
                    _logger.Log($"[v68] npm completion pattern bulundu: {pattern}");
                    return true;
                }
            }

            return false;
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
