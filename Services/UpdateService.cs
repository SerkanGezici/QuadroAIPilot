using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoUpdaterDotNET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuadroAIPilot.Dialogs;
using QuadroAIPilot.Helpers;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Services;
using Serilog;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// GitHub Releases tabanlı otomatik güncelleme servisi
    /// AutoUpdater.NET kütüphanesini kullanır
    /// Modern UI/UX dialog'ları ile kullanıcı dostu deneyim
    /// </summary>
    public class UpdateService
    {
        private readonly SettingsManager _settingsManager;
        private static UpdateService? _instance;
        private const string UPDATE_XML_URL = "https://raw.githubusercontent.com/SerkanGezici/QuadroAIPilot/main/update.xml";
        private bool _isConfigured = false;
        private readonly object _configLock = new object();
        private static readonly HttpClient _httpClient = new HttpClient();

        // XamlRoot referansı (dialog'lar için gerekli)
        private XamlRoot? _xamlRoot;

        // Otomatik kontrol için silent mode flag
        private bool _isSilentCheck = true;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static UpdateService Instance => _instance ??= new UpdateService();

        private UpdateService()
        {
            _settingsManager = SettingsManager.Instance;
            // AutoUpdater.NET UI thread'de çalışmalı - constructor'da yapma
            // İlk kullanımda ConfigureAutoUpdater() çağrılacak

            // HttpClient timeout ayarla (büyük dosyalar için)
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// XamlRoot'u ayarla (MainWindow'dan çağrılmalı)
        /// Dialog'ların gösterilmesi için gerekli
        /// </summary>
        public void SetXamlRoot(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
            Log.Information("[UpdateService] XamlRoot ayarlandı");
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
        /// 📥 MODERN İNDİRME VE KURULUM SİSTEMİ
        /// Kullanıcı dostu dialog'larla adım adım rehberlik
        /// </summary>
        private async Task DownloadAndInstallUpdateAsync(UpdateInfoEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] DownloadAndInstallUpdateAsync BAŞLADI ====");

            try
            {
                // 1. ADIM: Kullanıcıya güncelleme bilgilerini göster ve onay al
                var userWantsToDownload = await ShowUpdateConfirmationDialogAsync(args);
                if (!userWantsToDownload)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] Kullanıcı indirmeyi reddetti ====");
                    Log.Warning("[UpdateService] Kullanıcı güncelleme indirmesini iptal etti");
                    return;
                }

                // 2. ADIM: Dosya boyutunu HTTP HEAD request ile al
                long fileSize = 0;
                try
                {
                    using (var headRequest = new HttpRequestMessage(HttpMethod.Head, args.DownloadURL))
                    {
                        var headResponse = await _httpClient.SendAsync(headRequest);
                        fileSize = headResponse.Content.Headers.ContentLength ?? 0;
                    }
                }
                catch
                {
                    // Dosya boyutu alınamazsa 0 olarak devam et (progress dialog "Bilinmiyor" gösterir)
                    fileSize = 0;
                }

                // 3. ADIM: İndirme dialog'u ile dosyayı indir
                string setupFilePath;
                try
                {
                    setupFilePath = await DownloadUpdateWithProgressDialogAsync(args, fileSize);
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("[UpdateService] Kullanıcı indirmeyi iptal etti");
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[UpdateService] İndirme hatası");
                    await ShowUpdateErrorDialogAsync($"İndirme sırasında hata oluştu:\n\n{ex.Message}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] İndirme tamamlandı: {setupFilePath} ====");

                // 3. ADIM: Kurulum onayı al
                var userWantsToInstall = await ShowInstallConfirmationDialogAsync();
                if (!userWantsToInstall)
                {
                    Log.Information("[UpdateService] Kullanıcı kurulumu erteledi. Dosya saklandı: {Path}", setupFilePath);
                    return;
                }

                // 4. ADIM: Kurulumu başlat
                await LaunchSetupAsync(setupFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Genel hata: {ex.Message} ====");
                Log.Error(ex, "[UpdateService] Güncelleme işlemi genel hatası");
                await ShowUpdateErrorDialogAsync($"Güncelleme sırasında beklenmeyen hata:\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// İndirme progress dialog'u ile dosyayı indir
        /// </summary>
        private async Task<string> DownloadUpdateWithProgressDialogAsync(UpdateInfoEventArgs args, long fileSize)
        {
            // Temp klasörü oluştur
            var tempFolder = Path.Combine(Path.GetTempPath(), "QuadroAIPilot");
            Directory.CreateDirectory(tempFolder);

            var setupFileName = Path.GetFileName(args.DownloadURL);
            var setupFilePath = Path.Combine(tempFolder, setupFileName);

            // Progress dialog oluştur
            var progressDialog = new UpdateProgressDialog(setupFileName, fileSize);

            if (_xamlRoot != null)
            {
                progressDialog.XamlRoot = _xamlRoot;
            }

            // Dialog'u non-blocking göster
            _ = progressDialog.ShowAsync();

            try
            {
                // HTTP indirme
                using (var response = await _httpClient.GetAsync(
                    args.DownloadURL,
                    HttpCompletionOption.ResponseHeadersRead,
                    progressDialog.CancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? fileSize;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(setupFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var downloadedBytes = 0L;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(
                            buffer, 0, buffer.Length, progressDialog.CancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, progressDialog.CancellationToken);
                            downloadedBytes += bytesRead;

                            // Progress güncelle
                            progressDialog.UpdateProgress(downloadedBytes);
                        }
                    }
                }

                // İndirme tamamlandı, dialog'u kapat
                progressDialog.Hide();

                // Dialog kapanma animasyonu tamamlansın (ContentDialog conflict önleme)
                await Task.Delay(300);

                Log.Information("[UpdateService] İndirme başarıyla tamamlandı: {Path}", setupFilePath);
                return setupFilePath;
            }
            catch (OperationCanceledException)
            {
                progressDialog.Hide();

                // Dialog kapanma animasyonu tamamlansın
                await Task.Delay(300);

                Log.Warning("[UpdateService] İndirme kullanıcı tarafından iptal edildi");

                // İndirilen kısmi dosyayı sil
                if (File.Exists(setupFilePath))
                {
                    try { File.Delete(setupFilePath); } catch { }
                }

                throw;
            }
            catch (Exception ex)
            {
                progressDialog.Hide();

                // Dialog kapanma animasyonu tamamlansın
                await Task.Delay(300);

                Log.Error(ex, "[UpdateService] İndirme sırasında hata");
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
        /// 💬 KULLANICIYA GÜNCELLEME BİLGİLERİNİ GÖSTER VE ONAY AL
        /// Modern UpdateNotificationDialog ile
        /// </summary>
        private async Task<bool> ShowUpdateConfirmationDialogAsync(UpdateInfoEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] UpdateNotificationDialog gösteriliyor ====");

            try
            {
                // XamlRoot null ise bekle (MainWindow yüklenene kadar)
                for (int wait = 0; wait < 10 && _xamlRoot == null; wait++)
                {
                    Log.Warning("[UpdateService] XamlRoot henüz null, {Wait}/10 bekleniyor...", wait + 1);
                    await Task.Delay(500); // 0.5 saniye bekle
                }

                // Hala null ise hata
                if (_xamlRoot == null)
                {
                    Log.Error("[UpdateService] XamlRoot 5 saniye sonra hala null! Dialog gösterilemedi.");
                    return false;
                }

                // Retry logic: Başka dialog açıksa kısa bekle
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        // Dialog oluştur ve göster (button click zaten UI thread'de)
                        var dialog = new UpdateNotificationDialog(args)
                        {
                            XamlRoot = _xamlRoot
                        };

                        var result = await dialog.ShowAsync();
                        bool userAccepted = result == ContentDialogResult.Primary;

                        Log.Information("[UpdateService] Kullanıcı güncelleme onayı: {Accepted}", userAccepted);
                        return userAccepted;
                    }
                    catch (System.Runtime.InteropServices.COMException comEx) when (comEx.Message.Contains("ContentDialog"))
                    {
                        // Başka dialog açık, kısa bekle ve tekrar dene
                        Log.Warning("[UpdateService] Başka ContentDialog açık, {Retry}/3 tekrar deneniyor...", retry + 1);
                        await Task.Delay(1000);

                        if (retry == 2)
                        {
                            Log.Error("[UpdateService] Dialog gösterilemedi - Başka dialog açık ve kapanmadı");
                            return false;
                        }
                    }
                    catch (ArgumentException argEx) when (argEx.Message.Contains("XamlRoot"))
                    {
                        // XamlRoot hatası - bekle ve tekrar dene
                        Log.Warning("[UpdateService] XamlRoot hatası, {Retry}/3 tekrar deneniyor...", retry + 1);
                        await Task.Delay(1000);

                        if (retry == 2)
                        {
                            Log.Error("[UpdateService] XamlRoot hatası - 3 deneme sonrası başarısız");
                            return false;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] UpdateNotificationDialog gösterilirken beklenmeyen hata: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// ✅ KURULUM ONAYI AL
        /// İndirme tamamlandıktan sonra kurulum için onay
        /// </summary>
        private async Task<bool> ShowInstallConfirmationDialogAsync()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] InstallConfirmationDialog gösteriliyor ====");

            try
            {
                // XamlRoot null ise bekle
                for (int wait = 0; wait < 10 && _xamlRoot == null; wait++)
                {
                    Log.Warning("[UpdateService] [Install] XamlRoot henüz null, {Wait}/10 bekleniyor...", wait + 1);
                    await Task.Delay(500);
                }

                if (_xamlRoot == null)
                {
                    Log.Error("[UpdateService] [Install] XamlRoot 5 saniye sonra hala null!");
                    return false;
                }

                // Retry logic
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        // Dialog oluştur ve göster (zaten UI thread'deyiz)
                        var dialog = new UpdateInstallConfirmationDialog()
                        {
                            XamlRoot = _xamlRoot
                        };

                        var result = await dialog.ShowAsync();
                        bool userAccepted = result == ContentDialogResult.Primary;

                        Log.Information("[UpdateService] Kullanıcı kurulum onayı: {Accepted}", userAccepted);
                        return userAccepted;
                    }
                    catch (System.Runtime.InteropServices.COMException comEx) when (comEx.Message.Contains("ContentDialog"))
                    {
                        Log.Warning("[UpdateService] Install dialog - Başka ContentDialog açık, {Retry}/3 tekrar deneniyor...", retry + 1);
                        await Task.Delay(1000);

                        if (retry == 2)
                        {
                            Log.Error("[UpdateService] Install dialog gösterilemedi - Başka dialog açık");
                            return false;
                        }
                    }
                    catch (ArgumentException argEx) when (argEx.Message.Contains("XamlRoot"))
                    {
                        Log.Warning("[UpdateService] [Install] XamlRoot hatası, {Retry}/3 tekrar deneniyor...", retry + 1);
                        await Task.Delay(1000);

                        if (retry == 2)
                        {
                            Log.Error("[UpdateService] [Install] XamlRoot hatası - 3 deneme sonrası başarısız");
                            return false;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] InstallConfirmationDialog gösterilirken hata: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// ℹ️ GÜNCELLEME YOK DIALOG'U
        /// Manuel kontrolde güncelleme yoksa gösterilir
        /// </summary>
        private async Task ShowNoUpdateDialogAsync()
        {
            System.Diagnostics.Debug.WriteLine("==== [UpdateService] 'Güncelleme yok' dialog'u gösteriliyor ====");

            try
            {
                // Sadece manuel kontrolde göster
                if (_isSilentCheck)
                {
                    Log.Information("[UpdateService] Otomatik kontrol - güncelleme yok mesajı atlandı");
                    return;
                }

                // XamlRoot kontrolü
                if (_xamlRoot == null)
                {
                    Log.Warning("[UpdateService] XamlRoot null! Dialog gösterilemedi.");
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "Uygulama Güncel",
                    Content = "QuadroAI Pilot güncel durumda.\n\nEn son sürümü kullanıyorsunuz.",
                    CloseButtonText = "Tamam",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = _xamlRoot
                };

                await dialog.ShowAsync();
                Log.Information("[UpdateService] 'Güncelleme yok' mesajı gösterildi");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] 'Güncelleme yok' dialog'u gösterilirken hata");
            }
        }

        /// <summary>
        /// ❌ HATA DIALOG'U
        /// Güncelleme sırasında hata oluştuğunda gösterilir
        /// </summary>
        private async Task ShowUpdateErrorDialogAsync(string errorMessage)
        {
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] Hata dialog'u gösteriliyor: {errorMessage} ====");

            try
            {
                // XamlRoot kontrolü
                if (_xamlRoot == null)
                {
                    Log.Warning("[UpdateService] XamlRoot null! Hata dialog'u gösterilemedi.");
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "Güncelleme Hatası",
                    Content = errorMessage,
                    CloseButtonText = "Tamam",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = _xamlRoot
                };

                await dialog.ShowAsync();
                Log.Error("[UpdateService] Hata mesajı gösterildi: {Error}", errorMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] Hata dialog'u gösterilirken hata");
            }
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
        /// <param name="silentCheck">Sessiz kontrol (otomatik) veya manuel kontrol</param>
        /// <returns></returns>
        public Task CheckForUpdatesAsync(bool silentCheck = true)
        {
            System.Diagnostics.Debug.WriteLine($"==== [UpdateService] CheckForUpdatesAsync BAŞLADI (silentCheck: {silentCheck}) ====");

            try
            {
                // Silent mode flag'ini kaydet (dialog'larda kullanılacak)
                _isSilentCheck = silentCheck;

                // AutoUpdater.NET'i yapılandır (ilk kez çağrıldığında)
                ConfigureAutoUpdater();

                // Otomatik güncelleme kapalıysa ve sessiz kontrol ise çık
                if (silentCheck && !_settingsManager.Settings.AutoUpdateEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("==== [UpdateService] Otomatik güncelleme kapalı, kontrol atlanıyor ====");
                    Log.Warning("[UpdateService] Otomatik güncelleme kapalı, kontrol atlanıyor");
                    return Task.CompletedTask;
                }

                // Son kontrol zamanını güncelle
                _settingsManager.Settings.LastUpdateCheck = DateTime.Now;
                _ = _settingsManager.SaveSettingsAsync();

                Log.Information("[UpdateService] Güncelleme kontrolü başlatılıyor - Mod: {Mode}",
                    silentCheck ? "Otomatik (Sessiz)" : "Manuel");
                Log.Information("[UpdateService] URL: {URL}", UPDATE_XML_URL);
                Log.Information("[UpdateService] Mevcut versiyon: {Version}", GetCurrentVersion());

                // Güncelleme kontrolü başlat
                AutoUpdater.Start(UPDATE_XML_URL);

                Log.Information("[UpdateService] AutoUpdater.Start() çağrıldı");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateService] Güncelleme kontrolü hatası");
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
        /// Mevcut uygulama versiyonunu al (BuildInfoHelper'dan okur)
        /// </summary>
        public string GetCurrentVersion()
        {
            return Helpers.BuildInfoHelper.GetFullVersion();
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
