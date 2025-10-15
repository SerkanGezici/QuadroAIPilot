using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AutoUpdaterDotNET;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Services;
using Serilog;

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
        private bool _isConfigured = false;
        private readonly object _configLock = new object();

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static UpdateService Instance => _instance ??= new UpdateService();

        private UpdateService()
        {
            _settingsManager = SettingsManager.Instance;
            // AutoUpdater.NET UI thread'de çalışmalı - constructor'da yapma
            // İlk kullanımda ConfigureAutoUpdater() çağrılacak
        }

        /// <summary>
        /// AutoUpdater.NET konfigürasyonu (Lazy initialization - UI thread'de çalışır)
        /// </summary>
        private void ConfigureAutoUpdater()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater METODU ÇAĞRILDI ====");

            lock (_configLock)
            {
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - Lock alındı ====");

                if (_isConfigured)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - Zaten yapılandırılmış, atlanıyor ====");
                    return;
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - TRY bloğu başladı ====");
                    Log.Warning("[UpdateService] AutoUpdater.NET yapılandırılıyor...");

                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - AutoUpdater özellikleri ayarlanıyor ====");
                    // Türkçe dil desteği
                    AutoUpdater.Mandatory = false;
                    AutoUpdater.UpdateMode = Mode.Normal;
                    AutoUpdater.ReportErrors = true;
                    AutoUpdater.ShowSkipButton = true;
                    AutoUpdater.ShowRemindLaterButton = true;

                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - Event handlers ekleniyor ====");
                    // Update dialog'u kapatıldığında event
                    AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;

                    // Güncelleme kontrolü başarısız olursa
                    AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;

                    _isConfigured = true;
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - BAŞARIYLA TAMAMLANDI ====");
                    Log.Warning("[UpdateService] AutoUpdater.NET yapılandırıldı ✓");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] ConfigureAutoUpdater - EXCEPTION: {ex.Message} ====");
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] ConfigureAutoUpdater - STACK TRACE: {ex.StackTrace} ====");
                    Log.Error(ex, "[UpdateService] AutoUpdater yapılandırma hatası: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Update kontrolü event handler
        /// </summary>
        private void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent TETİKLENDİ ====");

            try
            {
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent - TRY bloğu başladı ====");
                Log.Warning("[UpdateService] CheckForUpdateEvent tetiklendi");

                if (args == null)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent - args NULL! ====");
                    Log.Warning("[UpdateService] UpdateInfoEventArgs null!");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent - args geçerli ====");

                if (args.Error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - ERROR: {args.Error.Message} ====");
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - ERROR STACK: {args.Error.StackTrace} ====");
                    Log.Error(args.Error, "[UpdateService] Güncelleme kontrolü hatası: {Message}", args.Error.Message);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - InstalledVersion: {args.InstalledVersion} ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - CurrentVersion: {args.CurrentVersion} ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - IsUpdateAvailable: {args.IsUpdateAvailable} ====");

                Log.Warning("[UpdateService] Mevcut versiyon: {InstalledVersion}", args.InstalledVersion);
                Log.Warning("[UpdateService] Sunucu versiyonu: {CurrentVersion}", args.CurrentVersion);
                Log.Warning("[UpdateService] Güncelleme mevcut: {IsUpdateAvailable}", args.IsUpdateAvailable);

                if (!args.IsUpdateAvailable)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent - Güncelleme YOK ====");
                    Log.Warning("[UpdateService] Güncelleme yok, uygulama güncel");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - YENİ VERSİYON BULUNDU: {args.InstalledVersion} → {args.CurrentVersion} ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - Download URL: {args.DownloadURL} ====");

                Log.Warning("[UpdateService] Yeni versiyon bulundu: {InstalledVersion} → {CurrentVersion}", args.InstalledVersion, args.CurrentVersion);
                Log.Warning("[UpdateService] İndirme URL: {DownloadURL}", args.DownloadURL);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - EXCEPTION: {ex.Message} ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdateEvent - STACK TRACE: {ex.StackTrace} ====");
                Log.Error(ex, "[UpdateService] CheckForUpdateEvent hatası: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Uygulama çıkış event'i (güncelleme için kapatma)
        /// </summary>
        private void AutoUpdater_ApplicationExitEvent()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] ApplicationExitEvent TETİKLENDİ ====");
            Log.Warning("[UpdateService] Uygulama güncelleme için kapatılıyor");
            System.Windows.Application.Current?.Shutdown();
        }

        /// <summary>
        /// Güncellemeleri kontrol et (UI thread'de)
        /// </summary>
        /// <param name="silentCheck">Sessiz kontrol (UI gösterme)</param>
        /// <returns></returns>
        public Task CheckForUpdatesAsync(bool silentCheck = true)
        {
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync BAŞLADI (silentCheck: {silentCheck}) ====");

            try
            {
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - TRY bloğu başladı ====");

                // AutoUpdater.NET'i yapılandır (ilk kez çağrıldığında)
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - ConfigureAutoUpdater() çağrılıyor ====");
                ConfigureAutoUpdater();
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - ConfigureAutoUpdater() tamamlandı ====");

                // Otomatik güncelleme kapalıysa ve sessiz kontrol ise çık
                if (silentCheck && !_settingsManager.Settings.AutoUpdateEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - Otomatik güncelleme kapalı, çıkılıyor ====");
                    Log.Warning("[UpdateService] Otomatik güncelleme kapalı, kontrol atlanıyor");
                    return Task.CompletedTask;
                }

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - Son kontrol zamanı güncelleniyor ====");
                // Son kontrol zamanını güncelle
                _settingsManager.Settings.LastUpdateCheck = DateTime.Now;
                _ = _settingsManager.SaveSettingsAsync();

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - Güncelleme kontrolü parametreleri loglanıyor ====");
                Log.Warning("[UpdateService] Güncelleme kontrolü başlatılıyor... (Silent: {SilentCheck})", silentCheck);
                Log.Warning("[UpdateService] URL: {URL}", UPDATE_XML_URL);
                Log.Warning("[UpdateService] Mevcut versiyon: {Version}", GetCurrentVersion());

                // AutoUpdater.NET kullanarak kontrol
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync - AutoUpdater.UpdateMode ayarlanıyor (silent: {silentCheck}) ====");
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
                    AutoUpdater.UpdateMode = Mode.Normal;
                }

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - AutoUpdater.Start() ÇAĞRILIYOR ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync - URL: {UPDATE_XML_URL} ====");
                Log.Warning("[UpdateService] AutoUpdater.Start() çağrılıyor...");

                // Güncelleme kontrolü başlat (UI thread'de çalışmalı)
                AutoUpdater.Start(UPDATE_XML_URL);

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - AutoUpdater.Start() TAMAMLANDI ====");
                Log.Warning("[UpdateService] AutoUpdater.Start() çağrısı tamamlandı");

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesAsync - Task.CompletedTask dönülüyor ====");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync - EXCEPTION: {ex.Message} ====");
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync - STACK TRACE: {ex.StackTrace} ====");
                Log.Error(ex, "[UpdateService] Güncelleme kontrolü hatası: {Message}", ex.Message);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Güncelleme kontrolünü zorla (manuel)
        /// UI gösterir, kullanıcı etkileşimlidir
        /// </summary>
        public async Task CheckForUpdatesManualAsync()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesManualAsync BAŞLADI ====");
            System.Diagnostics.Debug.WriteLine("==== CHECK FOR UPDATES MANUEL BUTTON CLICKED ====");
            Console.WriteLine("==== CHECK FOR UPDATES MANUEL BUTTON CLICKED ====");
            Log.Warning("[UpdateService] Manuel güncelleme kontrolü başlatılıyor...");

            System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesManualAsync - CheckForUpdatesAsync(false) çağrılıyor ====");
            await CheckForUpdatesAsync(silentCheck: false);
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdatesManualAsync - CheckForUpdatesAsync(false) TAMAMLANDI ====");
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
                Log.Error(ex, "[UpdateService] Versiyon bilgisi alınamadı: {Message}", ex.Message);
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
            Log.Warning("[UpdateService] Otomatik güncelleme: {Status}", enabled ? "Açık" : "Kapalı");
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
                    Log.Warning("[UpdateService] Bugün zaten güncelleme kontrolü yapıldı, atlanıyor");
                    return;
                }

                // Otomatik güncelleme kapalıysa atla
                if (!_settingsManager.Settings.AutoUpdateEnabled)
                {
                    Log.Warning("[UpdateService] Otomatik güncelleme kapalı");
                    return;
                }

                // UI yüklensin diye 10 saniye bekle
                await Task.Delay(10000);

                Log.Warning("[UpdateService] Başlangıç güncelleme kontrolü başlatılıyor...");
                await CheckForUpdatesAsync(silentCheck: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] Başlangıç güncelleme kontrolü hatası: {Message}", ex.Message);
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
                Log.Warning("[UpdateService] Update URL değiştirme özelliği henüz desteklenmiyor");
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
                Log.Warning("[UpdateService] Release sayfası açıldı: {ReleaseUrl}", releaseUrl);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] Release sayfası açılamadı: {Message}", ex.Message);
            }
        }
    }
}
