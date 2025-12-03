using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using QuadroAIPilot.Infrastructure;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Unified AI Python HTTP Bridge lifecycle yöneticisi
    /// Tek Chromium instance'da ChatGPT ve Gemini (2 sekme)
    /// ~400-600MB RAM tasarrufu sağlar
    /// </summary>
    public class UnifiedAIPythonBridge : IDisposable
    {
        private static UnifiedAIPythonBridge _instance;
        private Process _pythonProcess;
        private readonly object _lock = new object();
        private bool _isStarted;

        public static UnifiedAIPythonBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UnifiedAIPythonBridge();
                }
                return _instance;
            }
        }

        private UnifiedAIPythonBridge() { }

        /// <summary>
        /// Unified AI bridge'i başlat (async, UI blocking yok)
        /// </summary>
        public async Task StartBridgeAsync()
        {
            lock (_lock)
            {
                if (_isStarted)
                {
                    LogService.LogWarning("[UnifiedAIBridge] Already started - returning early");
                    return;
                }
                _isStarted = true;
            }

            try
            {
                LogService.LogInfo("[UnifiedAIBridge] ========== UNIFIED AI BRIDGE STARTUP ==========");
                LogService.LogInfo("[UnifiedAIBridge] Tek Chromium + 2 Sekme (ChatGPT + Gemini)");
                LogService.LogInfo("[UnifiedAIBridge] ~400-600MB RAM tasarrufu");
                LogService.LogInfo("[UnifiedAIBridge] ================================================");

                // Python script yolu
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var pythonBridgeDir = Path.Combine(appDir, "PythonBridge");
                var scriptPath = Path.Combine(pythonBridgeDir, "unified_ai_bridge.py");

                LogService.LogInfo($"[UnifiedAIBridge] Script path: {scriptPath}");

                if (!File.Exists(scriptPath))
                {
                    LogService.LogError($"[UnifiedAIBridge] FATAL: Script not found at {scriptPath}");
                    return;
                }

                // Python path bul
                var pythonPath = await FindPythonAsync();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    LogService.LogError("[UnifiedAIBridge] FATAL: Python not found");
                    return;
                }

                LogService.LogInfo($"[UnifiedAIBridge] Python: {pythonPath}");

                // Process başlat
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = pythonBridgeDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _pythonProcess = new Process { StartInfo = psi };

                // Output logging
                _pythonProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.LogInfo($"[PY-UNIFIED] {e.Data}");
                    }
                };

                _pythonProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.LogError($"[PY-UNIFIED-ERR] {e.Data}");
                    }
                };

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                LogService.LogInfo($"[UnifiedAIBridge] Process started (PID: {_pythonProcess.Id})");

                // Health check (background task)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // 5 saniye bekle

                    LogService.LogInfo("[UnifiedAIBridge] Health check başlatılıyor...");

                    bool chatgptReady = false;
                    bool geminiReady = false;
                    int maxRetries = 15; // 15 * 3 = 45 saniye (2 servis için daha uzun)

                    for (int retry = 1; retry <= maxRetries; retry++)
                    {
                        try
                        {
                            // Genel health check
                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(5);

                            var response = await httpClient.GetAsync("http://localhost:8765/health");
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();

                                // JSON parse
                                chatgptReady = json.Contains("\"chatgpt_ready\": true") ||
                                               json.Contains("\"chatgpt_ready\":true");
                                geminiReady = json.Contains("\"gemini_ready\": true") ||
                                              json.Contains("\"gemini_ready\":true");

                                LogService.LogInfo($"[UnifiedAIBridge] Health: ChatGPT={chatgptReady}, Gemini={geminiReady} (Deneme {retry}/{maxRetries})");

                                if (chatgptReady && geminiReady)
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.LogWarning($"[UnifiedAIBridge] Health check hatası: {ex.Message} (Deneme {retry}/{maxRetries})");
                        }

                        await Task.Delay(3000);
                    }

                    // Son durum
                    if (chatgptReady && geminiReady)
                    {
                        LogService.LogInfo("✅ [UnifiedAIBridge] Unified AI Bridge hazır!");
                        LogService.LogInfo("   📌 ChatGPT: http://localhost:8765/chatgpt/chat");
                        LogService.LogInfo("   📌 Gemini:  http://localhost:8765/gemini/chat");
                    }
                    else
                    {
                        LogService.LogWarning($"⚠️ [UnifiedAIBridge] Kısmen hazır - ChatGPT={chatgptReady}, Gemini={geminiReady}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UnifiedAIBridge] Start failed: {ex.Message}");
                lock (_lock)
                {
                    _isStarted = false;
                }
                throw;
            }
        }

        /// <summary>
        /// Python yolu bul
        /// </summary>
        private async Task<string> FindPythonAsync()
        {
            try
            {
                // 1. QuadroAIPilot embedded Python
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var embeddedPython = Path.Combine(localAppData, "QuadroAIPilot", "Python", "python.exe");

                if (File.Exists(embeddedPython))
                {
                    LogService.LogInfo($"[UnifiedAIBridge] Found embedded Python: {embeddedPython}");
                    return embeddedPython;
                }

                // 2. System Python fallback
                var result = await RunCommandAsync("python", "--version");
                if (result.success)
                {
                    return "python";
                }

                // 3. python3 fallback
                result = await RunCommandAsync("python3", "--version");
                if (result.success)
                {
                    return "python3";
                }

                return null;
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UnifiedAIBridge] Python search failed: {ex.Message}");
                return null;
            }
        }

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
                if (process == null) return (false, null);

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
        /// ChatGPT hazır mı? (async check)
        /// </summary>
        public async Task<bool> IsChatGPTReadyAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);
                var response = await httpClient.GetAsync("http://localhost:8765/chatgpt/health");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json.Contains("\"ready\": true") || json.Contains("\"ready\":true");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gemini hazır mı? (async check)
        /// </summary>
        public async Task<bool> IsGeminiReadyAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);
                var response = await httpClient.GetAsync("http://localhost:8765/gemini/health");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json.Contains("\"ready\": true") || json.Contains("\"ready\":true");
                }
                return false;
            }
            catch
            {
                return false;
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
                _isStarted = false;
            }

            try
            {
                LogService.LogInfo("[UnifiedAIBridge] Stopping bridge...");

                if (_pythonProcess != null)
                {
                    try
                    {
                        if (_pythonProcess.HasExited)
                        {
                            LogService.LogInfo("[UnifiedAIBridge] Process already exited");
                            _pythonProcess.Dispose();
                        }
                        else
                        {
                            // Graceful shutdown
                            bool gracefulShutdown = false;
                            try
                            {
                                using var httpClient = new System.Net.Http.HttpClient();
                                httpClient.Timeout = TimeSpan.FromSeconds(3);

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

                                if (shutdownTask.Wait(3000) && shutdownTask.Result)
                                {
                                    gracefulShutdown = _pythonProcess.WaitForExit(5000);
                                    if (gracefulShutdown)
                                    {
                                        LogService.LogInfo("[UnifiedAIBridge] Process exited gracefully");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.LogInfo($"[UnifiedAIBridge] HTTP shutdown exception: {ex.Message}");
                            }

                            // Force kill if needed
                            if (!gracefulShutdown && !_pythonProcess.HasExited)
                            {
                                var pythonPid = _pythonProcess.Id;
                                LogService.LogInfo($"[UnifiedAIBridge] Force killing process (PID: {pythonPid})");

                                try
                                {
                                    _pythonProcess.Kill(entireProcessTree: true);
                                    _pythonProcess.WaitForExit(2000);
                                }
                                catch { }

                                // Fallback: taskkill
                                try
                                {
                                    var killProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "taskkill",
                                            Arguments = $"/F /PID {pythonPid} /T",
                                            UseShellExecute = false,
                                            CreateNoWindow = true
                                        }
                                    };
                                    killProcess.Start();
                                    killProcess.WaitForExit(3000);
                                }
                                catch { }

                                // unified-profile kullanan Chrome'ları bul ve öldür
                                try
                                {
                                    var findChromePs = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "powershell.exe",
                                            Arguments = "-Command \"Get-WmiObject Win32_Process -Filter \\\"name = 'chrome.exe'\\\" | Where-Object { $_.CommandLine -like '*unified-profile*' } | Select-Object -ExpandProperty ProcessId\"",
                                            UseShellExecute = false,
                                            CreateNoWindow = true,
                                            RedirectStandardOutput = true
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
                                                    var killChrome = new Process
                                                    {
                                                        StartInfo = new ProcessStartInfo
                                                        {
                                                            FileName = "taskkill",
                                                            Arguments = $"/F /PID {chromePid}",
                                                            UseShellExecute = false,
                                                            CreateNoWindow = true
                                                        }
                                                    };
                                                    killChrome.Start();
                                                    killChrome.WaitForExit(2000);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }

                            try { _pythonProcess.Dispose(); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogInfo($"[UnifiedAIBridge] Cleanup exception: {ex.Message}");
                    }
                }

                lock (_lock)
                {
                    _isStarted = false;
                    _pythonProcess = null;
                }

                LogService.LogInfo("[UnifiedAIBridge] Bridge stopped");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UnifiedAIBridge] Stop failed: {ex.Message}");
            }
        }
    }
}
