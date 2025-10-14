using System;
using System.Diagnostics;

namespace QuadroAIPilot.Services.Api
{
    /// <summary>
    /// Process yönetimi API çağrıları için sınıf
    /// </summary>
    public class ProcessApi
    {
        /// <summary>
        /// Uygulamanın çalışıp çalışmadığını kontrol eder
        /// </summary>
        /// <param name="processName">Process adı</param>
        /// <returns>Uygulama çalışıyorsa true</returns>
        public bool IsApplicationRunning(string processName)
        {
            try
            {
                // .exe uzantısını kaldır
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                Process[] processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessApi] IsApplicationRunning hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sistemdeki tüm process'leri arayarak process name ve window title eşleşmelerini bulur
        /// </summary>
        /// <param name="searchText">Aranacak metin</param>
        public void FindAllProcessesWithNameLike(string searchText)
        {
            try
            {
                Debug.WriteLine($"[ProcessApi] '{searchText}' içeren process'ler aranıyor...");
                Process[] processes = Process.GetProcesses();

                bool found = false;
                foreach (Process p in processes)
                {
                    try
                    {
                        if (p.ProcessName.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) ||
                            (p.MainWindowTitle != null && p.MainWindowTitle.ToLowerInvariant().Contains(searchText.ToLowerInvariant())))
                        {
                            found = true;
                            Debug.WriteLine($"[ProcessApi] Bulunan Process: Adı={p.ProcessName}, Başlık={p.MainWindowTitle}, ID={p.Id}, Handle={p.MainWindowHandle}");
                        }
                    }
                    catch { /* Process'e erişim hatası olabilir, devam et */ }
                }

                if (!found)
                {
                    Debug.WriteLine($"[ProcessApi] '{searchText}' içeren process bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessApi] Process arama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Process'i güvenli bir şekilde sonlandırır
        /// </summary>
        /// <param name="processId">Sonlandırılacak process ID'si</param>
        /// <returns>İşlem başarılıysa true</returns>
        public bool TerminateProcess(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    // Pencereyi kapat
                    process.CloseMainWindow();

                    // Eğer kapanmazsa sonlandır
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill();
                    }
                }

                Debug.WriteLine($"[ProcessApi] Process sonlandırıldı: {processId}");
                return true;
            }
            catch (ArgumentException)
            {
                // Process artık yok, başarılı kabul et
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessApi] Process sonlandırma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process adına göre process'leri bulur
        /// </summary>
        /// <param name="processName">Process adı</param>
        /// <returns>Bulunan process'ler</returns>
        public Process[] GetProcessesByName(string processName)
        {
            try
            {
                // .exe uzantısını kaldır
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                return Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessApi] GetProcessesByName hatası: {ex.Message}");
                return new Process[0];
            }
        }

        /// <summary>
        /// Belirtilen executable'ı çalıştırır
        /// </summary>
        /// <param name="fileName">Çalıştırılacak dosya adı</param>
        /// <param name="arguments">Argümanlar (opsiyonel)</param>
        /// <returns>Başlatılan process veya null</returns>
        public Process StartProcess(string fileName, string arguments = null)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                Process process = Process.Start(startInfo);
                Debug.WriteLine($"[ProcessApi] Process başlatıldı: {fileName}");
                return process;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessApi] Process başlatma hatası: {ex.Message}");
                return null;
            }
        }
    }
}
