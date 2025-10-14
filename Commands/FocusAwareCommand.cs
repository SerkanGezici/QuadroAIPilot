using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Odak yönetimini içeren temel komut sınıfı.
    /// Tüm odak duyarlı komutlar bu sınıftan türetilmelidir.
    /// </summary>
    public abstract class FocusAwareCommand : ICommand
    {
        /// <summary>
        /// Komutun meta verileri.
        /// </summary>
        protected readonly CommandMetadata Metadata;

        /// <summary>
        /// Windows API servisine erişim.
        /// </summary>
        protected readonly WindowsApiService ApiService;

        /// <summary>
        /// Komut metni.
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// Orijinal uygulama penceresinin handle'ı (geri dönmek için).
        /// </summary>
        protected IntPtr _originalWindow;

        /// <summary>
        /// Hedef uygulama penceresinin handle'ı.
        /// </summary>
        protected IntPtr _targetWindow;

        /// <summary>
        /// Odak farkındalığı olan komut oluşturucu.
        /// </summary>
        /// <param name="commandText">Komut metni</param>
        /// <param name="metadata">Komut meta verileri</param>
        /// <param name="apiService">Windows API servisi</param>
        protected FocusAwareCommand(string commandText, CommandMetadata metadata, WindowsApiService apiService)
        {
            CommandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            ApiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        }

        /// <summary>
        /// Komutu yürütür. Odak yönetimi ve komut yürütme adımlarını sırasıyla gerçekleştirir.
        /// </summary>
        public async Task<bool> ExecuteAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[FocusAwareCommand][{stopwatch.ElapsedMilliseconds}ms] ===== BAŞLANGIÇ: Komut yürütülüyor: {CommandText} =====");

            try
            {
                // 1. Odak hazırlığı (örn: gerekirse pencere değiştirme)
                await PrepareFocus();

                // 2. Asıl komutu çalıştır
                bool result = await ExecuteCore();

                // 3. Odağı geri getir (gerekirse)
                if (Metadata.ReturnFocusAfterExecution)
                {
                    await RestoreFocus();
                }

                // 4. Sonucu raporla
                Debug.WriteLine($"[FocusAwareCommand][{stopwatch.ElapsedMilliseconds}ms] ===== BİTİŞ: Komut {(result ? "başarılı" : "başarısız")} =====");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusAwareCommand][{stopwatch.ElapsedMilliseconds}ms] HATA: {ex.Message}");
                try
                {
                    // Hata durumunda odağı geri getirmeye çalış
                    await RestoreFocus();
                }
                catch
                {
                    // Odak geri getirme sırasında hata oluşursa yoksay
                }
                return false;
            }
        }

        /// <summary>
        /// Komut yürütmeden önce gerekli odak hazırlığını yapar.
        /// Alt sınıflar tarafından uygulanmalıdır.
        /// </summary>
        protected abstract Task<bool> PrepareFocus();

        /// <summary>
        /// Asıl komut mantığını yürütür.
        /// Alt sınıflar tarafından uygulanmalıdır.
        /// </summary>
        protected abstract Task<bool> ExecuteCore();

        /// <summary>
        /// Odağı orijinal uygulamaya (QuadroAIPilot) geri getirir.
        /// Alt sınıflar tarafından uygulanmalıdır veya temel uygulamayı kullanabilir.
        /// </summary>
        protected virtual async Task<bool> RestoreFocus()
        {
            try
            {
                if (_originalWindow != IntPtr.Zero)
                {
                    Debug.WriteLine($"[FocusAwareCommand] Odak orijinal pencereye geri getiriliyor: {_originalWindow}");
                    
                    // İlk deneme: SetForegroundWindow API'si ile
                    if (ApiService.BringWindowToFront(_originalWindow))
                    {
                        Debug.WriteLine("[FocusAwareCommand] Odak başarıyla geri getirildi.");
                        await Task.Delay(100); // Odak değişikliğinin yerleşmesi için kısa bir bekleme
                        return true;
                    }
                    
                    // Gerekirse alternatif yöntemler kullanılabilir
                    Debug.WriteLine("[FocusAwareCommand] SetForegroundWindow başarısız oldu, alternatif yöntemler deneniyor.");
                    // Alternatif: Alt+Tab kullanarak geri gelme
                }
                else
                {
                    Debug.WriteLine("[FocusAwareCommand] Orijinal pencere tanımlayıcısı boş, odak geri getirme atlanıyor.");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusAwareCommand] Odak geri getirme hatası: {ex.Message}");
                return false;
            }
        }
    }
}
