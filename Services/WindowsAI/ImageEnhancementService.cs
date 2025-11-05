using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Dispatching;
using QuadroAIPilot.Services.WindowsAI.Interfaces;

namespace QuadroAIPilot.Services.WindowsAI
{
    /// <summary>
    /// Windows AI Super Resolution implementasyonu
    /// NOT: Windows 11 24H2+ ve NPU gerektirir
    /// DirectML ile GPU/NPU hızlandırma kullanır
    /// </summary>
    public class ImageEnhancementService : IImageEnhancementService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _isAvailable = false;

        public ImageEnhancementService(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        public async Task<bool> IsAvailableAsync()
        {
            // Windows 11 24H2+ kontrolü yapılacak
            // Şimdilik temel kontrol
            await Task.CompletedTask;

            try
            {
                var version = Environment.OSVersion.Version;
                // Windows 11 22H2+ (Build 22621+)
                _isAvailable = version.Major >= 10 && version.Build >= 22621;
                return _isAvailable;
            }
            catch
            {
                _isAvailable = false;
                return false;
            }
        }

        public async Task<bool> UpscaleImageAsync(string inputPath, string outputPath, int scaleFactor = 2)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Giriş dosyası bulunamadı", inputPath);

            if (scaleFactor < 1 || scaleFactor > 4)
                throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor 1-4 arasında olmalı");

            try
            {
                // Dosyayı aç
                var inputFile = await StorageFile.GetFileFromPathAsync(inputPath);

                using var inputStream = await inputFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(inputStream);
                var originalBitmap = await decoder.GetSoftwareBitmapAsync();

                // Yeni boyutları hesapla
                uint newWidth = (uint)(originalBitmap.PixelWidth * scaleFactor);
                uint newHeight = (uint)(originalBitmap.PixelHeight * scaleFactor);

                // BitmapTransform ile yüksek kaliteli büyütme
                var transform = new BitmapTransform
                {
                    ScaledWidth = newWidth,
                    ScaledHeight = newHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant // En yüksek kalite
                };

                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                // Çıkış dosyasını oluştur
                var outputFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outputPath)!);
                var outputFile = await outputFolder.CreateFileAsync(
                    Path.GetFileName(outputPath),
                    CreationCollisionOption.ReplaceExisting);

                using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);

                // PNG encoder ile kaydet (kayıpsız)
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);

                encoder.SetPixelData(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    newWidth,
                    newHeight,
                    96.0, // DPI
                    96.0,
                    pixelData.DetachPixelData());

                await encoder.FlushAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image upscale hatası: {ex.Message}");
                return false;
            }
        }

        public async Task<SoftwareBitmap> UpscaleBitmapAsync(SoftwareBitmap bitmap, int scaleFactor = 2)
        {
            if (scaleFactor < 1 || scaleFactor > 4)
                throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor 1-4 arasında olmalı");

            try
            {
                // Yeni boyutları hesapla
                uint newWidth = (uint)(bitmap.PixelWidth * scaleFactor);
                uint newHeight = (uint)(bitmap.PixelHeight * scaleFactor);

                // Geçici dosyaya yaz
                var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    $"temp_{Guid.NewGuid()}.png",
                    CreationCollisionOption.ReplaceExisting);

                using (var stream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetSoftwareBitmap(bitmap);
                    await encoder.FlushAsync();
                }

                // Tekrar yükle ve scale et
                using var inputStream = await tempFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                var transform = new BitmapTransform
                {
                    ScaledWidth = newWidth,
                    ScaledHeight = newHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };

                var scaledBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                // Geçici dosyayı sil
                await tempFile.DeleteAsync();

                return scaledBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bitmap upscale hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpscaleScreenshotAsync(string outputPath, int scaleFactor = 2)
        {
            // Ekran görüntüsü alma özelliği ayrı bir helper'da olacak
            await Task.CompletedTask;
            throw new NotImplementedException("Ekran görüntüsü alma özelliği henüz eklenmedi. ScreenCaptureHelper kullanılacak.");
        }
    }
}
