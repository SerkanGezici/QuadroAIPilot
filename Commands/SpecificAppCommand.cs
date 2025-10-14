using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Belirli bir uygulamada çalışması gereken komutlar için sınıf.
    /// Uygulamanın açık olup olmadığını kontrol eder, değilse açar.
    /// </summary>
    public class SpecificAppCommand : FocusAwareCommand
    {
        /// <summary>
        /// Belirli uygulama komutu oluşturucu.
        /// </summary>
        /// <param name="commandText">Komut metni</param>
        /// <param name="metadata">Komut meta verileri</param>
        /// <param name="apiService">Windows API servisi</param>
        public SpecificAppCommand(string commandText, CommandMetadata metadata, WindowsApiService apiService)
            : base(commandText, metadata, apiService)
        {
            if (metadata.FocusType != CommandFocusType.SpecificApp)
            {
                throw new ArgumentException("Bu sınıf sadece SpecificApp odak türü ile kullanılabilir.", nameof(metadata));
            }

            if (string.IsNullOrEmpty(metadata.TargetApplication))
            {
                throw new ArgumentException("SpecificApp komutları için hedef uygulama adı gereklidir.", nameof(metadata));
            }
        }

        /// <summary>
        /// Hedef uygulamanın açık olup olmadığını kontrol eder, açık değilse açar ve odağı taşır.
        /// </summary>
        protected override async Task<bool> PrepareFocus()
        {
            Debug.WriteLine($"[SpecificAppCommand] Hedef uygulamaya odak taşınıyor: {Metadata.TargetApplication}");
            
            try
            {
                // Önce mevcut pencereyi kaydet (geri dönmek için)
                _originalWindow = ApiService.GetActiveWindow();
                Debug.WriteLine($"[SpecificAppCommand] Orijinal pencere: {_originalWindow}");

                // Hedef uygulama çalışıyor mu kontrol et
                bool isRunning = ApiService.IsApplicationRunning(Metadata.TargetApplication);
                Debug.WriteLine($"[SpecificAppCommand] Hedef uygulama ({Metadata.TargetApplication}) çalışıyor mu? {isRunning}");

                if (!isRunning)
                {
                    // Uygulama çalışmıyorsa başlat
                    Debug.WriteLine($"[SpecificAppCommand] Hedef uygulama ({Metadata.TargetApplication}) başlatılıyor...");
                    await StartApplication();
                    
                    // Uygulamanın başlaması için bekle
                    await Task.Delay(2000); // Uygulama başlama süresi
                }
                else
                {
                    // Uygulama çalışıyorsa odağı ona taşı
                    Debug.WriteLine($"[SpecificAppCommand] Odak hedef uygulamaya taşınıyor: {Metadata.TargetApplication}");
                    bool switchSuccess = await SwitchToApplication();
                    
                    if (!switchSuccess)
                    {
                        Debug.WriteLine($"[SpecificAppCommand] Hedef uygulamaya geçiş başarısız!");
                        return false;
                    }
                }

                // Pencere değişimi için bekle
                await Task.Delay(Metadata.DelayAfterFocusChange);
                
                // Odak değişimini doğrula
                _targetWindow = ApiService.GetActiveWindow();
                bool focusChanged = (_originalWindow != _targetWindow);
                
                Debug.WriteLine($"[SpecificAppCommand] Hedef pencere: {_targetWindow}, Odak değişti mi: {focusChanged}");
                
                return focusChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecificAppCommand] Odak taşıma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Belirli uygulamada komutu yürütür.
        /// </summary>
        protected override async Task<bool> ExecuteCore()
        {
            Debug.WriteLine($"[SpecificAppCommand] Komut yürütülüyor: {CommandText}");
            
            try
            {
                // Kısa bir bekleme (uygulamanın hazır olması için)
                await Task.Delay(500);
                
                // Komutun yürütülmesi için gereken klavye kısayolunu gönder
                if (!string.IsNullOrEmpty(Metadata.KeyCombination))
                {
                    await HotkeySender.SendCustomKeyCombination(Metadata.KeyCombination);
                    
                    // Komutun etkisi için bekleme
                    await Task.Delay(500);
                    
                    Debug.WriteLine($"[SpecificAppCommand] Klavye kısayolu gönderildi: {Metadata.KeyCombination}");
                    return true;
                }
                else
                {
                    Debug.WriteLine("[SpecificAppCommand] Komut için klavye kısayolu tanımlanmamış!");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecificAppCommand] Komut yürütme hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hedef uygulamayı başlatır.
        /// </summary>
        private async Task<bool> StartApplication()
        {
            try
            {
                string appName = Metadata.TargetApplication;
                
                // Özel uygulama başlatma mantığı
                switch (appName.ToLowerInvariant())
                {
                    case "outlook":
                        Process.Start(new ProcessStartInfo { FileName = "outlook", UseShellExecute = true });
                        break;
                    case "chrome":
                    case "google chrome":
                        Process.Start(new ProcessStartInfo { FileName = "chrome", UseShellExecute = true });
                        break;
                    case "edge":
                    case "microsoft edge":
                        Process.Start(new ProcessStartInfo { FileName = "msedge", UseShellExecute = true });
                        break;
                    case "word":
                    case "microsoft word":
                        Process.Start(new ProcessStartInfo { FileName = "winword", UseShellExecute = true });
                        break;
                    case "excel":
                    case "microsoft excel":
                        Process.Start(new ProcessStartInfo { FileName = "excel", UseShellExecute = true });
                        break;
                    default:
                        Process.Start(new ProcessStartInfo { FileName = appName, UseShellExecute = true });
                        break;
                }
                
                // Uygulamanın başlaması için bir süre bekle
                await Task.Delay(2000);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecificAppCommand] Uygulama başlatma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Çalışan uygulamaya odağı taşır.
        /// </summary>
        private async Task<bool> SwitchToApplication()
        {
            try
            {
                string appName = Metadata.TargetApplication;
                
                // Uygulamaya odak taşıma - FindWindow ve SwitchToWindow kullanarak
                bool result = ApiService.SwitchToApplication(appName);
                
                if (!result)
                {
                    // Alt+Tab ile pencereleri dolaş
                    Debug.WriteLine("[SpecificAppCommand] SwitchToApplication başarısız oldu, Alt+Tab denenecek");
                    
                    for (int i = 0; i < 5; i++) // En fazla 5 pencere dene
                    {
                        HotkeySender.AltTab();
                        await Task.Delay(500);
                        
                        // Aktif pencere başlığını kontrol et
                        string windowTitle = ApiService.GetActiveWindowTitle();
                        
                        if (windowTitle.Contains(appName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"[SpecificAppCommand] Alt+Tab ile {appName} bulundu!");
                            return true;
                        }
                    }
                    
                    Debug.WriteLine($"[SpecificAppCommand] {appName} bulunamadı!");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecificAppCommand] Uygulamaya geçiş hatası: {ex.Message}");
                return false;
            }
        }
    }
}
