using System;
using System.Diagnostics;
using AutoUpdaterDotNET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace QuadroAIPilot.Dialogs
{
    /// <summary>
    /// Güncelleme bildirimi dialog'u
    /// Modern, bilgilendirici ve kullanıcı dostu
    /// </summary>
    public sealed partial class UpdateNotificationDialog : ContentDialog
    {
        private readonly UpdateInfoEventArgs _updateInfo;

        public UpdateNotificationDialog(UpdateInfoEventArgs updateInfo)
        {
            this.InitializeComponent();
            _updateInfo = updateInfo;

            LoadUpdateInformation();
        }

        /// <summary>
        /// Güncelleme bilgilerini UI'ya yükle
        /// </summary>
        private void LoadUpdateInformation()
        {
            try
            {
                // Yeni versiyon
                NewVersionText.Text = _updateInfo.CurrentVersion?.ToString() ?? "Bilinmiyor";

                // Mevcut versiyon
                CurrentVersionText.Text = _updateInfo.InstalledVersion?.ToString() ?? "Bilinmiyor";

                // Dosya boyutu (UpdateInfoEventArgs'da FileSize yok, "Bilinmiyor" göster)
                FileSizeText.Text = "Bilinmiyor";

                // Release tarihi (update.xml'de belirtilmişse)
                // Şimdilik bugünün tarihini göster
                ReleaseDateText.Text = DateTime.Now.ToString("dd MMMM yyyy");

                // Release notes
                LoadReleaseNotes();

                Log.Information("[UpdateNotificationDialog] Dialog yüklendi - Yeni versiyon: {Version}", _updateInfo.CurrentVersion);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateNotificationDialog] Güncelleme bilgileri yüklenirken hata");
            }
        }

        /// <summary>
        /// Release notes'u yükle ve göster
        /// </summary>
        private void LoadReleaseNotes()
        {
            try
            {
                // update.xml'de changelog varsa, buradan basit bir özet göster
                // Gerçek changelog tarayıcıda açılacak

                // Varsayılan mesaj
                ReleaseNotesText.Text =
                    "• Performans iyileştirmeleri\n" +
                    "• Hata düzeltmeleri\n" +
                    "• Güvenlik güncellemeleri\n" +
                    "• Kullanıcı deneyimi geliştirmeleri";

                // Eğer changelog URL varsa butonu göster
                if (!string.IsNullOrEmpty(_updateInfo.ChangelogURL))
                {
                    ReleaseNotesButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ReleaseNotesButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateNotificationDialog] Release notes yüklenirken hata");
                ReleaseNotesText.Text = "• Detaylar yüklenemedi";
            }
        }

        /// <summary>
        /// "Tüm değişiklikleri görüntüle" butonu tıklandığında
        /// </summary>
        private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_updateInfo.ChangelogURL))
                {
                    // Changelog URL'ini tarayıcıda aç
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _updateInfo.ChangelogURL,
                        UseShellExecute = true
                    });

                    Log.Information("[UpdateNotificationDialog] Changelog URL açıldı: {URL}", _updateInfo.ChangelogURL);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UpdateNotificationDialog] Changelog URL açılamadı");
            }
        }

        /// <summary>
        /// Byte'ları okunabilir formata çevir (KB, MB, GB)
        /// </summary>
        private string FormatBytes(long bytes)
        {
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
    }
}
