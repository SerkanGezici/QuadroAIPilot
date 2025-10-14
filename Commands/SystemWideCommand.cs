using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Sistem genelinde çalışan, odak değişimi gerektirmeyen komutlar için sınıf.
    /// Örneğin: ses kontrolü, parlaklık ayarı gibi.
    /// </summary>
    public class SystemWideCommand : FocusAwareCommand
    {
        /// <summary>
        /// Sistem geneli komut oluşturucu.
        /// </summary>
        /// <param name="commandText">Komut metni</param>
        /// <param name="metadata">Komut meta verileri</param>
        /// <param name="apiService">Windows API servisi</param>
        public SystemWideCommand(string commandText, CommandMetadata metadata, WindowsApiService apiService)
            : base(commandText, metadata, apiService)
        {
            if (metadata.FocusType != CommandFocusType.SystemWide)
            {
                throw new ArgumentException("Bu sınıf sadece SystemWide odak türü ile kullanılabilir.", nameof(metadata));
            }
        }

        /// <summary>
        /// Sistem geneli komutlar için odak hazırlığı genellikle gerekmez.
        /// Sadece mevcut odağı kaydeder.
        /// </summary>
        protected override Task<bool> PrepareFocus()
        {
            Debug.WriteLine("[SystemWideCommand] Sistem geneli komut için odak değişimi gerekmez.");
            _originalWindow = ApiService.GetActiveWindow();
            return Task.FromResult(true);
        }

        /// <summary>
        /// Sistem geneli komutun yürütülmesi.
        /// </summary>
        protected override async Task<bool> ExecuteCore()
        {
            Debug.WriteLine($"[SystemWideCommand] Sistem geneli komut yürütülüyor: {CommandText}");
            
            try
            {
                // Klavye kısayolu veya API bazlı işlem gerçekleştir
                switch (Metadata.KeyCombination?.ToLowerInvariant())
                {
                    case "volumeup":
                        HotkeySender.VolumeUp();
                        break;
                    case "volumedown":
                        HotkeySender.VolumeDown();
                        break;
                    case "volumemute":
                        HotkeySender.VolumeMute();
                        break;
                    // Diğer sistem geneli komutlar burada eklenir
                    default:
                        if (!string.IsNullOrEmpty(Metadata.KeyCombination))
                        {
                            // Özel klavye kısayolunu çalıştır
                            await HotkeySender.SendCustomKeyCombination(Metadata.KeyCombination);
                        }
                        else
                        {
                            Debug.WriteLine($"[SystemWideCommand] Bilinmeyen komut: {CommandText}");
                            return false;
                        }
                        break;
                }
                
                Debug.WriteLine($"[SystemWideCommand] Sistem geneli komut başarıyla yürütüldü: {CommandText}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemWideCommand] Komut yürütme hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sistem geneli komutlar için odak geri getirmeye genellikle gerek yoktur.
        /// </summary>
        protected override Task<bool> RestoreFocus()
        {
            // Sistem geneli komutlar için odak geri getirmeye gerek yok
            return Task.FromResult(true);
        }
    }
}
