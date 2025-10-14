using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuadroAIPilot.Services.Api
{
    /// <summary>
    /// Klavye API çağrıları için sınıf
    /// </summary>
    public class KeyboardApi
    {
        #region Windows API İmportları

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        #endregion

        #region Sabitler

        // Klavye olayları için sabitler
        private const int KEYEVENTF_EXTENDEDKEY = 0x01;
        private const int KEYEVENTF_KEYUP = 0x02;
        private const byte VK_ESCAPE = 0x1B;
        private const byte VK_CONTROL = 0x11;
        private const byte C_KEY = 0x43;
        private const byte V_KEY = 0x56;

        #endregion

        /// <summary>
        /// ESC tuşunu gönderir
        /// </summary>
        public void SendEscapeKey()
        {
            try
            {
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, 0);
                Debug.WriteLine("[KeyboardApi] ESC tuşu gönderildi");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardApi] SendEscapeKey hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Belirtilen tuş kodunu gönderir
        /// </summary>
        /// <param name="keyCode">Gönderilecek tuş kodu</param>
        public void SendKey(byte keyCode)
        {
            try
            {
                keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                Debug.WriteLine($"[KeyboardApi] Tuş gönderildi: {keyCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardApi] SendKey hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Tuş kombinasyonu gönderir (Ctrl+C, Alt+Tab vb.)
        /// </summary>
        /// <param name="modifierKey">Modifier tuş (Ctrl, Alt, Shift)</param>
        /// <param name="key">Ana tuş</param>
        public void SendKeyCombo(byte modifierKey, byte key)
        {
            try
            {
                // Modifier tuşu bas
                keybd_event(modifierKey, 0, KEYEVENTF_EXTENDEDKEY, 0);
                
                // Ana tuşu bas ve bırak
                keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(key, 0, KEYEVENTF_KEYUP, 0);
                
                // Modifier tuşu bırak
                keybd_event(modifierKey, 0, KEYEVENTF_KEYUP, 0);
                
                Debug.WriteLine($"[KeyboardApi] Tuş kombinasyonu gönderildi: {modifierKey}+{key}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardApi] SendKeyCombo hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Kopyala komutunu gönderir (Ctrl+C)
        /// </summary>
        public void Copy()
        {
            try
            {
                SendKeyCombo(VK_CONTROL, C_KEY);
                Debug.WriteLine("[KeyboardApi] Kopyala komutu gönderildi (Ctrl+C)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardApi] Copy hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Yapıştır komutunu gönderir (Ctrl+V)
        /// </summary>
        public void Paste()
        {
            try
            {
                SendKeyCombo(VK_CONTROL, V_KEY);
                Debug.WriteLine("[KeyboardApi] Yapıştır komutu gönderildi (Ctrl+V)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardApi] Paste hatası: {ex.Message}");
            }
        }
    }
}
