using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Dispatching;

namespace QuadroAIPilot.Services.WindowsAI.Helpers
{
    /// <summary>
    /// Windows Graphics Capture API ile ekran görüntüsü alma
    /// Windows 10 1809+ destekler
    /// </summary>
    public class ScreenCaptureHelper
    {
        private readonly DispatcherQueue _dispatcherQueue;

        public ScreenCaptureHelper(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        /// <summary>
        /// Tüm ekranın görüntüsünü al
        /// </summary>
        public async Task<SoftwareBitmap?> CaptureScreenAsync()
        {
            try
            {
                // Win32 API ile ekran görüntüsü al
                return await CaptureScreenWithGdiAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screen capture hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ekranın belirli bir bölgesinin görüntüsünü al
        /// </summary>
        public async Task<SoftwareBitmap?> CaptureScreenRegionAsync(int x, int y, int width, int height)
        {
            try
            {
                return await CaptureScreenRegionWithGdiAsync(x, y, width, height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screen region capture hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ekran görüntüsünü dosyaya kaydet
        /// </summary>
        public async Task<bool> CaptureScreenToFileAsync(string outputPath)
        {
            try
            {
                var bitmap = await CaptureScreenAsync();
                if (bitmap == null) return false;

                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outputPath)!);
                var file = await folder.CreateFileAsync(
                    Path.GetFileName(outputPath),
                    CreationCollisionOption.ReplaceExisting);

                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);

                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screen capture to file hatası: {ex.Message}");
                return false;
            }
        }

        #region Win32 GDI+ Capture Implementation

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SRCCOPY = 0x00CC0020;

        private async Task<SoftwareBitmap?> CaptureScreenWithGdiAsync()
        {
            IntPtr desktopWindow = GetDesktopWindow();
            IntPtr desktopDC = GetWindowDC(desktopWindow);
            IntPtr memoryDC = CreateCompatibleDC(desktopDC);

            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);

            IntPtr bitmap = CreateCompatibleBitmap(desktopDC, width, height);
            IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

            BitBlt(memoryDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);

            SelectObject(memoryDC, oldBitmap);

            // Bitmap'i SoftwareBitmap'e çevir
            var softwareBitmap = await ConvertGdiBitmapToSoftwareBitmapAsync(bitmap, width, height);

            // Cleanup
            DeleteObject(bitmap);
            DeleteDC(memoryDC);
            ReleaseDC(desktopWindow, desktopDC);

            return softwareBitmap;
        }

        private async Task<SoftwareBitmap?> CaptureScreenRegionWithGdiAsync(int x, int y, int width, int height)
        {
            IntPtr desktopWindow = GetDesktopWindow();
            IntPtr desktopDC = GetWindowDC(desktopWindow);
            IntPtr memoryDC = CreateCompatibleDC(desktopDC);

            IntPtr bitmap = CreateCompatibleBitmap(desktopDC, width, height);
            IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

            BitBlt(memoryDC, 0, 0, width, height, desktopDC, x, y, SRCCOPY);

            SelectObject(memoryDC, oldBitmap);

            var softwareBitmap = await ConvertGdiBitmapToSoftwareBitmapAsync(bitmap, width, height);

            DeleteObject(bitmap);
            DeleteDC(memoryDC);
            ReleaseDC(desktopWindow, desktopDC);

            return softwareBitmap;
        }

        private async Task<SoftwareBitmap?> ConvertGdiBitmapToSoftwareBitmapAsync(IntPtr hBitmap, int width, int height)
        {
            try
            {
                // Geçici dosyaya kaydet
                var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    $"screen_{Guid.NewGuid()}.png",
                    CreationCollisionOption.ReplaceExisting);

                // System.Drawing kullanarak kaydet
                using (var bitmap = System.Drawing.Image.FromHbitmap(hBitmap))
                {
                    bitmap.Save(tempFile.Path, System.Drawing.Imaging.ImageFormat.Png);
                }

                // Windows.Graphics.Imaging ile yükle
                using var stream = await tempFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied);

                // Geçici dosyayı sil
                await tempFile.DeleteAsync();

                return softwareBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GDI Bitmap conversion hatası: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
