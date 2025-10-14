using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Aktif pencerede çalışması gereken komutlar için sınıf (QuadroAIPilot dışında).
    /// Alt+Tab veya benzer yöntemlerle odak değişimi gerektirir.
    /// </summary>
    public class ActiveWindowCommand : FocusAwareCommand
    {
        /// <summary>
        /// Aktif pencere komutu oluşturucu.
        /// </summary>
        /// <param name="commandText">Komut metni</param>
        /// <param name="metadata">Komut meta verileri</param>
        /// <param name="apiService">Windows API servisi</param>
        public ActiveWindowCommand(string commandText, CommandMetadata metadata, WindowsApiService apiService)
            : base(commandText, metadata, apiService)
        {
            if (metadata.FocusType != CommandFocusType.ActiveWindow)
            {
                throw new ArgumentException("Bu sınıf sadece ActiveWindow odak türü ile kullanılabilir.", nameof(metadata));
            }
        }

        /// <summary>
        /// Odağı önceki pencereye taşır (Alt+Tab kullanarak).
        /// </summary>
        protected override async Task<bool> PrepareFocus()
        {
            Debug.WriteLine("[ActiveWindowCommand] Odak bir önceki pencereye taşınıyor (Alt+Tab)...");
            
            try
            {
                // Önce mevcut pencereyi kaydet (geri dönmek için)
                _originalWindow = ApiService.GetActiveWindow();
                Debug.WriteLine($"[ActiveWindowCommand] Orijinal pencere: {_originalWindow}");

                // Odağı önceki pencereye taşı (Alt+Tab)
                HotkeySender.AltTab();
                
                // Pencere değişimi için bekle
                await Task.Delay(Metadata.DelayAfterFocusChange);
                
                // Pencere değişimini doğrula
                _targetWindow = ApiService.GetActiveWindow();
                bool focusChanged = (_originalWindow != _targetWindow);
                
                Debug.WriteLine($"[ActiveWindowCommand] Hedef pencere: {_targetWindow}, Odak değişti mi: {focusChanged}");
                
                // Pencere değişmediyse tekrar dene
                if (!focusChanged)
                {
                    Debug.WriteLine("[ActiveWindowCommand] Pencere değişmedi! Tekrar Alt+Tab deneniyor...");
                    HotkeySender.AltTab();
                    await Task.Delay(Metadata.DelayAfterFocusChange);
                    
                    _targetWindow = ApiService.GetActiveWindow();
                    focusChanged = (_originalWindow != _targetWindow);
                    Debug.WriteLine($"[ActiveWindowCommand] İkinci deneme sonrası hedef pencere: {_targetWindow}, Odak değişti mi: {focusChanged}");
                }
                
                // Hedef pencereyi odaklama (bazen gerekebilir)
                if (focusChanged)
                {
                    HotkeySender.FocusTargetWindow();
                    await Task.Delay(100); // Kısa bir bekleme
                }
                
                return focusChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActiveWindowCommand] Odak taşıma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Aktif pencerede komutu yürütür.
        /// </summary>
        protected override async Task<bool> ExecuteCore()
        {
            Debug.WriteLine($"[ActiveWindowCommand] Komut yürütülüyor: {CommandText}");
            
            try
            {
                // Komutun yürütülmesi için gereken klavye kısayolunu gönder
                if (!string.IsNullOrEmpty(Metadata.KeyCombination))
                {
                    await HotkeySender.SendCustomKeyCombination(Metadata.KeyCombination);
                    
                    // Komutun etkisi için kısa bir bekleme
                    await Task.Delay(200);
                    
                    Debug.WriteLine($"[ActiveWindowCommand] Klavye kısayolu gönderildi: {Metadata.KeyCombination}");
                    return true;
                }
                else
                {
                    Debug.WriteLine("[ActiveWindowCommand] Komut için klavye kısayolu tanımlanmamış!");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActiveWindowCommand] Komut yürütme hatası: {ex.Message}");
                return false;
            }
        }
    }
}
