using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
        private static readonly HttpClient _httpClient = new HttpClient();

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

                    // ✨ PROFESYONEL OTOMATIK GÜNCELLEME SİSTEMİ
                    AutoUpdater.Mandatory = false; // Zorunlu güncelleme değil
                    AutoUpdater.UpdateMode = Mode.Normal; // Normal mod (built-in UI)
                    AutoUpdater.ReportErrors = true; // Hataları kullanıcıya göster
                    AutoUpdater.ShowSkipButton = true; // "Atla" butonu
                    AutoUpdater.ShowRemindLaterButton = true; // "Daha sonra hatırlat" butonu

                    // 📥 OTOMATIK İNDİRME VE KURULUM (EXE Setup için)
                    AutoUpdater.DownloadPath = Path.Combine(Path.GetTempPath(), "QuadroAIPilot"); // Temp'e indir
                    AutoUpdater.RunUpdateAsAdmin = true; // Admin yetkileriyle kur

                    // 🎯 EXE Setup Modu (ZIP değil!)
                    // update.xml'de <url> direkt .exe dosyasını gösteriyor
                    // AutoUpdater otomatik olarak EXE'yi indirecek ve çalıştıracak

                    // 🎨 UI Özelleştirme
                    AutoUpdater.Icon = null; // Varsayılan Windows icon
                    AutoUpdater.AppTitle = "QuadroAI Pilot - Güncelleme"; // Dialog başlığı

                    // 📝 Changelog: update.xml'deki <changelog> otomatik gösterilir

                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - Event handlers ekleniyor ====");

                    // ✨ PROFESYONEL GÜNCELLEME SİSTEMİ - CheckForUpdateEvent handler ile özel indirme mantığı
                    AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;

                    // Update dialog'u kapatıldığında event (kurulum sonrası uygulama kapatılır)
                    AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;

                    _isConfigured = true;
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] ConfigureAutoUpdater - BAŞARIYLA TAMAMLANDI ====");
                    Log.Warning("[UpdateService] AutoUpdater.NET yapılandırıldı ✓ (Otomatik indirme aktif)");
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
        /// ✨ PROFESYONEL GÜNCELLEME SİSTEMİ - CheckForUpdateEvent Handler
        /// AutoUpdater.NET güncelleme tespit ettiğinde bu event tetiklenir
        /// Burada kendi indirme mantığımızı uyguluyoruz (ZipExtractor.exe hatası yok!)
        /// </summary>
        private async void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] CheckForUpdateEvent TETİKLENDİ ====");
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Güncelleme mevcut mu: {args != null && args.IsUpdateAvailable} ====");

            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    // ✅ GÜNCELLEME MEVCUT!
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] YENİ VERSİYON: {args.CurrentVersion} -> {args.InstalledVersion} ====");
                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İNDİRME URL: {args.DownloadURL} ====");
                    Log.Warning($"[UpdateService] Güncelleme mevcut: {args.CurrentVersion} -> {args.InstalledVersion}");

                    try
                    {
                        // 🎯 PROFESYONEL YAKLAŞIM: Kendi indirme mantığımız
                        await DownloadAndInstallUpdateAsync(args);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İNDİRME HATASI: {ex.Message} ====");
                        Log.Error(ex, "[UpdateService] Güncelleme indirme hatası: {Message}", ex.Message);

                        // Kullanıcıya hata göster
                        await ShowUpdateErrorDialogAsync($"Güncelleme indirilirken hata oluştu:\n{ex.Message}");
                    }
                }
                else
                {
                    // ℹ️ GÜNCELLEME YOK
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] Güncelleme yok - Uygulama güncel ====");
                    Log.Warning("[UpdateService] Uygulama güncel, güncelleme yok");

                    // Manuel kontrol ise kullanıcıya bilgi ver
                    await ShowNoUpdateDialogAsync();
                }
            }
            else
            {
                // ❌ HATA
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] HATA: {args.Error.Message} ====");
                Log.Error(args.Error, "[UpdateService] Güncelleme kontrolü hatası: {Message}", args.Error.Message);

                // Kullanıcıya hata göster
                await ShowUpdateErrorDialogAsync($"Güncelleme kontrolü sırasında hata oluştu:\n{args.Error.Message}");
            }
        }

        /// <summary>
        /// 📥 OTOMATIK İNDİRME VE KURULUM - Profesyonel sistem
        /// </summary>
        private async Task DownloadAndInstallUpdateAsync(UpdateInfoEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] DownloadAndInstallUpdateAsync BAŞLADI ====");

            // Kullanıcıya sor
            var userConfirmed = await ShowUpdateConfirmationDialogAsync(args);
            if (!userConfirmed)
            {
                System.Diagnostics.Debug.WriteLine("==== [UpdateService] Kullanıcı güncellemeyi reddetti ====");
                Log.Warning("[UpdateService] Kullanıcı güncellemeyi reddetti");
                return;
            }

            // Temp klasörü oluştur
            var tempFolder = Path.Combine(Path.GetTempPath(), "QuadroAIPilot");
            Directory.CreateDirectory(tempFolder);

            var setupFileName = Path.GetFileName(args.DownloadURL);
            var setupFilePath = Path.Combine(tempFolder, setupFileName);

            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İndirme hedefi: {setupFilePath} ====");
            Log.Warning($"[UpdateService] Setup indiriliyor: {setupFileName}");

            try
            {
                // 🔽 HTTP İLE İNDİRME (Progress tracking ile)
                using (var response = await _httpClient.GetAsync(args.DownloadURL, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var downloadedBytes = 0L;

                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Toplam boyut: {totalBytes} bytes ====");

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(setupFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        var lastReportedProgress = -1;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;

                            // Progress tracking - sadece %10'luk değişimlerde log bas
                            if (totalBytes > 0)
                            {
                                var progress = (int)((downloadedBytes * 100) / totalBytes);
                                if (progress >= lastReportedProgress + 10)
                                {
                                    lastReportedProgress = progress;
                                    System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İndirme ilerlemesi: {progress}% ====");
                                    Log.Warning($"[UpdateService] İndirme: {progress}%");
                                }
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] İndirme TAMAMLANDI ====");
                Log.Warning($"[UpdateService] Setup indirildi: {setupFilePath}");

                // ✅ DOSYA İNDİRİLDİ - KURULUMU BAŞLAT
                await LaunchSetupAsync(setupFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İndirme hatası: {ex.Message} ====");
                Log.Error(ex, "[UpdateService] Setup indirme hatası: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 🚀 KURULUMU BAŞLAT - Admin yetkileriyle ve sessiz modda
        /// </summary>
        private async Task LaunchSetupAsync(string setupFilePath)
        {
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] LaunchSetupAsync BAŞLADI: {setupFilePath} ====");
            Log.Warning($"[UpdateService] Kurulum başlatılıyor: {setupFilePath}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = setupFilePath,
                    UseShellExecute = true,
                    Verb = "runas", // Admin yetkileri
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS" // Sessiz kurulum
                };

                System.Diagnostics.Debug.WriteLine("==== [UpdateService] Process.Start() çağrılıyor ====");
                var process = Process.Start(startInfo);

                if (process != null)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] Kurulum başlatıldı, uygulama kapatılıyor ====");
                    Log.Warning("[UpdateService] Kurulum başlatıldı, uygulama kapatılıyor");

                    // WinUI 3 uygulamasını kapat (WPF değil!)
                    await Task.Delay(500); // Kurulum başlasın diye kısa bir gecikme

                    // Environment.Exit kullan - WinUI 3'te güvenli
                    Environment.Exit(0);
                }
                else
                {
                    throw new Exception("Setup process başlatılamadı");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Kurulum başlatma hatası: {ex.Message} ====");
                Log.Error(ex, "[UpdateService] Kurulum başlatma hatası: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 💬 KULLANICIYA GÜNCELLEME ONAYI SOR
        /// </summary>
        private async Task<bool> ShowUpdateConfirmationDialogAsync(UpdateInfoEventArgs args)
        {
            // TODO: WinUI 3 ContentDialog ile profesyonel bir dialog göster
            // Şimdilik basit bir onay sistemi
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] Kullanıcı onayı bekleniyor (şimdilik otomatik true) ====");

            // Geçici olarak otomatik true döndür - sonra dialog eklenecek
            return await Task.FromResult(true);
        }

        /// <summary>
        /// ℹ️ GÜNCELLEME YOK DIALOG'U
        /// </summary>
        private async Task ShowNoUpdateDialogAsync()
        {
            // TODO: WinUI 3 ContentDialog ile "Uygulama güncel" mesajı
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] 'Uygulama güncel' dialog'u gösterilmeli (şimdilik log) ====");
            await Task.CompletedTask;
        }

        /// <summary>
        /// ❌ HATA DIALOG'U
        /// </summary>
        private async Task ShowUpdateErrorDialogAsync(string errorMessage)
        {
            // TODO: WinUI 3 ContentDialog ile hata mesajı
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Hata dialog'u gösterilmeli: {errorMessage} ====");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Uygulama çıkış event'i (güncelleme için kapatma)
        /// </summary>
        private void AutoUpdater_ApplicationExitEvent()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] ApplicationExitEvent TETİKLENDİ ====");
            Log.Warning("[UpdateService] Uygulama güncelleme için kapatılıyor");
            Environment.Exit(0);
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
        /// AutoUpdater.NET'in built-in dialog'unu gösterir
        /// Otomatik indirme ve kurulum özelliği aktif
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
        /// Mevcut uygulama versiyonunu al (Hibrit format: "1.2.1 (Build 19)")
        /// </summary>
        public string GetCurrentVersion()
        {
            try
            {
                // Assembly versiyonunu al (Major.Minor.Build format)
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var displayVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.2.1";

                // Registry'den build numarasını oku (Inno Setup tarafından yazılmış)
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\QuadroAI\QuadroAIPilot"))
                    {
                        if (key != null)
                        {
                            var buildNumber = key.GetValue("BuildNumber") as string;
                            if (!string.IsNullOrEmpty(buildNumber))
                            {
                                return $"{displayVersion} (Build {buildNumber})";
                            }
                        }
                    }
                }
                catch
                {
                    // Registry okunamazsa sadece versiyon döndür
                }

                return displayVersion;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] Versiyon bilgisi alınamadı: {Message}", ex.Message);
                return "1.2.1";
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
