using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuadroAIPilot.Services.Api
{
    /// <summary>
    /// Windows pencere işlemleri için API sınıfı
    /// </summary>
    public class WindowApi
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool CloseWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Constants

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_MAXIMIZE = 3;
        private const int MAX_TITLE_LENGTH = 255;

        private const uint WM_CLOSE = 0x0010;
        private const uint WM_QUIT = 0x0012;

        #endregion

        #region Public Methods

        /// <summary>
        /// Aktif pencere handle'ını döndürür
        /// </summary>
        public IntPtr GetActiveWindow()
        {
            return GetForegroundWindow();
        }

        /// <summary>
        /// Pencere başlığını döndürür
        /// </summary>
        public string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                int length = GetWindowTextLength(hWnd);
                if (length == 0) return string.Empty;

                StringBuilder title = new StringBuilder(length + 1);
                GetWindowText(hWnd, title, title.Capacity);
                return title.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere başlığı alınırken hata: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Process adına göre pencere bulur
        /// </summary>
        public IntPtr FindWindowByProcessName(string processName)
        {
            IntPtr foundWindow = IntPtr.Zero;

            try
            {
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd))
                        return true;

                    uint processId;
                    GetWindowThreadProcessId(hWnd, out processId);

                    try
                    {
                        using (Process process = Process.GetProcessById((int)processId))
                        {
                            if (process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                foundWindow = hWnd;
                                return false; // Dur
                            }
                        }
                    }
                    catch
                    {
                        // Process erişim hatası durumunda devam et
                    }

                    return true; // Devam et
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere arama hatası: {ex.Message}");
            }

            return foundWindow;
        }

        /// <summary>
        /// Pencereyi öne getirir
        /// </summary>
        public bool BringToFront(IntPtr hWnd)
        {
            try
            {
                if (!IsWindowVisible(hWnd))
                {
                    ShowWindow(hWnd, SW_SHOW);
                }
                return SetForegroundWindow(hWnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere öne getirme hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pencereyi maksimize eder
        /// </summary>
        public bool MaximizeWindow(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_MAXIMIZE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere maksimize hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pencereyi restore eder
        /// </summary>
        public bool RestoreWindow(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_RESTORE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere restore hatası: {ex.Message}");
                return false;
            }
        }        /// <summary>
        /// Pencereyi kapatır (WM_CLOSE mesajı gönderir)
        /// </summary>
        public bool CloseWindowMessage(IntPtr hWnd)
        {
            try
            {
                return PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere kapatma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pencere process ID'sini döndürür
        /// </summary>
        public uint GetWindowProcessId(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                return processId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Process ID alma hatası: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// İstemci koordinatlarını ekran koordinatlarına dönüştürür
        /// </summary>
        public bool ClientToScreenCoord(IntPtr hWnd, ref POINT lpPoint)
        {
            try
            {
                return ClientToScreen(hWnd, ref lpPoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] ClientToScreen hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// İstemci alanı koordinatlarını döndürür
        /// </summary>
        public RECT GetClientRect(IntPtr hWnd)
        {
            RECT rect = new RECT();
            try
            {
                GetClientRect(hWnd, out rect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] GetClientRect hatası: {ex.Message}");
            }
            return rect;
        }

        /// <summary>
        /// Pencereye odak verir
        /// </summary>
        public IntPtr SetWindowFocus(IntPtr hWnd)
        {
            try
            {
                return SetFocus(hWnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] SetFocus hatası: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Thread'leri birbirine bağlar
        /// </summary>
        public bool AttachThreads(uint sourceThreadId, uint targetThreadId, bool attach)
        {
            try
            {
                return AttachThreadInput(sourceThreadId, targetThreadId, attach);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] AttachThreadInput hatası: {ex.Message}");
                return false;
            }
        }        /// <summary>
        /// Geçerli thread ID'sini döndürür
        /// </summary>
        public uint GetCurrentThreadIdentifier()
        {
            try
            {
                return GetCurrentThreadId();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] GetCurrentThreadId hatası: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Pencereyi en üste getirir
        /// </summary>
        public bool BringToTop(IntPtr hWnd)
        {
            try
            {
                return BringWindowToTop(hWnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] BringWindowToTop hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pencere görünür mü kontrol eder
        /// </summary>
        public bool IsVisible(IntPtr hWnd)
        {
            try
            {
                return IsWindowVisible(hWnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] IsWindowVisible hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pencere bulma işlemi yapar
        /// </summary>
        public IntPtr FindWindow(string processName, string windowTitle)
        {
            IntPtr foundWindow = IntPtr.Zero;

            try
            {
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd))
                        return true;

                    // Process ID'sini al ve kontrol et
                    uint processId = GetWindowProcessId(hWnd);
                    if (processId != 0 && !string.IsNullOrEmpty(processName))
                    {
                        try
                        {
                            using (var process = System.Diagnostics.Process.GetProcessById((int)processId))
                            {
                                if (!process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                        catch
                        {
                            return true;
                        }
                    }

                    // Pencere başlığını kontrol et
                    if (!string.IsNullOrEmpty(windowTitle))
                    {
                        string title = GetWindowTitle(hWnd);
                        if (!title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    foundWindow = hWnd;
                    return false;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere arama hatası: {ex.Message}");
            }

            return foundWindow;
        }

        /// <summary>
        /// Kısmi başlık eşleşmesine göre pencere arar
        /// </summary>
        public IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            IntPtr result = IntPtr.Zero;
            
            try
            {
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd))
                        return true;
                        
                    string title = GetWindowTitle(hWnd);
                    if (!string.IsNullOrEmpty(title) && title.Contains(partialTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        result = hWnd;
                        return false;
                    }
                    
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Kısmi başlık arama hatası: {ex.Message}");
            }
            
            return result;
        }        /// <summary>
        /// Pencereyi gösterir
        /// </summary>
        public bool ShowWindowNormal(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_SHOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] Pencere gösterme hatası: {ex.Message}");
                return false;
            }
        }        /// <summary>
        /// Pencere koordinatlarını döndürür
        /// </summary>
        public RECT GetWindowRectangle(IntPtr hWnd)
        {
            RECT rect = new RECT();
            try
            {
                GetWindowRect(hWnd, out rect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowApi] GetWindowRect hatası: {ex.Message}");
            }
            return rect;
        }

        #endregion
    }
}
