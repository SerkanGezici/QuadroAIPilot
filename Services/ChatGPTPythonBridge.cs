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
                    LogService.LogWarning("[ChatGPTBridge] Already started - returning early");
                    return;
                }
                _isStarted = true; // ✅ LOCK İÇİNDE SET ET - Race condition önlenir
            }

            try
            {
                LogService.LogInfo("[ChatGPTBridge] ========== STARTUP DEBUG ==========");
                LogService.LogInfo("[ChatGPTBridge] Starting HTTP bridge...");

                // Python script yolu
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                LogService.LogInfo($"[ChatGPTBridge] App directory: {appDir}");

                var pythonBridgeDir = Path.Combine(appDir, "PythonBridge");
                LogService.LogInfo($"[ChatGPTBridge] PythonBridge directory: {pythonBridgeDir}");
                LogService.LogInfo($"[ChatGPTBridge] PythonBridge exists: {Directory.Exists(pythonBridgeDir)}");

                var scriptPath = Path.Combine(pythonBridgeDir, "chatgpt_http_bridge.py");
                LogService.LogInfo($"[ChatGPTBridge] Script path: {scriptPath}");
                LogService.LogInfo($"[ChatGPTBridge] Script exists: {File.Exists(scriptPath)}");

                if (!File.Exists(scriptPath))
                {
                    LogService.LogError($"[ChatGPTBridge] FATAL: Script not found at {scriptPath}");
                    LogService.LogError("[ChatGPTBridge] Bridge başlatılamadı - script dosyası eksik");
                    return;
                }

                // Python path bul
                var pythonPath = await FindPythonAsync();
                LogService.LogInfo($"[ChatGPTBridge] Python path: {pythonPath ?? "NOT FOUND"}");

                if (string.IsNullOrEmpty(pythonPath))
                {
                    LogService.LogError("[ChatGPTBridge] FATAL: Python not found in PATH or standard locations");
                    LogService.LogError("[ChatGPTBridge] Bridge başlatılamadı - Python bulunamadı");
                    return;
                }

                LogService.LogInfo($"[ChatGPTBridge] Python executable: {pythonPath}");
                LogService.LogInfo($"[ChatGPTBridge] Python exists: {File.Exists(pythonPath)}");
                LogService.LogInfo($"[ChatGPTBridge] Script: {scriptPath}");
                LogService.LogInfo("[ChatGPTBridge] ====================================");

                // Process başlat
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = Path.Combine(appDir, "PythonBridge"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,   // Graceful shutdown için gerekli
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

                var pythonPid = _pythonProcess.Id;
                LogService.LogInfo($"[ChatGPTBridge] Process started (PID: {pythonPid})");

                // Health check (background task - UI blocking yok)
                _ = Task.Run(async () =>
                {
                    // İlk bekleme: Python process başlaması için
                    await Task.Delay(5000); // 5 saniye bekle (Python başlatma)

                    LogService.LogInfo("[ChatGPTBridge] Health check başlatılıyor (retry logic ile)...");

                    // Retry logic: 30 saniye boyunca her 3 saniyede bir dene
                    bool isHealthy = false;
                    int maxRetries = 10; // 10 * 3 = 30 saniye

                    for (int retry = 1; retry <= maxRetries; retry++)
                    {
                        try
                        {
                            isHealthy = await ChatGPTBridgeService.IsAvailableAsync();

                            if (isHealthy)
                            {
                                LogService.LogInfo($"✅ [ChatGPTBridge] Health check: OK - Bridge is ready! (Deneme {retry}/{maxRetries})");
                                break; // Başarılı, döngüden çık
                            }
                            else
                            {
                                LogService.LogInfo($"⏳ [ChatGPTBridge] Henüz hazır değil, tekrar deneniyor... (Deneme {retry}/{maxRetries})");
                                await Task.Delay(3000); // 3 saniye bekle ve tekrar dene
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.LogWarning($"⚠️ [ChatGPTBridge] Health check hatası: {ex.Message} (Deneme {retry}/{maxRetries})");
                            await Task.Delay(3000); // 3 saniye bekle ve tekrar dene
                        }
                    }

                    // Son durum kontrolü
                    if (isHealthy)
                    {
                        LogService.LogInfo("✅ [ChatGPTBridge] ChatGPT kullanıma hazır (http://localhost:8765)");

                        // Kimlik promptu GÖNDERİLMİYOR - ChatGPT'nin güvenlik filtrelerini tetikliyor
                        // Kullanıcı "sen kimsin?" diye sorduğunda AIMode'da custom response vereceğiz
                        LogService.LogInfo("[ChatGPTBridge] ChatGPT hazır, kimlik filtreleme AIMode'da yapılacak");
                    }
                    else
                    {
                        LogService.LogError($"❌ [ChatGPTBridge] Health check: FAILED - {maxRetries} deneme sonunda hazır olmadı");
                        LogService.LogWarning("[ChatGPTBridge] ChatGPT kullanılamayacak, Claude fallback aktif olacak");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError($"[ChatGPTBridge] Start failed: {ex.Message}");
                LogService.LogError($"[ChatGPTBridge] Exception Type: {ex.GetType().Name}");
                LogService.LogError($"[ChatGPTBridge] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogService.LogError($"[ChatGPTBridge] InnerException: {ex.InnerException.Message}");
                }
                lock (_lock)
                {
                    _isStarted = false; // ✅ Hata durumunda reset et
                }
                throw; // ✅ Exception'ı App.xaml.cs catch bloğuna fırlat
            }
        }

        /// <summary>
        /// Python yolu bul (embedded Python öncelikli, sonra system Python fallback)
        /// </summary>
        private async Task<string> FindPythonAsync()
        {
            try
            {
                // 1. QuadroAIPilot embedded Python (Setup tarafından kurulmuş)
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var embeddedPython = Path.Combine(localAppData, "QuadroAIPilot", "Python", "python.exe");

                if (File.Exists(embeddedPython))
                {
                    LogService.LogInfo($"[ChatGPTBridge] Found embedded Python: {embeddedPython}");
                    return embeddedPython;
                }

                LogService.LogWarning("[ChatGPTBridge] Embedded Python not found, trying system Python...");

                // 2. System Python fallback (development ortamı için)
                var result = await RunCommandAsync("python", "--version");
                if (result.success)
                {
                    LogService.LogInfo($"[ChatGPTBridge] Found system python: {result.output.Trim()}");
                    return "python";
                }

                // 3. python3 fallback (Linux/WSL için)
                result = await RunCommandAsync("python3", "--version");
                if (result.success)
                {
                    LogService.LogInfo($"[ChatGPTBridge] Found system python3: {result.output.Trim()}");
                    return "python3";
                }

                LogService.LogError("[ChatGPTBridge] Python not found anywhere!");
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
                _isStarted = false; // ✅ Flag reset et
            }

            try
            {
                LogService.LogInfo("[ChatGPTBridge] Stopping bridge...");

                if (_pythonProcess != null)
                {
                    try
                    {
                        // Process zaten çıkmışsa işlem yapma
                        if (_pythonProcess.HasExited)
                        {
                            LogService.LogInfo("[ChatGPTBridge] Process already exited");
                            _pythonProcess.Dispose();
                        }
                        else
                        {
                            // Önce graceful shutdown dene (HTTP shutdown endpoint)
                            bool gracefulShutdown = false;
                            try
                            {
                                using var httpClient = new System.Net.Http.HttpClient();
                                httpClient.Timeout = TimeSpan.FromSeconds(3);  // ✅ 2s → 3s

                                // HTTP POST /shutdown
                                var shutdownTask = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var response = await httpClient.PostAsync("http://localhost:8765/shutdown", null);
                                        return response.IsSuccessStatusCode;
                                    }
                                    catch
                                    {
                                        return false;
                                    }
                                });

                                // 3 saniye bekle
                                if (shutdownTask.Wait(3000) && shutdownTask.Result)
                                {
                                    // HTTP shutdown başarılı, process'in çıkmasını bekle (5 saniye)
                                    gracefulShutdown = _pythonProcess.WaitForExit(5000);  // ✅ 3s → 5s
                                    if (gracefulShutdown)
                                    {
                                        LogService.LogInfo("[ChatGPTBridge] Process exited gracefully via HTTP shutdown");
                                    }
                                    else
                                    {
                                        LogService.LogInfo("[ChatGPTBridge] HTTP shutdown sent, but process did not exit in time");
                                    }
                                }
                                else
                                {
                                    LogService.LogInfo("[ChatGPTBridge] HTTP shutdown request failed or timeout");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.LogInfo($"[ChatGPTBridge] HTTP shutdown exception: {ex.Message}");
                            }

                            // Graceful shutdown başarısızsa VE process hala çalışıyorsa, AGGRESSIVE force kill
                            if (!gracefulShutdown)
                            {
                                try
                                {
                                    // Process durumunu kontrol et
                                    if (_pythonProcess.HasExited)
                                    {
                                        LogService.LogInfo("[ChatGPTBridge] Process already exited");
                                    }
                                    else
                                    {
                                        LogService.LogInfo("[ChatGPTBridge] Graceful shutdown failed, AGGRESSIVE force killing...");

                                        var pythonPid = _pythonProcess.Id;

                                        // METHOD 1: Process.Kill(entireProcessTree: true)
                                        try
                                        {
                                            _pythonProcess.Kill(entireProcessTree: true);
                                            LogService.LogInfo($"[ChatGPTBridge] Process.Kill() called (PID: {pythonPid})");

                                            // WaitForExit'te exception çıkabilir, catch et
                                            try
                                            {
                                                _pythonProcess.WaitForExit(2000);  // 2 saniye bekle
                                            }
                                            catch (System.ComponentModel.Win32Exception)
                                            {
                                                // Process zaten öldü (expected)
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                // Process zaten çıktı (expected)
                                            }
                                        }
                                        catch (System.ComponentModel.Win32Exception winEx)
                                        {
                                            // Kill() hatası: Access denied veya process zaten öldü
                                            LogService.LogInfo($"[ChatGPTBridge] Process.Kill() failed: {winEx.Message}");
                                        }
                                        catch (InvalidOperationException invEx)
                                        {
                                            // Process zaten çıktı
                                            LogService.LogInfo($"[ChatGPTBridge] Process already exited: {invEx.Message}");
                                        }

                                        // METHOD 2: Fallback - taskkill komutunu kullan (TÜM python.exe'leri öldür)
                                        try
                                        {
                                            LogService.LogInfo($"[ChatGPTBridge] Fallback: taskkill /F /PID {pythonPid}");

                                            var killProcess = new System.Diagnostics.Process
                                            {
                                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = "taskkill",
                                                    Arguments = $"/F /PID {pythonPid} /T",  // /T = kill process tree
                                                    UseShellExecute = false,
                                                    CreateNoWindow = true,
                                                    RedirectStandardOutput = true,
                                                    RedirectStandardError = true
                                                }
                                            };

                                            killProcess.Start();
                                            killProcess.WaitForExit(3000);

                                            if (killProcess.ExitCode == 0)
                                            {
                                                LogService.LogInfo($"[ChatGPTBridge] taskkill SUCCESS (PID: {pythonPid})");
                                            }
                                            else
                                            {
                                                LogService.LogInfo($"[ChatGPTBridge] taskkill exit code: {killProcess.ExitCode}");
                                            }
                                        }
                                        catch (Exception taskKillEx)
                                        {
                                            LogService.LogInfo($"[ChatGPTBridge] taskkill failed: {taskKillEx.Message}");
                                        }

                                        // METHOD 3: Sadece chrome-profile kullanan Chrome'u bul ve öldür
                                        try
                                        {
                                            LogService.LogInfo("[ChatGPTBridge] Finding and killing chrome-profile Chrome processes...");

                                            // PowerShell ile chrome-profile kullanan Chrome process'lerini bul
                                            var findChromePs = new System.Diagnostics.Process
                                            {
                                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = "powershell.exe",
                                                    Arguments = "-Command \"Get-WmiObject Win32_Process -Filter \\\"name = 'chrome.exe'\\\" | Where-Object { $_.CommandLine -like '*chrome-profile*' } | Select-Object -ExpandProperty ProcessId\"",
                                                    UseShellExecute = false,
                                                    CreateNoWindow = true,
                                                    RedirectStandardOutput = true,
                                                    RedirectStandardError = true
                                                }
                                            };

                                            findChromePs.Start();
                                            var chromePidsOutput = findChromePs.StandardOutput.ReadToEnd();
                                            findChromePs.WaitForExit(3000);

                                            if (!string.IsNullOrWhiteSpace(chromePidsOutput))
                                            {
                                                var pids = chromePidsOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                                foreach (var pidStr in pids)
                                                {
                                                    if (int.TryParse(pidStr.Trim(), out int chromePid))
                                                    {
                                                        try
                                                        {
                                                            LogService.LogInfo($"[ChatGPTBridge] Killing chrome-profile Chrome PID: {chromePid}");
                                                            var killChrome = new System.Diagnostics.Process
                                                            {
                                                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                                                {
                                                                    FileName = "taskkill",
                                                                    Arguments = $"/F /PID {chromePid}",
                                                                    UseShellExecute = false,
                                                                    CreateNoWindow = true,
                                                                    RedirectStandardOutput = true,
                                                                    RedirectStandardError = true
                                                                }
                                                            };
                                                            killChrome.Start();
                                                            killChrome.WaitForExit(2000);
                                                            LogService.LogInfo($"[ChatGPTBridge] Chrome PID {chromePid} killed (exit code: {killChrome.ExitCode})");
                                                        }
                                                        catch (Exception killEx)
                                                        {
                                                            LogService.LogInfo($"[ChatGPTBridge] Failed to kill Chrome PID {chromePid}: {killEx.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogService.LogInfo("[ChatGPTBridge] No chrome-profile Chrome processes found");
                                            }
                                        }
                                        catch (Exception chromeEx)
                                        {
                                            LogService.LogInfo($"[ChatGPTBridge] Chrome cleanup failed: {chromeEx.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogService.LogInfo($"[ChatGPTBridge] Force kill exception (ignored): {ex.Message}");
                                }
                            }

                            // Dispose (exception ignore et)
                            try
                            {
                                _pythonProcess.Dispose();
                            }
                            catch
                            {
                                // Dispose exception ignore
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        // Win32Exception: Process zaten öldü veya erişim engellendi
                        LogService.LogInfo($"[ChatGPTBridge] Process cleanup: {ex.Message} (expected)");
                    }
                    catch (InvalidOperationException ex)
                    {
                        // InvalidOperationException: Process zaten çıktı
                        LogService.LogInfo($"[ChatGPTBridge] Process already terminated: {ex.Message}");
                    }
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
