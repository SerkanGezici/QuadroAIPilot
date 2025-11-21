using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using QuadroAIPilot.Infrastructure;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Gemini Python HTTP Bridge lifecycle yöneticisi
    /// ChatGPTPythonBridge pattern'inden uyarlanmıştır
    /// </summary>
    public class GeminiPythonBridge : IDisposable
    {
        private static GeminiPythonBridge _instance;
        private Process _pythonProcess;
        private readonly object _lock = new object();
        private bool _isStarted;

        public static GeminiPythonBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GeminiPythonBridge();
                }
                return _instance;
            }
        }

        private GeminiPythonBridge() { }

        /// <summary>
        /// Gemini bridge'i başlat
        /// </summary>
        public async Task StartBridgeAsync()
        {
            lock (_lock)
            {
                if (_isStarted)
                {
                    LogService.LogWarning("[GeminiBridge] Already started");
                    return;
                }
            }

            try
            {
                LogService.LogInfo("[GeminiBridge] Starting HTTP bridge...");

                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var scriptPath = Path.Combine(appDir, "PythonBridge", "gemini_http_bridge.py");

                if (!File.Exists(scriptPath))
                {
                    LogService.LogError($"[GeminiBridge] Script not found: {scriptPath}");
                    return;
                }

                var pythonPath = await FindPythonAsync();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    LogService.LogError("[GeminiBridge] Python not found");
                    return;
                }

                LogService.LogInfo($"[GeminiBridge] Using Python: {pythonPath}");
                LogService.LogInfo($"[GeminiBridge] Script: {scriptPath}");

                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = Path.Combine(appDir, "PythonBridge"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _pythonProcess = new Process { StartInfo = psi };

                _pythonProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        LogService.LogInfo($"[GEMINI-OUT] {e.Data}");
                };

                _pythonProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        LogService.LogError($"[GEMINI-ERR] {e.Data}");
                };

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                lock (_lock)
                {
                    _isStarted = true;
                }

                LogService.LogInfo($"[GeminiBridge] Process started (PID: {_pythonProcess.Id})");

                // Health check (background)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    LogService.LogInfo("[GeminiBridge] Health check başlatılıyor...");

                    bool isHealthy = false;
                    for (int retry = 1; retry <= 20; retry++)  // Gemini 57 saniye sürüyor, 20x3=60 saniye bekle
                    {
                        try
                        {
                            isHealthy = await GeminiBridgeService.IsAvailableAsync();
                            if (isHealthy)
                            {
                                LogService.LogInfo($"✅ [GeminiBridge] Ready! (Deneme {retry}/20)");
                                break;
                            }
                            else
                            {
                                LogService.LogInfo($"⏳ [GeminiBridge] Henüz hazır değil... (Deneme {retry}/20)");
                                await Task.Delay(3000);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.LogWarning($"⏳ [GeminiBridge] Health check hatası (deneme {retry}/20): {ex.Message}");
                            await Task.Delay(3000);
                        }
                    }

                    if (isHealthy)
                    {
                        LogService.LogInfo("✅ [GeminiBridge] Gemini kullanıma hazır (http://localhost:8766)");

                        // Sistem promptu YOK - Kullanıcı mesajları direkt Gemini'ye gidiyor
                        LogService.LogInfo("[GeminiBridge] Gemini hazır, sistem promptu devre dışı");
                    }
                    else
                    {
                        LogService.LogWarning("⚠️ [GeminiBridge] Health check başarısız, Gemini kullanılamayabilir");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError($"[GeminiBridge] Start failed: {ex.Message}");
                _isStarted = false;
            }
        }


        private async Task<string> FindPythonAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        var version = string.IsNullOrEmpty(output) ? error : output;
                        LogService.LogInfo($"[GeminiBridge] Found python: {version.Trim()}");
                        return "python";
                    }
                }
            }
            catch { }

            return null;
        }

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
                LogService.LogInfo("[GeminiBridge] Stopping bridge...");

                if (_pythonProcess != null)
                {
                    try
                    {
                        // Process zaten çıkmışsa işlem yapma
                        if (_pythonProcess.HasExited)
                        {
                            LogService.LogInfo("[GeminiBridge] Process already exited");
                            _pythonProcess.Dispose();
                        }
                        else
                        {
                            // Önce graceful shutdown dene (HTTP shutdown endpoint)
                            bool gracefulShutdown = false;
                            try
                            {
                                using var httpClient = new System.Net.Http.HttpClient();
                                httpClient.Timeout = TimeSpan.FromSeconds(3);

                                // HTTP POST /shutdown
                                var shutdownTask = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var response = await httpClient.PostAsync("http://localhost:8766/shutdown", null);
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
                                    gracefulShutdown = _pythonProcess.WaitForExit(5000);
                                    if (gracefulShutdown)
                                    {
                                        LogService.LogInfo("[GeminiBridge] Process exited gracefully via HTTP shutdown");
                                    }
                                    else
                                    {
                                        LogService.LogInfo("[GeminiBridge] HTTP shutdown sent, but process did not exit in time");
                                    }
                                }
                                else
                                {
                                    LogService.LogInfo("[GeminiBridge] HTTP shutdown request failed or timeout");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.LogInfo($"[GeminiBridge] HTTP shutdown exception: {ex.Message}");
                            }

                            // Graceful shutdown başarısızsa VE process hala çalışıyorsa, AGGRESSIVE force kill
                            if (!gracefulShutdown)
                            {
                                try
                                {
                                    // Process durumunu kontrol et
                                    if (_pythonProcess.HasExited)
                                    {
                                        LogService.LogInfo("[GeminiBridge] Process already exited");
                                    }
                                    else
                                    {
                                        LogService.LogInfo("[GeminiBridge] Graceful shutdown failed, AGGRESSIVE force killing...");

                                        var pythonPid = _pythonProcess.Id;

                                        // METHOD 1: Process.Kill(entireProcessTree: true)
                                        try
                                        {
                                            _pythonProcess.Kill(entireProcessTree: true);
                                            LogService.LogInfo($"[GeminiBridge] Process.Kill() called (PID: {pythonPid})");

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
                                            LogService.LogInfo($"[GeminiBridge] Process.Kill() failed: {winEx.Message}");
                                        }
                                        catch (InvalidOperationException invEx)
                                        {
                                            // Process zaten çıktı
                                            LogService.LogInfo($"[GeminiBridge] Process already exited: {invEx.Message}");
                                        }

                                        // METHOD 2: Fallback - taskkill komutunu kullan
                                        try
                                        {
                                            LogService.LogInfo($"[GeminiBridge] Fallback: taskkill /F /PID {pythonPid}");

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
                                            killProcess.WaitForExit(2000);  // 2 saniye bekle

                                            LogService.LogInfo($"[GeminiBridge] taskkill exit code: {killProcess.ExitCode}");
                                        }
                                        catch (Exception taskkillEx)
                                        {
                                            LogService.LogInfo($"[GeminiBridge] taskkill failed: {taskkillEx.Message}");
                                        }

                                        // METHOD 3: Sadece gemini-profile'ı kullanan Chrome'u bul ve öldür
                                        try
                                        {
                                            LogService.LogInfo("[GeminiBridge] Finding and killing gemini-profile Chrome processes...");

                                            // PowerShell ile gemini-profile kullanan Chrome process'lerini bul (WMI kullanarak)
                                            var findChromePs = new System.Diagnostics.Process
                                            {
                                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = "powershell.exe",
                                                    Arguments = "-Command \"Get-WmiObject Win32_Process -Filter \\\"name = 'chrome.exe'\\\" | Where-Object { $_.CommandLine -like '*gemini-profile*' } | Select-Object -ExpandProperty ProcessId\"",
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
                                                            LogService.LogInfo($"[GeminiBridge] Killing gemini-profile Chrome PID: {chromePid}");
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
                                                            LogService.LogInfo($"[GeminiBridge] Chrome PID {chromePid} killed (exit code: {killChrome.ExitCode})");
                                                        }
                                                        catch (Exception killEx)
                                                        {
                                                            LogService.LogInfo($"[GeminiBridge] Failed to kill Chrome PID {chromePid}: {killEx.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogService.LogInfo("[GeminiBridge] No gemini-profile Chrome processes found");
                                            }
                                        }
                                        catch (Exception chromeEx)
                                        {
                                            LogService.LogInfo($"[GeminiBridge] Chrome cleanup failed: {chromeEx.Message}");
                                        }
                                    }
                                }
                                catch (Exception killEx)
                                {
                                    LogService.LogWarning($"[GeminiBridge] Force kill error: {killEx.Message}");
                                }
                            }

                            // Process nesnesini dispose et
                            try
                            {
                                _pythonProcess.Dispose();
                                LogService.LogInfo("[GeminiBridge] Process object disposed");
                            }
                            catch (Exception disposeEx)
                            {
                                LogService.LogWarning($"[GeminiBridge] Process dispose error: {disposeEx.Message}");
                            }
                        }
                    }
                    catch (Exception processEx)
                    {
                        LogService.LogWarning($"[GeminiBridge] Process cleanup error: {processEx.Message}");
                    }
                    finally
                    {
                        _pythonProcess = null;
                    }
                }

                LogService.LogInfo("[GeminiBridge] Bridge stopped successfully");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[GeminiBridge] Fatal dispose error: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _isStarted = false;
                }
            }
        }
    }
}
