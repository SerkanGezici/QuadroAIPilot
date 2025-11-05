using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using QuadroAIPilot.Services.WindowsAI.Interfaces;

namespace QuadroAIPilot.Services.WindowsAI
{
    /// <summary>
    /// Windows AI Florence Image Encoder implementasyonu
    /// NOT: Bu özellik için Windows 11 24H2+ ve NPU gereklidir
    /// Şimdilik temel implementasyon, gelecekte Windows.AI.MachineLearning API eklenecek
    /// </summary>
    public class ImageDescriptionService : IImageDescriptionService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ITextRecognitionService _textRecognition;
        private bool _isAvailable = false;

        public ImageDescriptionService(
            DispatcherQueue dispatcherQueue,
            ITextRecognitionService textRecognition)
        {
            _dispatcherQueue = dispatcherQueue;
            _textRecognition = textRecognition;
        }

        public async Task<bool> IsAvailableAsync()
        {
            await Task.CompletedTask;

            try
            {
                var version = Environment.OSVersion.Version;
                // Windows 11 24H2+ (Build 26100+) gerekli
                _isAvailable = version.Major >= 10 && version.Build >= 26100;
                return _isAvailable;
            }
            catch
            {
                _isAvailable = false;
                return false;
            }
        }

        public async Task<string> DescribeImageAsync(string imagePath, string language = "tr-TR")
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Resim dosyası bulunamadı", imagePath);

            try
            {
                // Dosyayı aç
                var file = await StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var properties = await decoder.GetFrameAsync(0);

                // Temel görüntü bilgileri
                var width = properties.PixelWidth;
                var height = properties.PixelHeight;

                // OCR ile metin var mı kontrol et
                string ocrText = "";
                try
                {
                    ocrText = await _textRecognition.ExtractTextFromFileAsync(imagePath);
                }
                catch
                {
                    // OCR başarısız olursa devam et
                }

                // Temel açıklama oluştur (Florence API entegre edilene kadar)
                var description = $"Görüntü boyutu: {width}x{height} piksel";

                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    description += $"\n\nGörüntüde tespit edilen metin:\n{ocrText}";
                }
                else
                {
                    description += "\n\nGörüntüde metin tespit edilemedi.";
                }

                // TODO: Windows.AI.MachineLearning API ile Florence model entegrasyonu
                // Bu gelecekte LAF token ile birlikte eklenecek
                description += "\n\n[NOT: Detaylı görsel analiz için Windows AI Florence entegrasyonu bekleniyor]";

                return description;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image description hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DescribeBitmapAsync(SoftwareBitmap bitmap, string language = "tr-TR")
        {
            try
            {
                // Geçici dosyaya kaydet
                var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    $"temp_{Guid.NewGuid()}.png",
                    CreationCollisionOption.ReplaceExisting);

                using (var stream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetSoftwareBitmap(bitmap);
                    await encoder.FlushAsync();
                }

                // Dosyadan açıklama oluştur
                var description = await DescribeImageAsync(tempFile.Path, language);

                // Geçici dosyayı sil
                await tempFile.DeleteAsync();

                return description;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bitmap description hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DescribeScreenAsync(string language = "tr-TR")
        {
            // Ekran görüntüsü alma özelliği ayrı bir helper'da olacak
            await Task.CompletedTask;
            throw new NotImplementedException("Ekran görüntüsü alma özelliği henüz eklenmedi. ScreenCaptureHelper kullanılacak.");
        }

        public async Task<string[]> DetectObjectsAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Resim dosyası bulunamadı", imagePath);

            await Task.CompletedTask;

            // TODO: Windows.AI.MachineLearning API ile Florence object detection
            // Şimdilik placeholder
            return new string[]
            {
                "[Nesne tespiti için Windows AI Florence entegrasyonu bekleniyor]"
            };
        }

        public async Task<string?> DescribeClipboardImageAsync(string language = "tr-TR")
        {
            try
            {
                // Panodan bitmap al
                var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();

                if (!dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
                    return null;

                var bitmapRef = await dataPackageView.GetBitmapAsync();
                using var stream = await bitmapRef.OpenReadAsync();

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);

                return await DescribeBitmapAsync(bitmap, language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard image description hatası: {ex.Message}");
                return null;
            }
        }
    }
}
