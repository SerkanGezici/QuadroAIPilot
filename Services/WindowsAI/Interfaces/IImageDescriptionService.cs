using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace QuadroAIPilot.Services.WindowsAI.Interfaces
{
    /// <summary>
    /// Windows AI Florence Image Encoder servisi
    /// Görüntü içeriğini analiz eder ve açıklama üretir
    /// </summary>
    public interface IImageDescriptionService
    {
        /// <summary>
        /// Servis kullanılabilir mi? (NPU/GPU kontrolü)
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Resim dosyasından görsel açıklama oluştur
        /// </summary>
        /// <param name="imagePath">Resim dosyası yolu</param>
        /// <param name="language">Dil kodu (tr-TR, en-US)</param>
        /// <returns>Görsel açıklaması</returns>
        Task<string> DescribeImageAsync(string imagePath, string language = "tr-TR");

        /// <summary>
        /// SoftwareBitmap'ten görsel açıklama oluştur
        /// </summary>
        /// <param name="bitmap">Görüntü bitmap</param>
        /// <param name="language">Dil kodu</param>
        /// <returns>Görsel açıklaması</returns>
        Task<string> DescribeBitmapAsync(SoftwareBitmap bitmap, string language = "tr-TR");

        /// <summary>
        /// Ekran görüntüsünden görsel açıklama oluştur
        /// </summary>
        /// <param name="language">Dil kodu</param>
        /// <returns>Görsel açıklaması</returns>
        Task<string> DescribeScreenAsync(string language = "tr-TR");

        /// <summary>
        /// Görüntüde belirli nesneleri tespit et
        /// </summary>
        /// <param name="imagePath">Resim dosyası yolu</param>
        /// <returns>Tespit edilen nesneler listesi</returns>
        Task<string[]> DetectObjectsAsync(string imagePath);

        /// <summary>
        /// Panodan görüntü alıp açıklama oluştur
        /// </summary>
        /// <param name="language">Dil kodu</param>
        /// <returns>Görsel açıklaması veya null</returns>
        Task<string?> DescribeClipboardImageAsync(string language = "tr-TR");
    }
}
