using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Dispatching;
using QuadroAIPilot.Services.WindowsAI.Interfaces;

namespace QuadroAIPilot.Services.WindowsAI
{
    /// <summary>
    /// Windows AI OCR implementasyonu
    /// Windows.Media.Ocr API kullanır
    /// </summary>
    public class TextRecognitionService : ITextRecognitionService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private OcrEngine? _ocrEngine;
        private bool _isInitialized = false;

        public TextRecognitionService(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        /// <summary>
        /// OCR engine'i başlat
        /// </summary>
        private async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Türkçe dil desteği ile OCR engine oluştur
                var language = new Windows.Globalization.Language("tr-TR");

                if (OcrEngine.IsLanguageSupported(language))
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
                }
                else
                {
                    // Türkçe desteklenmiyorsa varsayılan dili kullan
                    _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR Engine başlatılamadı: {ex.Message}");
                _isInitialized = false;
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            await InitializeAsync();
            return _ocrEngine != null;
        }

        public async Task<string> ExtractTextFromFileAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Resim dosyası bulunamadı", imagePath);

            await InitializeAsync();

            if (_ocrEngine == null)
                throw new InvalidOperationException("OCR Engine başlatılamadı");

            try
            {
                // Dosyayı aç
                var file = await StorageFile.GetFileFromPathAsync(imagePath);

                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);

                // OCR işlemi
                var result = await _ocrEngine.RecognizeAsync(bitmap);

                // Metni birleştir
                var textBuilder = new StringBuilder();
                foreach (var line in result.Lines)
                {
                    textBuilder.AppendLine(line.Text);
                }

                return textBuilder.ToString().Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<string> ExtractTextFromBitmapAsync(SoftwareBitmap bitmap)
        {
            await InitializeAsync();

            if (_ocrEngine == null)
                throw new InvalidOperationException("OCR Engine başlatılamadı");

            try
            {
                // Bitmap formatını kontrol et
                if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Rgba8 ||
                    bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
                }

                // OCR işlemi
                var result = await _ocrEngine.RecognizeAsync(bitmap);

                // Metni birleştir
                var textBuilder = new StringBuilder();
                foreach (var line in result.Lines)
                {
                    textBuilder.AppendLine(line.Text);
                }

                return textBuilder.ToString().Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<string> ExtractTextFromScreenAsync()
        {
            // Ekran görüntüsü alma özelliği ayrı bir helper'da olacak
            // Şimdilik NotImplementedException fırlat
            await Task.CompletedTask;
            throw new NotImplementedException("Ekran görüntüsü alma özelliği henüz eklenmedi. ScreenCaptureHelper kullanılacak.");
        }

        public async Task<string> ExtractTextFromScreenRegionAsync(int x, int y, int width, int height)
        {
            // Ekran görüntüsü alma özelliği ayrı bir helper'da olacak
            await Task.CompletedTask;
            throw new NotImplementedException("Ekran görüntüsü alma özelliği henüz eklenmedi. ScreenCaptureHelper kullanılacak.");
        }

        public async Task<string?> ExtractTextFromClipboardAsync()
        {
            await InitializeAsync();

            if (_ocrEngine == null)
                return null;

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

                return await ExtractTextFromBitmapAsync(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pano OCR hatası: {ex.Message}");
                return null;
            }
        }
    }
}
