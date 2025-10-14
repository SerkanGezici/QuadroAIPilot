// ApplicationService.cs  (tam dosya)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Services
{
    /// <summary> Uygulama açma / odaklama servisi. </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly IWindowsApiService _windowsApiService;
        private readonly ApplicationRegistry _appRegistry;
        // Tekrarlı açma denemelerini önlemek için
        private static readonly object _lockObject = new object();
        private static string _currentlyProcessingApp = string.Empty;

        public ApplicationService(IWindowsApiService winApi)
        {
            _windowsApiService = winApi ?? throw new ArgumentNullException(nameof(winApi));
            _appRegistry = ApplicationRegistry.Instance;
            _ = Task.Run(_appRegistry.InitializeScanAsync);
        }

        public Task<bool> OpenOrFocusApplicationAsync(string appName,
                                                      string processName = "",
                                                      string expectedWin = "")
        {
            // Uygulama adını temizle
            string cleanAppName = appName.Trim();

            // Eğer aynı uygulama şu anda işleniyorsa, tekrar deneme
            lock (_lockObject)
            {
                if (_currentlyProcessingApp.Equals(cleanAppName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[ApplicationService] {cleanAppName} zaten işleniyor, tekrar denenmeyecek.");
                    return Task.FromResult(true); // Zaten işleniyor, başarılı kabul et
                }

                // İşleme başladığımızı kaydet
                _currentlyProcessingApp = cleanAppName;
            }

            // İşlem tamamlandığında _currentlyProcessingApp'i temizle
            return OpenOrFocusCoreAsync(cleanAppName, processName, expectedWin)
                .ContinueWith(t =>
                {
                    lock (_lockObject)
                    {
                        _currentlyProcessingApp = string.Empty;
                    }
                    return t.Result;
                });
        }

        /*───────────────────────────  + helper  ───────────────────────────*/

        private async Task<bool> OpenOrFocusCoreAsync(string appName,
                                                      string processName,
                                                      string expectedWin)
        {
            /* 1) Registry'den bulabiliyor muyuz? */
            var info = _appRegistry.FindApplication(appName);
            if (info != null)
            {
                _appRegistry.IncrementUsageCount(info.Name);
                processName = info.ProcessName;
                return await StartByPathAsync(info.ExecutablePath, processName, expectedWin);
            }

            /* 2) Verilen ismi "yol / exe / uri" kabul edip dene */
            return await StartByPathAsync(appName, processName, expectedWin);
        }

        /*──────────────────────  asıl başlatıcı  ──────────────────────────*/

        private async Task<bool> StartByPathAsync(string pathOrUri,
                                                  string processName,
                                                  string expectedWin)
        {            /* a) Zaten açık mı? */
            if (!string.IsNullOrWhiteSpace(processName) &&
                _windowsApiService.IsApplicationRunning(processName))
            {
                Debug.WriteLine($"[ApplicationService] {processName} zaten çalışıyor, öne getiriliyor.");
                // Mevcut boyutu korumak için maximize=false
                bool focused = _windowsApiService.BringWindowToFront(processName, expectedWin, false);
                Debug.WriteLine($"[ApplicationService] Öne getirme sonucu: {focused}");
                return true; // Uygulama zaten açıksa, öne getirme denemesi sonucuna bakılmaksızın başarılı kabul et
            }

            /* b) ─ URI şeması (ms-settings:, whatsapp:) */
            if (IsSystemUri(pathOrUri))
            {
                Debug.WriteLine($"[ApplicationService] URI başlatılıyor: {pathOrUri}");
                bool launched = await Launcher.LaunchUriAsync(new Uri(pathOrUri));
                Debug.WriteLine($"[ApplicationService] URI başlatıldı: {pathOrUri}, Sonuç: {launched}");
                return launched;
            }
            /* c) ─ MMC konsolu (.msc) */
            else if (pathOrUri.EndsWith(".msc", StringComparison.OrdinalIgnoreCase))
            {                Debug.WriteLine($"[ApplicationService] MMC konsolu tam ekranda başlatılıyor: {pathOrUri}");
                var startInfo = new ProcessStartInfo 
                {
                    FileName = "mmc.exe",
                    Arguments = $"\"{pathOrUri}\"", 
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Maximized  // Tam ekran olarak başlat
                };
                Process.Start(startInfo);
                Debug.WriteLine($"[ApplicationService] MMC konsolu tam ekranda başlatıldı: {pathOrUri}");
                return true;
            }
            /* d) ─ EXE veya PATH */
            else
            {
                var exePath = EnsureExeExtension(pathOrUri);
                if (File.Exists(exePath))
                {                    Debug.WriteLine($"[ApplicationService] Uygulama tam ekranda başlatılıyor: {exePath}");
                    var startInfo = new ProcessStartInfo 
                    { 
                        FileName = exePath, 
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized  // Tam ekran olarak başlat
                    };
                    Process.Start(startInfo);
                    Debug.WriteLine($"[ApplicationService] Uygulama tam ekranda başlatıldı: {exePath}");
                    return true;
                }
                else // son çare: shell start
                {                    Debug.WriteLine($"[ApplicationService] Shell ile tam ekranda başlatılıyor: {pathOrUri}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start /max \"\" \"{pathOrUri}\"",  // /max parametresi ile tam ekran başlatma
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    Debug.WriteLine($"[ApplicationService] Shell ile tam ekranda başlatıldı: {pathOrUri}");
                    return true;
                }
            }
        }

        /*──── yardımcı mini-metotlar ────*/

        private static bool IsSystemUri(string v) =>
                 !string.IsNullOrWhiteSpace(v) && v.Contains(':') && !v.Contains('\\');

        private static string EnsureExeExtension(string n) =>
                 n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n : $"{n}.exe";

        /*—————————————————— Belgeler —————————————————*/
        public async Task<bool> OpenDocumentsAsync()
        {
            try { return await Launcher.LaunchUriAsync(new Uri("shell:DocumentsLibrary")); }
            catch { return false; }
        }

        #region Interface Implementation

        public async Task<bool> LaunchApplicationAsync(string applicationName)
        {
            return await OpenOrFocusApplicationAsync(applicationName);
        }

        public string? FindApplication(string appName)
        {
            var info = _appRegistry.FindApplication(appName);
            return info?.ExecutablePath;
        }

        public string? GetApplicationPathFromRegistry(string appName)
        {
            var info = _appRegistry.FindApplication(appName);
            return info?.ExecutablePath;
        }

        public async Task<bool> LaunchApplicationByPathAsync(string applicationPath)
        {
            try
            {
                if (!File.Exists(applicationPath))
                    return false;

                Process.Start(applicationPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplicationService] Failed to launch {applicationPath}: {ex.Message}");
                return false;
            }
        }

        public List<string> GetInstalledApplications()
        {
            return _appRegistry.GetAllRegisteredApplications()
                .Select(app => app.Name)
                .ToList();
        }

        public bool IsApplicationRunning(string applicationName)
        {
            return _windowsApiService.IsProcessRunning(applicationName);
        }

        public async Task<bool> CloseApplicationAsync(string applicationName)
        {
            try
            {
                return _windowsApiService.KillProcessByName(applicationName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplicationService] Failed to close {applicationName}: {ex.Message}");
                return false;
            }
        }

        public List<string> SearchInRegistryApps(string searchTerm)
        {
            return _appRegistry.SearchApplications(searchTerm)
                .Select(app => app.Name)
                .ToList();
        }

        #endregion
    }
}