using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using QuadroAIPilot.Services.WindowsAI;
using QuadroAIPilot.Services.WindowsAI.Interfaces;
using QuadroAIPilot.Services.WindowsAI.Helpers;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Windows AI komutlarını işleyen handler
    /// OCR, Image Enhancement, Image Description
    /// </summary>
    public class AICommandHandler
    {
        private readonly ITextRecognitionService _textRecognition;
        private readonly IImageEnhancementService _imageEnhancement;
        private readonly IImageDescriptionService _imageDescription;
        private readonly ScreenCaptureHelper _screenCapture;
        private readonly ILogger<AICommandHandler> _logger;

        public AICommandHandler(
            DispatcherQueue dispatcherQueue,
            ILogger<AICommandHandler> logger)
        {
            _logger = logger;

            // Services'leri oluştur
            _textRecognition = new TextRecognitionService(dispatcherQueue);
            _imageEnhancement = new ImageEnhancementService(dispatcherQueue);
            _imageDescription = new ImageDescriptionService(dispatcherQueue, _textRecognition);
            _screenCapture = new ScreenCaptureHelper(dispatcherQueue);
        }

        /// <summary>
        /// AI komutunu işle
        /// </summary>
        public async Task<(bool handled, string? result)> HandleAICommandAsync(string command)
        {
            command = command.ToLowerInvariant().Trim();

            _logger.LogInformation($"[AICommandHandler] Komut alındı: '{command}'");

            // OCR komutları - daha esnek pattern
            if ((command.Contains("ekran") && command.Contains("metin") && command.Contains("oku")) ||
                command.Contains("ekran oku") ||
                command.Contains("ocr"))
            {
                _logger.LogInformation("[AICommandHandler] OCR komutu eşleşti");
                return await HandleOCRCommandAsync();
            }

            // Panodan OCR - daha esnek
            if ((command.Contains("pano") || command.Contains("panodaki") || command.Contains("clipboard")) &&
                (command.Contains("oku") || command.Contains("görsel")))
            {
                _logger.LogInformation("[AICommandHandler] Pano OCR komutu eşleşti");
                return await HandleClipboardOCRAsync();
            }

            // Görsel açıklama - ÇOK ESNEK PATTERN
            if ((command.Contains("ekran") || command.Contains("görsel") || command.Contains("görüntü")) &&
                command.Contains("açıkla"))
            {
                _logger.LogInformation("[AICommandHandler] Görsel açıklama komutu eşleşti");
                return await HandleImageDescriptionAsync();
            }

            // Görsel büyütme - esnek pattern
            if ((command.Contains("ekran") || command.Contains("görüntü") || command.Contains("görsel")) &&
                (command.Contains("büyüt") || command.Contains("çözünürlük")))
            {
                _logger.LogInformation("[AICommandHandler] Görsel büyütme komutu eşleşti");
                return await HandleImageEnhancementAsync();
            }

            // Ekran görüntüsü kaydet - esnek pattern
            if (command.Contains("ekran") &&
                (command.Contains("görüntüsü") || command.Contains("screenshot")) &&
                command.Contains("kaydet"))
            {
                _logger.LogInformation("[AICommandHandler] Screenshot komutu eşleşti");
                return await HandleScreenshotAsync();
            }

            _logger.LogInformation("[AICommandHandler] Hiçbir AI komutu eşleşmedi");
            return (false, null);
        }

        /// <summary>
        /// Ekrandan metin okuma (OCR)
        /// </summary>
        private async Task<(bool, string)> HandleOCRCommandAsync()
        {
            try
            {
                _logger.LogInformation("OCR komutu işleniyor...");

                if (!await _textRecognition.IsAvailableAsync())
                {
                    return (true, "OCR servisi kullanılamıyor. Windows sürümünüzü kontrol edin.");
                }

                // Ekran görüntüsü al
                var bitmap = await _screenCapture.CaptureScreenAsync();
                if (bitmap == null)
                {
                    return (true, "Ekran görüntüsü alınamadı.");
                }

                // OCR işlemi
                var text = await _textRecognition.ExtractTextFromBitmapAsync(bitmap);

                if (string.IsNullOrWhiteSpace(text))
                {
                    return (true, "Ekranda metin bulunamadı.");
                }

                _logger.LogInformation($"OCR başarılı: {text.Length} karakter");
                return (true, $"Ekrandan okunan metin:\n\n{text}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR komutu hatası");
                return (true, $"OCR hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Panodan görsel OCR
        /// </summary>
        private async Task<(bool, string)> HandleClipboardOCRAsync()
        {
            try
            {
                _logger.LogInformation("Pano OCR komutu işleniyor...");

                var text = await _textRecognition.ExtractTextFromClipboardAsync();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return (true, "Panoda görsel bulunamadı veya metin tespit edilemedi.");
                }

                _logger.LogInformation($"Pano OCR başarılı: {text.Length} karakter");
                return (true, $"Panodan okunan metin:\n\n{text}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pano OCR hatası");
                return (true, $"Pano OCR hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Ekran görüntüsü açıklama
        /// </summary>
        private async Task<(bool, string)> HandleImageDescriptionAsync()
        {
            try
            {
                _logger.LogInformation("Görsel açıklama komutu işleniyor...");

                if (!await _imageDescription.IsAvailableAsync())
                {
                    // Windows 11 24H2+ gerekli, ama temel özellikler çalışır
                    _logger.LogWarning("Florence AI desteklenmiyor, temel analiz kullanılıyor");
                }

                // Geçici ekran görüntüsü dosyası oluştur
                var tempPath = Path.Combine(Path.GetTempPath(), $"screenshot_{Guid.NewGuid()}.png");

                if (!await _screenCapture.CaptureScreenToFileAsync(tempPath))
                {
                    return (true, "Ekran görüntüsü alınamadı.");
                }

                // Açıklama oluştur
                var description = await _imageDescription.DescribeImageAsync(tempPath, "tr-TR");

                // Geçici dosyayı sil
                try { File.Delete(tempPath); } catch { }

                _logger.LogInformation("Görsel açıklama başarılı");
                return (true, $"Ekran analizi:\n\n{description}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görsel açıklama hatası");
                return (true, $"Görsel açıklama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Görüntü büyütme (Super Resolution)
        /// </summary>
        private async Task<(bool, string)> HandleImageEnhancementAsync()
        {
            try
            {
                _logger.LogInformation("Görüntü büyütme komutu işleniyor...");

                if (!await _imageEnhancement.IsAvailableAsync())
                {
                    return (true, "Görüntü büyütme servisi kullanılamıyor. Windows 11 22H2+ gerekli.");
                }

                // Desktop klasörüne kaydet
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var outputPath = Path.Combine(desktopPath, $"enhanced_screenshot_{timestamp}.png");

                if (!await _imageEnhancement.UpscaleScreenshotAsync(outputPath, scaleFactor: 2))
                {
                    return (true, "Görüntü büyütme işlemi başarısız oldu.");
                }

                _logger.LogInformation($"Görüntü büyütme başarılı: {outputPath}");
                return (true, $"Ekran görüntüsü 2x büyütülerek masaüstüne kaydedildi:\n{outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görüntü büyütme hatası");
                return (true, $"Görüntü büyütme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Ekran görüntüsü kaydetme
        /// </summary>
        private async Task<(bool, string)> HandleScreenshotAsync()
        {
            try
            {
                _logger.LogInformation("Ekran görüntüsü kaydetme komutu işleniyor...");

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var outputPath = Path.Combine(desktopPath, $"screenshot_{timestamp}.png");

                if (!await _screenCapture.CaptureScreenToFileAsync(outputPath))
                {
                    return (true, "Ekran görüntüsü alınamadı.");
                }

                _logger.LogInformation($"Ekran görüntüsü kaydedildi: {outputPath}");
                return (true, $"Ekran görüntüsü masaüstüne kaydedildi:\n{outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ekran görüntüsü kaydetme hatası");
                return (true, $"Ekran görüntüsü hatası: {ex.Message}");
            }
        }
    }
}
