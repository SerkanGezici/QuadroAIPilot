using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuadroAIPilot.Services.Api
{    /// <summary>
    /// Fare işlemleri için API sınıfı
    /// </summary>
    public class MouseApi
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        #endregion

        #region Constants

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;

        #endregion

        #region Public Methods

        /// <summary>
        /// Fare konumunu belirtilen koordinatlara ayarlar
        /// </summary>
        public bool SetCursorPosition(int x, int y)
        {
            try
            {
                return SetCursorPos(x, y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MouseApi] Cursor pozisyon hatası: {ex.Message}");
                return false;
            }
        }        // PerformClick metodu kaldırıldı ve Click ile birleştirildi

        /// <summary>
        /// Fare tıklaması gerçekleştirir (yeni metod)
        /// </summary>
        public void Click(int x, int y, bool rightClick = false)
        {
            try
            {
                // Fare konumunu ayarla
                SetCursorPos(x, y);

                // Tıklama gerçekleştir
                if (rightClick)
                {
                    // Sağ tıklama
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    System.Threading.Thread.Sleep(10);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                }
                else
                {
                    // Sol tıklama
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    System.Threading.Thread.Sleep(10);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }

                Debug.WriteLine($"[MouseApi] Fare tıklaması gerçekleştirildi: X={x}, Y={y}, Sağ Tıklama={rightClick}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MouseApi] Fare tıklama hatası: {ex.Message}");
            }
        }        /// <summary>
        /// Belirtilen koordinatlarda mouse tıklaması yapar
        /// </summary>
        public void ClickAt(int x, int y)
        {
            try
            {
                SetCursorPos(x, y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                Debug.WriteLine($"[MouseApi] Tıklama: X={x}, Y={y}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MouseApi] Tıklama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Çift tıklama gerçekleştirir
        /// </summary>
        public void PerformDoubleClick(int x, int y)
        {
            try
            {
                Click(x, y);
                System.Threading.Thread.Sleep(50);
                Click(x, y);
                Debug.WriteLine($"[MouseApi] Çift tıklama: X={x}, Y={y}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MouseApi] Çift tıklama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Fare olayı gönderir
        /// </summary>
        public void SendMouseEvent(int flags, int dx = 0, int dy = 0, int data = 0)
        {
            try
            {
                mouse_event(flags, dx, dy, data, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MouseApi] Mouse event hatası: {ex.Message}");
            }
        }

        #endregion
    }
}
