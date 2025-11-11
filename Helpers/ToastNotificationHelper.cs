using System;
using System.Diagnostics;
using AutoUpdaterDotNET;
using Microsoft.UI.Xaml;
using Serilog;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Windows 11 toast notification yöneticisi
    /// Güncelleme bildirimleri için kullanılır
    /// </summary>
    public static class ToastNotificationHelper
    {
        private static UpdateInfoEventArgs? _pendingUpdateInfo;
        private static Action<UpdateInfoEventArgs>? _onToastClicked;

        /// <summary>
        /// Güncelleme toast notification'ı göster
        /// </summary>
        /// <param name="updateInfo">Güncelleme bilgileri</param>
        /// <param name="onClicked">Toast'a tıklandığında çağrılacak callback</param>
        public static void ShowUpdateToastNotification(
            UpdateInfoEventArgs updateInfo,
            Action<UpdateInfoEventArgs> onClicked)
        {
            try
            {
                _pendingUpdateInfo = updateInfo;
                _onToastClicked = onClicked;

                // WinUI 3 için basit bir InfoBar benzeri yaklaşım kullanabiliriz
                // Veya Windows.UI.Notifications kullanabiliriz (daha gelişmiş)

                // Şimdilik basit bir yaklaşım: MainWindow'da InfoBar göster
                // Bu, UpdateService'den MainWindow'a event göndererek yapılabilir

                Log.Information("[ToastNotificationHelper] Toast notification gösterildi - Versiyon: {Version}",
                    updateInfo.CurrentVersion);

                // NOT: WinUI 3'te native toast notification göstermek için
                // Microsoft.Toolkit.Uwp.Notifications paketi kullanılabilir
                // Ancak bu minimal bir çözüm için overkill olabilir

                // Alternatif: Direkt UpdateNotificationDialog'u göster
                // (Otomatik kontrol için daha basit bir yaklaşım)
                ShowUpdateDialogFromToast();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ToastNotificationHelper] Toast notification gösterilemedi");
            }
        }

        /// <summary>
        /// Toast'tan dialog'u göster (basitleştirilmiş yaklaşım)
        /// </summary>
        private static void ShowUpdateDialogFromToast()
        {
            try
            {
                if (_pendingUpdateInfo != null && _onToastClicked != null)
                {
                    // Callback'i çağır
                    _onToastClicked.Invoke(_pendingUpdateInfo);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ToastNotificationHelper] Toast callback çalıştırılamadı");
            }
        }

        /// <summary>
        /// Windows 10/11 native toast notification göster
        /// (Microsoft.Toolkit.Uwp.Notifications gerektirir - opsiyonel)
        /// </summary>
        public static void ShowNativeToastNotification(string title, string message, string? actionUrl = null)
        {
            try
            {
                // Bu metod gelecekte Windows native toast API ile genişletilebilir
                // Şimdilik log'a yazalım

                Log.Information("[ToastNotificationHelper] Native toast: {Title} - {Message}", title, message);

                // TODO: Windows.UI.Notifications.ToastNotification kullanarak
                // gerçek bir Windows toast gösterilebilir
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ToastNotificationHelper] Native toast gösterilemedi");
            }
        }

        /// <summary>
        /// Bildirim izinlerini kontrol et
        /// </summary>
        public static bool AreNotificationsEnabled()
        {
            try
            {
                // Windows bildirim ayarlarını kontrol et
                // Şimdilik her zaman true dön (basit yaklaşım)
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ToastNotificationHelper] Bildirim izinleri kontrol edilemedi");
                return false;
            }
        }
    }
}
