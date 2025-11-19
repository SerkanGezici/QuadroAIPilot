using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using QuadroAIPilot.Infrastructure;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// ChatGPT Python HTTP Bridge lifecycle yöneticisi
    /// EdgeTTSPythonBridge pattern'ine benzer - process management
    /// </summary>
    public class ChatGPTPythonBridge : IDisposable
    {
        private static ChatGPTPythonBridge _instance;
        private Process _pythonProcess;
        private readonly object _lock = new object();
        private bool _isStarted;

        public static ChatGPTPythonBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ChatGPTPythonBridge();
                }
                return _instance;
            }
        }

        private ChatGPTPythonBridge() { }

        /// <summary>
        /// ChatGPT bridge'i başlat (async, UI blocking yok)
        /// </summary>
        public async Task StartBridgeAsync()
        {
            lock (_lock)
            {
                if (_isStarted)
                {
                    LogService.LogWarning("[ChatGPTBridge] Already started");
                    return;
                }
            }

            try
            {
                LogService.LogInfo("[ChatGPTBridge] Starting HTTP bridge...");

                // Python script yolu
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var scriptPath = Path.Combine(appDir, "PythonBridge", "chatgpt_http_bridge.py");

                if (!File.Exists(scriptPath))
                {
                    LogService.LogError($"[ChatGPTBridge] Script not found: {scriptPath}");
                    return;
                }

                // Python path bul
                var pythonPath = await FindPythonAsync();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    LogService.LogError("[ChatGPTBridge] Python not found in PATH");
                    return;
                }

                LogService.LogInfo($"[ChatGPTBridge] Using Python: {pythonPath}");
                LogService.LogInfo($"[ChatGPTBridge] Script: {scriptPath}");

                // Process başlat
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = Path.Combine(appDir, "PythonBridge"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _pythonProcess = new Process { StartInfo = psi };

                // Output logging - TÜM Python logları INFO seviyesinde (debugging için)
                _pythonProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        // TÜM logları INFO olarak göster (Visual Studio Output'ta görünsün)
                        LogService.LogInfo($"[PY-OUT] {e.Data}");
                    }
                };

                _pythonProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        // Tüm error output ERROR olarak logla (Visual Studio'da kırmızı görünsün)
                        LogService.LogError($"[PY-ERR] {e.Data}");
                    }
                };

                // Process başlat
                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                lock (_lock)
                {
                    _isStarted = true;
                }

                LogService.LogInfo($"[ChatGPTBridge] Process started (PID: {_pythonProcess.Id})");

                // Health check (background task - UI blocking yok)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(8000); // 8 saniye bekle (Playwright headless başlaması için)

                    try
                    {
                        var isHealthy = await ChatGPTBridgeService.IsAvailableAsync();
                        if (isHealthy)
                        {
                            LogService.LogInfo("✅ [ChatGPTBridge] Health check: OK - Bridge is ready!");
                            LogService.LogInfo("[ChatGPTBridge] ChatGPT kullanıma hazır (http://localhost:8765)");

                            // Kimlik promptunu arka planda gönder (user AI modunda değil, görünmez)
                            try
                            {
                                LogService.LogInfo("[ChatGPTBridge] Kimlik promptu gönderiliyor (arka plan)...");

                                // Sistem promptu TEK SATIR olmalı (ChatGPT type() metodu çok satırlı metinde sadece ilk satırı yazıyor)
                                var systemPrompt = "SEN QUADRO YAPAY ZEKA ASİSTANISIN. ÖNEMLİ KURALLAR: Asla OpenAI, ChatGPT, GPT, Claude veya başka bir AI sisteminden bahsetme. Kendini tanıtırken 'Ben Quadro Yapay Zeka Asistanıyım, sana yardımcı olmak için buradayım' de. Kullanıcı 'sen kimsin?', 'hangi model kullanıyorsun?', 'hangi yapay zekasın?' gibi sorularda sadece 'Ben Quadro Yapay Zeka Asistanıyım' de. Yeteneklerini anlatırken de Quadro olarak konuş. ASLA gerçek kimliğini (ChatGPT/GPT-5/OpenAI) ifşa etme.";

                                var response = await ChatGPTBridgeService.SendMessageAsync(systemPrompt);

                                if (!response.IsError)
                                {
                                    LogService.LogInfo("✅ [ChatGPTBridge] Kimlik promptu başarıyla gönderildi (response ignore edildi)");
                                    LogService.LogInfo($"[ChatGPTBridge] ChatGPT artık 'Ben Quadro'yum' diyor (context hazır)");
                                }
                                else
                                {
                                    LogService.LogWarning($"⚠️ [ChatGPTBridge] Kimlik promptu gönderilemedi: {response.ErrorMessage}");
                                }
                            }
                            catch (Exception promptEx)
                            {
                                LogService.LogWarning($"⚠️ [ChatGPTBridge] Kimlik promptu hatası: {promptEx.Message}");
                            }
                        }
                        else
                        {
                            LogService.LogWarning("❌ [ChatGPTBridge] Health check: FAILED - Bridge not responding");
                            LogService.LogWarning("[ChatGPTBridge] ChatGPT kullanılamayacak, Claude fallback aktif olacak");
                        }
                    }
                    catch (Exception healthEx)
                    {
                        LogService.LogError($"❌ [ChatGPTBridge] Health check exception: {healthEx.Message}");
                        LogService.LogWarning("[ChatGPTBridge] ChatGPT erişilemez, Claude fallback kullanılacak");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ChatGPTBridge] Start failed: {ex.Message}");
                lock (_lock)
                {
                    _isStarted = false;
                }
            }
        }

        /// <summary>
        /// Python yolu bul (python3 veya python)
        /// </summary>
        private async Task<string> FindPythonAsync()
        {
            try
            {
                // Önce python3 dene
                var result = await RunCommandAsync("python3", "--version");
                if (result.success)
                {
                    LogService.LogInfo($"[ChatGPTBridge] Found python3: {result.output.Trim()}");
                    return "python3";
                }

                // Sonra python dene
                result = await RunCommandAsync("python", "--version");
                if (result.success)
                {
                    LogService.LogInfo($"[ChatGPTBridge] Found python: {result.output.Trim()}");
                    return "python";
                }

                return null;
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ChatGPTBridge] Python search failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Komut çalıştır (async)
        /// </summary>
        private async Task<(bool success, string output)> RunCommandAsync(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, null);
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var completed = await Task.Run(() => process.WaitForExit(2000));

                return (completed && process.ExitCode == 0, output);
            }
            catch
            {
                return (false, null);
            }
        }

        /// <summary>
        /// Bridge çalışıyor mu?
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isStarted && _pythonProcess != null && !_pythonProcess.HasExited;
                }
            }
        }

        /// <summary>
        /// Bridge'i durdur ve temizle
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (!_isStarted || _pythonProcess == null)
                {
                    return;
                }
            }

            try
            {
                LogService.LogInfo("[ChatGPTBridge] Stopping bridge...");

                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill(entireProcessTree: true);
                    _pythonProcess.WaitForExit(5000);
                    _pythonProcess.Dispose();
                }

                lock (_lock)
                {
                    _isStarted = false;
                    _pythonProcess = null;
                }

                LogService.LogInfo("[ChatGPTBridge] Bridge stopped");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ChatGPTBridge] Stop failed: {ex.Message}");
            }
        }
    }
}
