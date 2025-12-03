using QuadroAIPilot.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Claude CLI ile iletişim kuran servis
    /// Dashboard raporundaki Node.js implementasyonunun C# çevirisi
    /// </summary>
    public class ClaudeCLIService
    {
        private bool _isFirstMessage = true;  // İlk mesaj flag'i (session değil!)
        private readonly object _sessionLock = new object();

        /// <summary>
        /// Claude CLI'nin kurulu olup olmadığını kontrol eder
        /// </summary>
        public static bool IsClaudeCLIAvailable()
        {
            try
            {
                // cmd.exe ile sarmalama - loglardan gördüğümüz gibi
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"claude --version\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };

                LogService.LogInfo("[ClaudeCLI] Checking Claude CLI availability...");

                using var process = Process.Start(psi);
                if (process == null)
                {
                    LogService.LogWarning("[ClaudeCLI] Process.Start returned null");
                    return false;
                }

                var completed = process.WaitForExit(5000);
                if (!completed)
                {
                    LogService.LogWarning("[ClaudeCLI] Version check timeout");
                    return false;
                }

                var isAvailable = process.ExitCode == 0;
                LogService.LogInfo($"[ClaudeCLI] Availability check result: {isAvailable} (Exit code: {process.ExitCode})");

                return isAvailable;
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ClaudeCLI] Availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Claude CLI'ye mesaj gönderir ve yanıtı alır
        /// </summary>
        /// <param name="userInput">Kullanıcı mesajı</param>
        /// <returns>Claude'un yanıtı</returns>
        public async Task<ClaudeResponse> SendMessageAsync(string userInput)
        {
            var startTime = DateTime.Now;

            try
            {
                // Input validasyonu
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    return new ClaudeResponse
                    {
                        IsError = true,
                        ErrorMessage = "Boş mesaj gönderilemez"
                    };
                }

                LogService.LogInfo($"[ClaudeCLI] Sending message: '{userInput.Substring(0, Math.Min(50, userInput.Length))}...'");

                // Temp input dosyası oluştur (Claude CLI stdin için)
                var tempDir = Path.Combine(Path.GetTempPath(), "QuadroAIPilot");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var tempFile = Path.Combine(tempDir, $"claude_input_{DateTime.Now.Ticks}.txt");

                // System prompt ile birlikte kullanıcı mesajını hazırla
                var messageWithSystemPrompt = PrepareMessageWithSystemPrompt(userInput);
                await File.WriteAllTextAsync(tempFile, messageWithSystemPrompt, Encoding.UTF8);

                // İlk mesaj mı kontrol et
                bool isFirst;
                lock (_sessionLock)
                {
                    isFirst = _isFirstMessage;
                }

                // Claude CLI komutu - Claude Code html projesindeki gibi
                // İlk mesaj: -p (prompt), Devam: -c (continue)
                var flag = isFirst ? "-p" : "-c";

                LogService.LogInfo($"[ClaudeCLI] Command: claude {flag} < \"{tempFile}\"");

                // CLI'yi çalıştır
                var (success, output, error) = await ExecuteClaudeCLIAsync(flag, tempFile);

                // Temp dosyayı temizle
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch { /* Ignore cleanup errors */ }

                // Yanıtı işle
                if (!success || string.IsNullOrWhiteSpace(output))
                {
                    return new ClaudeResponse
                    {
                        IsError = true,
                        ErrorMessage = error ?? "Claude CLI yanıt vermedi",
                        Duration = DateTime.Now - startTime
                    };
                }

                // Artık ilk mesaj değil, devam mesajları -c kullanacak
                lock (_sessionLock)
                {
                    _isFirstMessage = false;
                }

                var duration = DateTime.Now - startTime;
                LogService.LogInfo($"[ClaudeCLI] Response received ({output.Length} chars, {duration.TotalSeconds:F1}s)");

                return new ClaudeResponse
                {
                    Content = output.Trim(),
                    Duration = duration,
                    IsError = false
                };
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ClaudeCLI] Error: {ex.Message}");
                return new ClaudeResponse
                {
                    IsError = true,
                    ErrorMessage = $"Claude CLI hatası: {ex.Message}",
                    Duration = DateTime.Now - startTime
                };
            }
        }

        /// <summary>
        /// Claude CLI process'ini çalıştırır
        /// </summary>
        private async Task<(bool success, string output, string error)> ExecuteClaudeCLIAsync(string flag, string tempFile)
        {
            Process process = null;
            try
            {
                // Claude CLI'ın script'leri oluşturacağı merkezi klasör
                // Bu sayede kullanıcı proje klasörleri temiz kalır
                var claudeScriptsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude",
                    "scripts"
                );

                // Klasör yoksa oluştur
                if (!Directory.Exists(claudeScriptsDir))
                {
                    Directory.CreateDirectory(claudeScriptsDir);
                    LogService.LogInfo($"[ClaudeCLI] Scripts klasörü oluşturuldu: {claudeScriptsDir}");
                }

                LogService.LogInfo($"[ClaudeCLI] Starting process with flag '{flag}'");
                LogService.LogInfo($"[ClaudeCLI] Temp file: {tempFile}");
                LogService.LogInfo($"[ClaudeCLI] Working dir: {claudeScriptsDir}");

                // Claude Code html projesindeki gibi cmd.exe ile redirect
                // --permission-mode bypassPermissions: Tüm izin kontrollerini bypass et (okuma, yazma, düzenleme)
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"claude {flag} --permission-mode bypassPermissions < \"{tempFile}\"\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = claudeScriptsDir,  // Script'ler merkezi klasörde oluşturulur
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                LogService.LogInfo($"[ClaudeCLI] Executing: {psi.FileName} {psi.Arguments}");

                process = Process.Start(psi);
                if (process == null)
                {
                    LogService.LogError("[ClaudeCLI] Failed to start process");
                    return (false, null, "Process başlatılamadı");
                }

                // DÜZELTME: CancellationToken ile timeout kontrolü
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(180));

                // Output ve error'u async olarak oku (timeout ile)
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Process'in bitmesini bekle (CancellationToken ile)
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout oluştu
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true); // Tüm child process'leri de öldür
                        }
                    }
                    catch (Exception killEx)
                    {
                        LogService.LogError($"[ClaudeCLI] Process kill error: {killEx.Message}");
                    }

                    LogService.LogError("[ClaudeCLI] Timeout (3 dakika)");
                    return (false, null, "Claude CLI timeout (3 dakika)");
                }

                // Output ve error'u al (stream'ler kapanmış olabilir, try-catch ile)
                string output = "";
                string error = "";

                try
                {
                    output = await outputTask;
                }
                catch (Exception ex)
                {
                    LogService.LogWarning($"[ClaudeCLI] Output read error: {ex.Message}");
                }

                try
                {
                    error = await errorTask;
                }
                catch (Exception ex)
                {
                    LogService.LogWarning($"[ClaudeCLI] Error read error: {ex.Message}");
                }

                LogService.LogInfo($"[ClaudeCLI] Process completed with exit code: {process.ExitCode}");
                LogService.LogInfo($"[ClaudeCLI] Output length: {output?.Length ?? 0} chars");
                LogService.LogInfo($"[ClaudeCLI] Error length: {error?.Length ?? 0} chars");

                // İlk 200 karakteri logla (debug için)
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var preview = output.Length > 200 ? output.Substring(0, 200) + "..." : output;
                    LogService.LogInfo($"[ClaudeCLI] Output preview: {preview}");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    LogService.LogWarning($"[ClaudeCLI] Error output: {error}");
                }

                if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
                {
                    LogService.LogError($"[ClaudeCLI] Exit code: {process.ExitCode}, Error: {error}");
                    return (false, null, error);
                }

                return (true, output, error);
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ClaudeCLI] Execution error: {ex.Message}");
                return (false, null, ex.Message);
            }
            finally
            {
                // DÜZELTME: Process'i her durumda temizle
                if (process != null && !process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.Dispose();
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                else if (process != null)
                {
                    try
                    {
                        process.Dispose();
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }

        /// <summary>
        /// Session'ı sıfırlar
        /// </summary>
        public void ResetSession()
        {
            lock (_sessionLock)
            {
                _isFirstMessage = true;  // Yeni session için ilk mesaj flag'ini resetle
            }
            LogService.LogInfo("[ClaudeCLI] Session reset - next message will use -p flag");
        }

        /// <summary>
        /// İlk mesaj mı kontrol eder
        /// </summary>
        public bool IsFirstMessage
        {
            get
            {
                lock (_sessionLock)
                {
                    return _isFirstMessage;
                }
            }
        }

        /// <summary>
        /// Kullanıcı mesajına system prompt ekler (sadece ilk mesajda)
        /// </summary>
        private string PrepareMessageWithSystemPrompt(string userInput)
        {
            // İlk mesaj değilse direkt kullanıcı mesajını döndür
            bool isFirst;
            lock (_sessionLock)
            {
                isFirst = _isFirstMessage;
            }

            if (!isFirst)
            {
                return userInput;
            }

            // İlk mesajda system prompt ekle
            var systemPrompt = @"Sen Quadro AI Pilot'sun. Quadro Computer tarafından geliştirilen yapay zeka asistanısın.

ÖNEMLİ KURALLAR:
- Sadece sorulursa kendini tanıt, aksi halde ASLA ""Ben Quadro AI Pilot'um"" gibi girişler yapma
- ASLA Claude, Anthropic'ten bahsetme
- Her zaman Türkçe yanıt ver
- KISA ve ÖZ cevaplar ver (maksimum 2-3 cümle)
- Gereksiz sorular sorma (""Başka bir şey ister misin?"" gibi)
- Direkt cevabı ver, fazla açıklama yapma

Şimdi soruya SADECE CEVABI ver:

";

            return systemPrompt + userInput;
        }
    }

    /// <summary>
    /// Claude CLI yanıt modeli
    /// </summary>
    public class ClaudeResponse
    {
        public string Content { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
