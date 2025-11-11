using System;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace QuadroAIPilot.Dialogs
{
    /// <summary>
    /// Güncelleme indirme ilerleme dialog'u
    /// Real-time progress tracking ve hız hesaplama
    /// </summary>
    public sealed partial class UpdateProgressDialog : ContentDialog
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private DateTime _downloadStartTime;
        private long _totalBytes;
        private long _lastReportedBytes;
        private DateTime _lastReportTime;

        /// <summary>
        /// İptal token'ı - indirme işlemini iptal etmek için kullanılır
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        /// <summary>
        /// İndirme iptal edildi mi?
        /// </summary>
        public bool IsCancelled => _cancellationTokenSource.IsCancellationRequested;

        public UpdateProgressDialog(string fileName, long totalBytes)
        {
            this.InitializeComponent();

            _cancellationTokenSource = new CancellationTokenSource();
            _totalBytes = totalBytes;
            _downloadStartTime = DateTime.Now;
            _lastReportTime = DateTime.Now;
            _lastReportedBytes = 0;

            // UI başlangıç değerleri
            FileNameText.Text = fileName;
            TotalBytesText.Text = FormatBytes(totalBytes);
            DownloadProgressBar.Maximum = 100;
            DownloadProgressBar.Value = 0;
            PercentageText.Text = "0%";
            DownloadedBytesText.Text = "0 B";
            SpeedText.Text = "0 MB/s";
            RemainingTimeText.Text = "Hesaplanıyor...";

            // İptal butonu handler
            this.CloseButtonClick += (s, e) =>
            {
                Log.Warning("[UpdateProgressDialog] Kullanıcı indirmeyi iptal etti");
                _cancellationTokenSource.Cancel();
            };

            Log.Information("[UpdateProgressDialog] Progress dialog başlatıldı - Dosya: {FileName}, Boyut: {Size}",
                fileName, FormatBytes(totalBytes));
        }

        /// <summary>
        /// İndirme ilerlemesini güncelle
        /// </summary>
        /// <param name="downloadedBytes">İndirilen byte sayısı</param>
        public void UpdateProgress(long downloadedBytes)
        {
            try
            {
                // UI thread'de çalıştır
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Yüzde hesapla
                    var percentage = _totalBytes > 0
                        ? (int)((downloadedBytes * 100) / _totalBytes)
                        : 0;

                    // UI güncelle
                    DownloadProgressBar.Value = percentage;
                    PercentageText.Text = $"{percentage}%";
                    DownloadedBytesText.Text = FormatBytes(downloadedBytes);

                    // Hız ve kalan süre hesapla
                    CalculateSpeedAndRemainingTime(downloadedBytes);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateProgressDialog] Progress güncellenirken hata");
            }
        }

        /// <summary>
        /// İndirme hızı ve kalan süreyi hesapla
        /// </summary>
        private void CalculateSpeedAndRemainingTime(long downloadedBytes)
        {
            try
            {
                var now = DateTime.Now;
                var elapsed = (now - _downloadStartTime).TotalSeconds;

                if (elapsed > 0)
                {
                    // Genel ortalama hız (başlangıçtan beri)
                    var averageSpeedBytesPerSec = downloadedBytes / elapsed;

                    // Anlık hız (son güncelleme arası)
                    var timeSinceLastReport = (now - _lastReportTime).TotalSeconds;
                    var bytesSinceLastReport = downloadedBytes - _lastReportedBytes;
                    var instantSpeedBytesPerSec = timeSinceLastReport > 0
                        ? bytesSinceLastReport / timeSinceLastReport
                        : averageSpeedBytesPerSec;

                    // Hızı göster (anlık hız + ortalama hız karışımı)
                    var displaySpeed = (instantSpeedBytesPerSec * 0.7) + (averageSpeedBytesPerSec * 0.3);
                    SpeedText.Text = $"{FormatBytes((long)displaySpeed)}/s";

                    // Kalan süre hesapla (ortalama hız kullan - daha stabil)
                    var remainingBytes = _totalBytes - downloadedBytes;
                    if (averageSpeedBytesPerSec > 0)
                    {
                        var remainingSeconds = (int)(remainingBytes / averageSpeedBytesPerSec);
                        RemainingTimeText.Text = FormatTime(remainingSeconds);
                    }

                    // Son rapor bilgilerini güncelle
                    _lastReportTime = now;
                    _lastReportedBytes = downloadedBytes;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateProgressDialog] Hız/süre hesaplanırken hata");
            }
        }

        /// <summary>
        /// Byte'ları okunabilir formata çevir (B, KB, MB, GB)
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:F1} {sizes[order]}";
        }

        /// <summary>
        /// Saniyeyi okunabilir süre formatına çevir
        /// </summary>
        private string FormatTime(int seconds)
        {
            if (seconds < 0) return "Bilinmiyor";
            if (seconds < 10) return "Birkaç saniye";
            if (seconds < 60) return $"{seconds} saniye";
            if (seconds < 3600)
            {
                var minutes = seconds / 60;
                var secs = seconds % 60;
                return secs > 0 ? $"{minutes} dakika {secs} saniye" : $"{minutes} dakika";
            }

            var hours = seconds / 3600;
            var mins = (seconds % 3600) / 60;
            return mins > 0 ? $"{hours} saat {mins} dakika" : $"{hours} saat";
        }

        /// <summary>
        /// Dialog'u kapat (indirme tamamlandığında)
        /// </summary>
        public new void Hide()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    base.Hide();
                    Log.Information("[UpdateProgressDialog] Dialog kapatıldı");
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateProgressDialog] Dialog kapatılırken hata");
            }
        }
    }
}
