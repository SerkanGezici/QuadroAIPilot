using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Windows ses seviyesini yüzde olarak kontrol eden servis
    /// "sesi yüzde 50 yap", "ses seviyesini yüzde 30 yap" gibi komutlar için
    /// </summary>
    public static class VolumeController
    {
        // Windows Core Audio API için COM interop
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Ses seviyesini belirli bir yüzdeye ayarlar
        /// </summary>
        /// <param name="targetPercentage">Hedef ses seviyesi (0-100 arası)</param>
        public static async Task<bool> SetVolumePercentageAsync(int targetPercentage)
        {
            try
            {
                // Yüzdeyi 0-100 arasında sınırla
                targetPercentage = Math.Clamp(targetPercentage, 0, 100);

                Debug.WriteLine($"[VolumeController] Ses seviyesi {targetPercentage}% olarak ayarlanıyor...");

                // Önce sesi tamamen kapat (50 adım Volume Down)
                for (int i = 0; i < 50; i++)
                {
                    PressVolumeDown();
                    await Task.Delay(10); // Her adım arası 10ms bekle
                }

                Debug.WriteLine("[VolumeController] Ses seviyesi 0'a çekildi");

                // Şimdi hedef yüzdeye çık (her adım ~2%)
                int steps = targetPercentage / 2;
                for (int i = 0; i < steps; i++)
                {
                    PressVolumeUp();
                    await Task.Delay(10);
                }

                Debug.WriteLine($"[VolumeController] Ses seviyesi {targetPercentage}% olarak ayarlandı");

                await TextToSpeechService.SpeakTextAsync($"Ses seviyesi yüzde {targetPercentage} olarak ayarlandı");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeController] Ses ayarlama hatası: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Ses seviyesi ayarlanamadı");
                return false;
            }
        }

        /// <summary>
        /// Ses arttır tuşuna basar
        /// </summary>
        private static void PressVolumeUp()
        {
            keybd_event(VK_VOLUME_UP, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Ses azalt tuşuna basar
        /// </summary>
        private static void PressVolumeDown()
        {
            keybd_event(VK_VOLUME_DOWN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Sesi kapat/aç (mute/unmute)
        /// </summary>
        public static void ToggleMute()
        {
            keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Ses seviyesini artırır (varsayılan adım)
        /// </summary>
        public static void IncreaseVolume()
        {
            PressVolumeUp();
        }

        /// <summary>
        /// Ses seviyesini azaltır (varsayılan adım)
        /// </summary>
        public static void DecreaseVolume()
        {
            PressVolumeDown();
        }

        /// <summary>
        /// Ses seviyesini belirli sayıda adım artırır
        /// </summary>
        public static async Task IncreaseVolumeByStepsAsync(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                PressVolumeUp();
                await Task.Delay(50); // Sistem yanıt vermesi için bekle
            }
        }

        /// <summary>
        /// Ses seviyesini belirli sayıda adım azaltır
        /// </summary>
        public static async Task DecreaseVolumeByStepsAsync(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                PressVolumeDown();
                await Task.Delay(50);
            }
        }
    }
}
