using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace QuadroAIPilot.Services.WindowsAI.Interfaces
{
    /// <summary>
    /// Windows AI OCR (Optical Character Recognition) servisi
    /// Ekran görüntülerinden ve resimlerden metin çıkarımı yapar
    /// </summary>
    public interface ITextRecognitionService
    {
        /// <summary>
        /// Servis kullanılabilir mi? (NPU/GPU kontrolü)
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Dosya yolundan resimden metin çıkarımı
        /// </summary>
        /// <param name="imagePath">Resim dosyası yolu</param>
        /// <returns>Çıkarılan metin</returns>
        Task<string> ExtractTextFromFileAsync(string imagePath);

        /// <summary>
        /// SoftwareBitmap'ten metin çıkarımı
        /// </summary>
        /// <param name="bitmap">Görüntü bitmap</param>
        /// <returns>Çıkarılan metin</returns>
        Task<string> ExtractTextFromBitmapAsync(SoftwareBitmap bitmap);

        /// <summary>
        /// Ekran görüntüsünden metin çıkarımı
        /// </summary>
        /// <returns>Çıkarılan metin</returns>
        Task<string> ExtractTextFromScreenAsync();

        /// <summary>
        /// Ekran görüntüsünden belirli bir alandan metin çıkarımı
        /// </summary>
        /// <param name="x">X koordinatı</param>
        /// <param name="y">Y koordinatı</param>
        /// <param name="width">Genişlik</param>
        /// <param name="height">Yükseklik</param>
        /// <returns>Çıkarılan metin</returns>
        Task<string> ExtractTextFromScreenRegionAsync(int x, int y, int width, int height);

        /// <summary>
        /// Panodan görüntü alıp metin çıkarımı
        /// </summary>
        /// <returns>Çıkarılan metin veya null</returns>
        Task<string?> ExtractTextFromClipboardAsync();
    }
}
