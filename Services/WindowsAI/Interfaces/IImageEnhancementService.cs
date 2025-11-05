using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace QuadroAIPilot.Services.WindowsAI.Interfaces
{
    /// <summary>
    /// Windows AI Super Resolution servisi
    /// Görüntü kalitesini artırma ve büyütme
    /// </summary>
    public interface IImageEnhancementService
    {
        /// <summary>
        /// Servis kullanılabilir mi? (NPU/GPU kontrolü)
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Resim dosyasını yüksek çözünürlüğe çıkar
        /// </summary>
        /// <param name="inputPath">Giriş resmi</param>
        /// <param name="outputPath">Çıkış resmi</param>
        /// <param name="scaleFactor">Büyütme oranı (2x, 4x)</param>
        /// <returns>İşlem başarılı mı?</returns>
        Task<bool> UpscaleImageAsync(string inputPath, string outputPath, int scaleFactor = 2);

        /// <summary>
        /// SoftwareBitmap'i yüksek çözünürlüğe çıkar
        /// </summary>
        /// <param name="bitmap">Giriş bitmap</param>
        /// <param name="scaleFactor">Büyütme oranı</param>
        /// <returns>Yüksek çözünürlüklü bitmap</returns>
        Task<SoftwareBitmap> UpscaleBitmapAsync(SoftwareBitmap bitmap, int scaleFactor = 2);

        /// <summary>
        /// Ekran görüntüsünü al ve yüksek çözünürlüğe çıkar
        /// </summary>
        /// <param name="outputPath">Çıkış dosya yolu</param>
        /// <param name="scaleFactor">Büyütme oranı</param>
        /// <returns>İşlem başarılı mı?</returns>
        Task<bool> UpscaleScreenshotAsync(string outputPath, int scaleFactor = 2);
    }
}
