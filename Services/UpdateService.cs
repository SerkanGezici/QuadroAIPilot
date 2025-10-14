using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AutoUpdaterDotNET;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// GitHub Releases tabanlı otomatik güncelleme servisi
    /// AutoUpdater.NET kütüphanesini kullanır
    /// </summary>
    public class UpdateService
    {
        private readonly SettingsManager _settingsManager;
        private static UpdateService? _instance;
        private const string UPDATE_XML_URL = "https://raw.githubusercontent.com/SerkanGezici/QuadroAIPilot/main/update.xml";

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static UpdateService Instance => _instance ??= new UpdateService();

        private UpdateService()
        {
            _settingsManager = SettingsManager.Instance;
            ConfigureAutoUpdater();
        }

        /// <summary>
        /// AutoUpdater.NET konfigürasyonu
        /// </summary>
        private void ConfigureAutoUpdater()
        {
            try
            {
                // Türkçe dil desteği
                AutoUpdater.Mandatory = false;
                AutoUpdater.UpdateMode = Mode.Normal;
                AutoUpdater.ReportErrors = true;
                AutoUpdater.ShowSkipButton = true;
                AutoUpdater.ShowRemindLaterButton = true;

                // Update dialog'u kapatıldığında event
                AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;

                // Güncelleme kontrolü başarısız olursa
                AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;

                LogService.LogDebug("[UpdateService] AutoUpdater.NET yapılandırıldı");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UpdateService] AutoUpdater yapılandırma hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update kontrolü event handler
        /// </summary>
        private void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error != null)
            {
                LogService.LogError($"[UpdateService] Güncelleme kontrolü hatası: {args.Error.Message}", args.Error);
                return;
            }

            if (!args.IsUpdateAvailable)
            {
                LogService.LogDebug("[UpdateService] Güncelleme yok, uygulama güncel");
                return;
            }

            LogService.LogInfo($"[UpdateService] Yeni versiyon bulundu: {args.CurrentVersion} → {args.InstalledVersion}");
        }

        /// <summary>
        /// Uygulama çıkış event'i (güncelleme için kapatma)
        /// </summary>
        private void AutoUpdater_ApplicationExitEvent()
        {
            LogService.LogInfo("[UpdateService] Uygulama güncelleme için kapatılıyor");
            System.Windows.Application.Current?.Shutdown();
        }

        /// <summary>
        /// Güncellemeleri kontrol et (arka planda)
        /// </summary>
        /// <param name="silentCheck">Sessiz kontrol (UI gösterme)</param>
        /// <returns></returns>
        public async Task CheckForUpdatesAsync(bool silentCheck = true)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Otomatik güncelleme kapalıysa ve sessiz kontrol ise çık
                    if (silentCheck && !_settingsManager.Settings.AutoUpdateEnabled)
                    {
                        LogService.LogDebug("[UpdateService] Otomatik güncelleme kapalı, kontrol atlanıyor");
                        return;
                    }

                    // Son kontrol zamanını güncelle
                    _settingsManager.Settings.LastUpdateCheck = DateTime.Now;
                    _ = _settingsManager.SaveSettingsAsync();

                    LogService.LogInfo($"[UpdateService] Güncelleme kontrolü başlatılıyor... (Silent: {silentCheck})");
                    LogService.LogInfo($"[UpdateService] URL: {UPDATE_XML_URL}");
                    LogService.LogInfo($"[UpdateService] Mevcut versiyon: {GetCurrentVersion()}");

                    // AutoUpdater.NET kullanarak kontrol
                    if (silentCheck)
                    {
                        // Sessiz mod: Sadece güncelleme varsa bildirim göster
                        AutoUpdater.Mandatory = false;
                        AutoUpdater.UpdateMode = Mode.Normal;
                    }
                    else
                    {
                        // Manuel kontrol: Her durumda dialog göster
                        AutoUpdater.Mandatory = false;
                        AutoUpdater.UpdateMode = Mode.ForcedDownload;
                    }

                    // Güncelleme kontrolü başlat
                    AutoUpdater.Start(UPDATE_XML_URL);
                }
                catch (Exception ex)
                {
                    LogService.LogError($"[UpdateService] Güncelleme kontrolü hatası: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Güncelleme kontrolünü zorla (manuel)
        /// UI gösterir, kullanıcı etkileşimlidir
        /// </summary>
        public async Task CheckForUpdatesManualAsync()
        {
            LogService.LogInfo("[UpdateService] Manuel güncelleme kontrolü başlatılıyor...");
            await CheckForUpdatesAsync(silentCheck: false);
        }

        /// <summary>
        /// Mevcut uygulama versiyonunu al
        /// </summary>
        public string GetCurrentVersion()
        {
            try
            {
                // Assembly versiyonunu al
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UpdateService] Versiyon bilgisi alınamadı: {ex.Message}", ex);
                return "1.0.0";
            }
        }

        /// <summary>
        /// Otomatik güncelleme ayarını değiştir
        /// </summary>
        public async Task SetAutoUpdateEnabledAsync(bool enabled)
        {
            _settingsManager.Settings.AutoUpdateEnabled = enabled;
            await _settingsManager.SaveSettingsAsync();
            LogService.LogInfo($"[UpdateService] Otomatik güncelleme: {(enabled ? "Açık" : "Kapalı")}");
        }

        /// <summary>
        /// Güncellemeleri kontrol etmek için geçen süreyi al
        /// </summary>
        public TimeSpan TimeSinceLastCheck()
        {
            return DateTime.Now - _settingsManager.Settings.LastUpdateCheck;
        }

        /// <summary>
        /// Bugün güncelleme kontrolü yapıldı mı?
        /// </summary>
        public bool IsCheckedToday()
        {
            return _settingsManager.Settings.LastUpdateCheck.Date == DateTime.Today;
        }

        /// <summary>
        /// Başlangıçta otomatik güncelleme kontrolü
        /// Günde bir kez, uygulama başlangıcından 10 saniye sonra
        /// </summary>
        public async Task StartupUpdateCheckAsync()
        {
            try
            {
                // Bugün zaten kontrol edildiyse atla
                if (IsCheckedToday())
                {
                    LogService.LogDebug("[UpdateService] Bugün zaten güncelleme kontrolü yapıldı, atlanıyor");
                    return;
                }

                // Otomatik güncelleme kapalıysa atla
                if (!_settingsManager.Settings.AutoUpdateEnabled)
                {
                    LogService.LogDebug("[UpdateService] Otomatik güncelleme kapalı");
                    return;
                }

                // UI yüklensin diye 10 saniye bekle
                await Task.Delay(10000);

                LogService.LogInfo("[UpdateService] Başlangıç güncelleme kontrolü başlatılıyor...");
                await CheckForUpdatesAsync(silentCheck: true);
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UpdateService] Başlangıç güncelleme kontrolü hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update XML URL'ini güncelle (test/production için)
        /// </summary>
        public static void SetUpdateUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                // Bu metod compile-time constant kullandığı için runtime'da değiştirilemez
                // Gerekirse settings'e eklenebilir
                LogService.LogWarning("[UpdateService] Update URL değiştirme özelliği henüz desteklenmiyor");
            }
        }

        /// <summary>
        /// GitHub Release'i tarayıcıda aç
        /// </summary>
        public void OpenReleasePage()
        {
            try
            {
                var releaseUrl = "https://github.com/SerkanGezici/QuadroAIPilot/releases";
                Process.Start(new ProcessStartInfo
                {
                    FileName = releaseUrl,
                    UseShellExecute = true
                });
                LogService.LogInfo($"[UpdateService] Release sayfası açıldı: {releaseUrl}");
            }
            catch (Exception ex)
            {
                LogService.LogError($"[UpdateService] Release sayfası açılamadı: {ex.Message}", ex);
            }
        }
    }
}
