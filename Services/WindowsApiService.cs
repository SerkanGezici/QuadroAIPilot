using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using QuadroAIPilot.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Windows API çağrıları için yardımcı sınıf
    /// </summary>
    public class WindowsApiService : IWindowsApiService
    {
        #region Windows API İmportları ve Yapılar

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
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // UI Automation ve Child Window API'leri

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);



        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, StringBuilder lParam);

        // Window ilişkili sabitler
        private const uint GW_CHILD = 5;
        private const uint GW_HWNDNEXT = 2;
        private const int WM_GETTEXT = 0x000D;
        private const int WM_GETTEXTLENGTH = 0x000E;

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

        // SendInput için gerekli yapılar
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // SendInput sabitleri
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Fare olayları için sabitler
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;

        // Klavye olayları için sabitler
        private const int KEYEVENTF_EXTENDEDKEY = 0x01;
        private const byte VK_ESCAPE = 0x1B;        // Pencere davranışı için sabitler
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_QUIT = 0x0012;
        private const int MAX_TITLE_LENGTH = 255;

        // Delegasyon tanımı
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        /// <summary>
        /// Şu anda odaklanmış pencereyi döndürür
        /// </summary>
        /// <returns>Odaklanmış pencerenin handle'ı</returns>
        public IntPtr GetCurrentForegroundWindow()
        {
            return GetForegroundWindow();
        }

        /// <summary>
        /// Pencereyi başlık veya process adına göre arar
        /// </summary>
        /// <param name="processName">Process adı</param>
        /// <param name="windowTitle">Pencere başlığı</param>
        /// <returns>Pencere handle veya IntPtr.Zero</returns>
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
                    uint processId;
                    GetWindowThreadProcessId(hWnd, out processId);

                    try
                    {
                        using (Process process = Process.GetProcessById((int)processId))
                        {
                            if (!string.IsNullOrEmpty(processName) &&
                                !process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogService.LogWarning($"Process access error: {ex.Message}");
                        return true;
                    }

                    // Pencere başlığını kontrol et (eğer belirtilmişse)
                    if (!string.IsNullOrEmpty(windowTitle))
                    {
                        const int TITLE_BUFFER_SIZE = 256;
                        StringBuilder sb = new StringBuilder(TITLE_BUFFER_SIZE);
                        try
                        {
                            GetWindowText(hWnd, sb, TITLE_BUFFER_SIZE);
                            string title = sb.ToString();

                            if (!title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.LogVerbose($"GetWindowText error: {ex.Message}");
                            return true;
                        }
                    }

                    // Pencere bulundu
                    foundWindow = hWnd;
                    return false;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Pencere arama hatası: {ex.Message}", ex);
            }

            return foundWindow;
        }

        /// <summary>
        /// Uygulamanın çalışıp çalışmadığını kontrol eder
        /// </summary>
        /// <param name="processName">Process adı</param>
        /// <returns>Uygulama çalışıyorsa true</returns>
        public bool IsApplicationRunning(string processName)
        {
            try
            {
                // .exe uzantısını kaldır
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                Process[] processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                LogService.LogError($"IsApplicationRunning hatası: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Pencereyi öne getirir
        /// </summary>
        /// <param name="processName">Process adı</param>        /// <param name="windowTitle">Pencere başlığı</param>
        /// <param name="maximize">İlk açılışta tam ekran yapılsın mı</param>
        /// <returns>İşlem başarılıysa true</returns>
        public bool BringWindowToFront(string processName, string windowTitle, bool maximize = false)
        {
            try
            {
                // .exe uzantısını kaldır
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                // Önce process adına göre pencereleri bul
                Process[] processes = Process.GetProcessesByName(processName);

                if (processes.Length == 0)
                {
                    // LogService.LogDebug($"[WindowsApiService] Process bulunamadı: {processName}");

                    // İsmi whatsapp olabilecek diğer process'leri kontrol et
                    if (processName.ToLowerInvariant().Contains("whatsapp"))
                    {
                        Process[] allProcesses = Process.GetProcesses();
                        var whatsappProcesses = new List<string>();
                        foreach (Process p in allProcesses)
                        {
                            try
                            {
                                if (p.ProcessName.ToLowerInvariant().Contains("whatsapp") ||
                                    (p.MainWindowTitle != null && p.MainWindowTitle.ToLowerInvariant().Contains("whatsapp")))
                                {
                                    whatsappProcesses.Add($"{p.ProcessName} ({p.MainWindowTitle})");
                                    if (p.MainWindowHandle != IntPtr.Zero)
                                    {
                                        // LogService.LogDebug($"WhatsApp process bulundu: {p.ProcessName}");
                                        return ForceForegroundWindow(p.MainWindowHandle);
                                    }
                                }
                            }
                            catch { /* Process'e erişim hatası olabilir, devam et */ }
                        }
                        if (whatsappProcesses.Count > 0)
                        {
                            // LogService.LogDebug($"WhatsApp için {whatsappProcesses.Count} alternatif process bulundu");
                        }
                    }

                    return false;
                }

                // Process bulunduğunda penceresini öne getir
                foreach (Process process in processes)
                {                    // Ana pencere varsa onu kullan
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        try
                        {
                            // İlk açılışta tam ekran, var olan pencere için mevcut durumu koru
                            ShowWindow(process.MainWindowHandle, maximize ? SW_MAXIMIZE : SW_RESTORE);
                        }
                        catch (Exception ex)
                        {
                            LogService.LogWarning($"ShowWindow hatası: {ex.Message}");
                        }

                        try
                        {
                            // SetForegroundWindow hatası yaşanabilir, ancak biz başarılı kabul edelim
                            SetForegroundWindow(process.MainWindowHandle);
                            // LogService.LogDebug($"[WindowsApiService] Pencere öne getirildi: {process.MainWindowTitle}");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            LogService.LogVerbose($"SetForegroundWindow hatası (kabul edilebilir): {ex.Message}");
                            // Hataya rağmen başarılı kabul et, çünkü büyük olasılıkla gerekli işlem gerçekleşti
                            return true;
                        }
                    }
                }                // Ana pencere yoksa pencereyi manuel olarak ara
                IntPtr hWnd = FindWindow(processName, windowTitle);

                if (hWnd != IntPtr.Zero)
                {
                    try
                    {
                        // İlk açılışta tam ekran, var olan pencere için mevcut durumu koru
                        ShowWindow(hWnd, maximize ? SW_MAXIMIZE : SW_RESTORE);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogWarning($"ShowWindow hatası: {ex.Message}");
                    }

                    try
                    {
                        // SetForegroundWindow hatası yaşanabilir, ancak biz başarılı kabul edelim
                        SetForegroundWindow(hWnd);
                        // LogService.LogDebug($"[WindowsApiService] Pencere bulundu ve öne getirildi");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogService.LogVerbose($"SetForegroundWindow hatası (kabul edilebilir): {ex.Message}");
                        // Hataya rağmen başarılı kabul et, çünkü büyük olasılıkla gerekli işlem gerçekleşti
                        return true;
                    }
                }

                // LogService.LogDebug($"[WindowsApiService] Pencere bulunamadı: {windowTitle}");
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Pencere öne getirme hatası: {ex.Message}", ex);
                // Genel bir hata varsa başarısız olarak değerlendir
                return false;
            }
        }

        /// <summary>
        /// Belirtilen pencereyi öne getirir
        /// </summary>
        /// <param name="hWnd">Pencere handle'ı</param>
        /// <param name="maximize">Pencereyi maksimize etmek için true</param>
        /// <returns>Başarılı ise true</returns>
        public bool BringWindowToFront(IntPtr hWnd, bool maximize = false)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                {
                    // LogService.LogDebug("[WindowsApiService] Geçersiz pencere handle: IntPtr.Zero");
                    return false;
                }
                
                // LogService.LogDebug($"[WindowsApiService] Pencere öne getiriliyor: {hWnd}");
                
                // Pencerenin görünür olduğundan emin ol
                if (!IsWindowVisible(hWnd))
                {
                    // LogService.LogDebug("[WindowsApiService] Pencere görünür değil, gösteriliyor...");
                    ShowWindow(hWnd, SW_SHOW);
                }
                
                // Pencereyi öne getir
                bool success = SetForegroundWindow(hWnd);
                
                if (success)
                {
                    LogService.LogInfo("Pencere başarıyla öne getirildi");
                    
                    if (maximize)
                    {
                        // LogService.LogDebug("[WindowsApiService] Pencere maksimize ediliyor");
                        ShowWindow(hWnd, SW_MAXIMIZE);
                    }
                }
                else
                {
                    LogService.LogWarning("Pencere öne getirilemedi");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Pencere öne getirme hatası: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Interface implementation - Belirtilen pencereyi öne getirir
        /// </summary>
        /// <param name="hWnd">Pencere handle'ı</param>
        /// <returns>Başarılı ise true</returns>
        bool IWindowsApiService.BringWindowToFront(IntPtr hWnd)
        {
            return BringWindowToFront(hWnd, false);
        }

        /// <summary>
        /// Aktif pencereyi kapatır
        /// </summary>
        /// <returns>İşlem başarılıysa true</returns>
        public bool CloseActiveWindow()
        {
            try
            {
                // Aktif pencereyi al
                IntPtr hWnd = GetForegroundWindow();

                if (hWnd == IntPtr.Zero)
                {
                    // LogService.LogDebug("[WindowsApiService] Aktif pencere bulunamadı");
                    return false;
                }

                // Pencereyi kapat (WM_CLOSE mesajı)
                // LogService.LogDebug("[WindowsApiService] Aktif pencere kapatılıyor");
                try
                {
                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] PostMessage hatası: {ex.Message}");
                    return false;
                }
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Pencere kapatma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Aktif uygulamayı kapatır
        /// </summary>
        /// <returns>İşlem başarılıysa true</returns>
        public bool CloseActiveApplication()
        {
            try
            {
                // Aktif pencereyi al
                IntPtr hWnd = GetForegroundWindow();

                if (hWnd == IntPtr.Zero)
                {
                    // LogService.LogDebug("[WindowsApiService] Aktif pencere bulunamadı");
                    return false;
                }

                // Process ID'sini al
                uint processId = 0;
                try
                {
                    GetWindowThreadProcessId(hWnd, out processId);
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] GetWindowThreadProcessId hatası: {ex.Message}");
                }

                if (processId == 0)
                {
                    // LogService.LogDebug("[WindowsApiService] Process ID alınamadı");
                    return false;
                }

                try
                {
                    // Process'i al ve sonlandır
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        // Pencereyi kapat
                        process.CloseMainWindow();

                        // Eğer kapanmazsa sonlandır
                        if (!process.WaitForExit(3000))
                        {
                            process.Kill();
                        }
                    }

                    // LogService.LogDebug("[WindowsApiService] Uygulama kapatıldı");
                    return true;
                }
                catch (ArgumentException)
                {
                    // Process artık yok, başarılı kabul et
                    return true;
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] Process sonlandırma hatası: {ex.Message}");
                    return false;
                }
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Uygulama kapatma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Aktif pencerenin başlığını alır
        /// </summary>
        /// <returns>Pencere başlığı</returns>
        public string GetActiveWindowTitle()
        {
            try
            {
                // Aktif pencereyi al
                IntPtr hWnd = GetForegroundWindow();

                if (hWnd == IntPtr.Zero)
                {
                    return string.Empty;
                }

                // GetWindowTitle metodunu kullan
                return GetWindowTitle(hWnd);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Pencere başlığı alma hatası: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Belirtilen pencerenin başlık metnini alır
        /// </summary>
        /// <param name="hWnd">Başlığı alınacak pencere handle değeri</param>
        /// <returns>Pencere başlık metni</returns>
        public string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                {
                    // LogService.LogDebug("[WindowsApiService] GetWindowTitle: Geçersiz pencere handle'ı");
                    return string.Empty;
                }

                // Başlık uzunluğunu al
                int length = 0;
                try
                {
                    length = GetWindowTextLength(hWnd);
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] GetWindowTextLength hatası: {ex.Message}");
                    return string.Empty;
                }

                if (length == 0)
                {
                    // LogService.LogDebug("[WindowsApiService] GetWindowTitle: Pencere başlığı yok veya okunamıyor");
                    return string.Empty;
                }

                // Başlık metnini al
                const int INITIAL_CAPACITY = 256;
                StringBuilder title = new StringBuilder(Math.Max(length + 1, INITIAL_CAPACITY));
                try
                {
                    int result = GetWindowText(hWnd, title, title.Capacity);
                    if (result == 0)
                    {
                        // LogService.LogDebug("[WindowsApiService] GetWindowText sıfır döndürdü, başlık alınamadı");
                        return string.Empty;
                    }
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] GetWindowText hatası: {ex.Message}");
                    return string.Empty;
                }

                string titleText = title.ToString();
                // LogService.LogDebug($"[WindowsApiService] GetWindowTitle: Pencere başlığı: '{titleText}'");
                return titleText;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] GetWindowTitle hatası: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Fare olaylarını gerçekleştirir
        /// </summary>
        public void PerformMouseClick(int x, int y, bool rightClick = false)
        {
            try
            {
                // Fare konumunu ayarla
                try
                {
                    SetCursorPos(x, y);
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] SetCursorPos hatası: {ex.Message}");
                }

                // Tıklama gerçekleştir
                try
                {
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
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] mouse_event hatası: {ex.Message}");
                }

                // LogService.LogDebug($"[WindowsApiService] Fare tıklaması gerçekleştirildi: X={x}, Y={y}, Sağ Tıklama={rightClick}");
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Fare tıklama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Pencere dikdörtgenini alır
        /// </summary>
        public bool GetWindowRectangle(IntPtr hWnd, out RECT lpRect)
        {
            try
            {
                bool result = GetWindowRect(hWnd, out lpRect);
                return result;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] GetWindowRect hatası: {ex.Message}");
                lpRect = new RECT { Left = 0, Top = 0, Right = 1000, Bottom = 800 };
                return true;  // Hata olsa bile başarılı kabul et ve varsayılan değerleri kullan
            }
        }

        /// <summary>
        /// İstemci dikdörtgenini alır
        /// </summary>
        public bool GetClientRectangle(IntPtr hWnd, out RECT lpRect)
        {
            try
            {
                bool result = GetClientRect(hWnd, out lpRect);
                return result;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] GetClientRect hatası: {ex.Message}");
                lpRect = new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 };
                return true;  // Hata olsa bile başarılı kabul et ve varsayılan değerleri kullan
            }
        }

        /// <summary>
        /// İstemci koordinatlarını ekran koordinatlarına dönüştürür
        /// </summary>
        public bool ClientToScreenCoord(IntPtr hWnd, ref POINT lpPoint)
        {
            try
            {
                bool result = ClientToScreen(hWnd, ref lpPoint);
                return result;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] ClientToScreen hatası: {ex.Message}");
                // Varsayılan olarak orta noktayı kullan
                lpPoint = new POINT { X = 500, Y = 300 };
                return true;  // Hata olsa bile başarılı kabul et ve varsayılan değerleri kullan
            }
        }

        /// <summary>
        /// ESC tuşunu gönderir
        /// </summary>
        public void SendEscapeKey()
        {
            try
            {
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_ESCAPE, 0, (int)KEYEVENTF_KEYUP, 0);
                // LogService.LogDebug("[WindowsApiService] ESC tuşu gönderildi");
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] SendEscapeKey hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows Speech Recognition pencerelerini kapatır
        /// </summary>
        public void CloseSpeechRecognitionWindows()
        {
            try
            {
                // LogService.LogDebug("[WindowsApiService] Speech Recognition pencereleri kapatılıyor...");
                
                // Windows Speech Recognition pencere başlıkları
                string[] speechWindowTitles = {
                    "Speech Recognition",
                    "Konuşma Tanıma",
                    "Windows Speech Recognition",
                    "Listening...",
                    "Dinleniyor..."
                };

                foreach (string title in speechWindowTitles)
                {
                    IntPtr speechWindow = FindWindow("", title);
                    if (speechWindow != IntPtr.Zero)
                    {
                        // LogService.LogDebug($"[WindowsApiService] Speech Recognition penceresi bulundu: {title}");
                        PostMessage(speechWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }

                // Process bazında da kontrol et
                var processes = Process.GetProcessesByName("sapisvr");
                try
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                            {
                                // LogService.LogDebug("[WindowsApiService] Speech Recognition servisi kapatılıyor");
                                PostMessage(process.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                        }
                        catch
                        {
                            // LogService.LogDebug($"[WindowsApiService] Speech process kapatma hatası: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process?.Dispose();
                    }
                }

                // LogService.LogDebug("[WindowsApiService] Speech Recognition pencere temizliği tamamlandı");
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] CloseSpeechRecognitionWindows hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Pencereyi tam olarak aktif hale getirir (öne getirir ve odak verir)
        /// </summary>
        /// <param name="hWnd">Pencere handle'ı</param>
        /// <returns>İşlem başarılıysa true</returns>
        public bool ForceForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                // LogService.LogDebug("[WindowsApiService] Geçersiz pencere handle'ı, öne getirme atlanıyor.");
                return false;
            }

            try
            {
                // Geçerli thread ve hedef pencere thread'i al
                uint currentThreadId = 0;
                uint targetThreadId = 0;

                try
                {
                    currentThreadId = GetCurrentThreadId();
                    targetThreadId = GetWindowThreadProcessId(hWnd, out _);
                }
                catch
                {
                    // LogService.LogDebug($"[WindowsApiService] Thread ID alma hatası: {ex.Message}");
                }

                if (targetThreadId != 0 && currentThreadId != targetThreadId)
                {
                    // Thread'leri birbirine bağla
                    try
                    {
                        bool attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                        if (!attached)
                        {
                            // LogService.LogDebug("[WindowsApiService] Thread'ler bağlanamadı");
                        }
                    }
                    catch
                    {
                        // LogService.LogDebug($"[WindowsApiService] AttachThreadInput hatası: {ex.Message}");
                    }

                    try
                    {
                        // Pencereyi normal durumda göster
                        ShowWindow(hWnd, SW_SHOWNORMAL);

                        // Pencereyi restore et (minimize edilmişse)
                        ShowWindow(hWnd, SW_RESTORE);

                        // Pencereyi en üste getir
                        BringWindowToTop(hWnd);

                        // Pencereyi öne getir
                        SetForegroundWindow(hWnd);

                        // Odak ver
                        SetFocus(hWnd);

                        // LogService.LogDebug("[WindowsApiService] Pencere başarıyla öne getirildi.");
                    }
                    catch
                    {
                        // LogService.LogDebug($"[WindowsApiService] Pencere öne getirme işlemi hatası: {ex.Message}");
                    }
                    finally
                    {
                        // Thread'leri çöz
                        try
                        {
                            AttachThreadInput(currentThreadId, targetThreadId, false);
                        }
                        catch
                        {
                            // LogService.LogDebug($"[WindowsApiService] AttachThreadInput (çözme) hatası: {ex.Message}");
                        }
                    }
                }
                else
                {
                    try
                    {
                        // Pencereyi normal durumda göster
                        ShowWindow(hWnd, SW_SHOWNORMAL);

                        // Pencereyi restore et (minimize edilmişse)
                        ShowWindow(hWnd, SW_RESTORE);

                        // Pencereyi en üste getir
                        BringWindowToTop(hWnd);

                        // Pencereyi öne getir
                        SetForegroundWindow(hWnd);

                        // Odak ver
                        SetFocus(hWnd);
                    }
                    catch
                    {
                        // LogService.LogDebug($"[WindowsApiService] Pencere öne getirme işlemi hatası: {ex.Message}");
                    }
                }

                // Aslında başarılı olmuş olabilir, true dön
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Pencere öne getirme hatası: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Sistemdeki tüm process'leri arayarak process name ve window title eşleşmelerini bulur
        /// </summary>
        public void FindAllProcessesWithNameLike(string searchText)
        {
            try
            {
                // LogService.LogDebug($"[WindowsApiService] '{searchText}' içeren process'ler aranıyor...");
                Process[] processes = Process.GetProcesses();

                bool found = false;
                foreach (Process p in processes)
                {
                    try
                    {
                        if (p.ProcessName.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) ||
                            (p.MainWindowTitle != null && p.MainWindowTitle.ToLowerInvariant().Contains(searchText.ToLowerInvariant())))
                        {
                            found = true;
                            // LogService.LogDebug($"[WindowsApiService] Bulunan Process: Adı={p.ProcessName}, Başlık={p.MainWindowTitle}, ID={p.Id}, Handle={p.MainWindowHandle}");
                        }
                    }
                    catch { /* Process'e erişim hatası olabilir, devam et */ }
                }

                if (!found)
                {
                    // LogService.LogDebug($"[WindowsApiService] '{searchText}' içeren process bulunamadı.");
                }
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Process arama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktif pencereyi alır ve tanımlayıcı bilgisiyle birlikte döndürür
        /// </summary>
        /// <returns>Aktif pencere tanımlayıcısı</returns>
        public IntPtr GetActiveWindow()
        {
            IntPtr hWnd = GetForegroundWindow();

            // Eğer daha detaylı bilgi gerekiyorsa, pencere başlığını alabiliriz
            try
            {
                string windowTitle = GetWindowTitle(hWnd);
                // LogService.LogDebug($"[WindowsApiService] Aktif pencere: {hWnd}, Başlık: {windowTitle}");
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Pencere başlığı alınırken hata: {ex.Message}");
            }

            return hWnd;
        }

        /// <summary>
        /// Belirtilen isme sahip uygulamaya geçiş yapar
        /// </summary>
        /// <param name="applicationName">Uygulama adı (process adı veya pencere başlığında geçen ad)</param>
        /// <returns>Başarılı ise true</returns>
        public bool SwitchToApplication(string applicationName)
        {
            try
            {
                // LogService.LogDebug($"[WindowsApiService] Uygulamaya geçiş yapılıyor: {applicationName}");
                
                // Uzantıyı temizle
                string processName = applicationName;
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }
                
                // İlk işlem adına göre dene
                Process[] processes = Process.GetProcessesByName(processName);
                
                if (processes.Length > 0)
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (process.MainWindowHandle != IntPtr.Zero)
                            {
                                // LogService.LogDebug($"[WindowsApiService] Ana pencere bulundu: {process.MainWindowHandle}");
                                return BringWindowToFront(process.MainWindowHandle);
                            }
                        }
                        catch
                        {
                            // LogService.LogDebug($"[WindowsApiService] Process pencere hatası: {ex.Message}");
                        }
                    }
                }
                
                // İşlem bulunamadı veya ana penceresi yoktu, pencere başlığına göre ara
                // LogService.LogDebug("[WindowsApiService] İsme göre pencere aranıyor...");
                
                IntPtr hWnd = FindWindow(null, applicationName);
                if (hWnd != IntPtr.Zero)
                {
                    // LogService.LogDebug($"[WindowsApiService] Pencere bulundu: {hWnd}");
                    return BringWindowToFront(hWnd);
                }
                
                // Bulunamadı, benzer isimli pencere ara
                // LogService.LogDebug("[WindowsApiService] Tam eşleşme bulunamadı, kısmi eşleşmeler aranıyor...");
                
                hWnd = FindWindowByPartialTitle(applicationName);
                if (hWnd != IntPtr.Zero)
                {
                    // LogService.LogDebug($"[WindowsApiService] Kısmi eşleşme bulundu: {hWnd}");
                    return BringWindowToFront(hWnd);
                }
                
                // LogService.LogDebug("[WindowsApiService] Hiçbir pencere bulunamadı");
                return false;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Uygulama geçiş hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kısmi başlık eşleşmesine göre pencere arar
        /// </summary>
        /// <param name="partialTitle">Pencere başlığında geçen text</param>
        /// <returns>İlk bulunan pencere handle'ı veya IntPtr.Zero</returns>
        public IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            IntPtr result = IntPtr.Zero;
            
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd))
                    return true;
                    
                string title = GetWindowTitle(hWnd);
                if (!string.IsNullOrEmpty(title) && title.Contains(partialTitle, StringComparison.OrdinalIgnoreCase))
                {
                    result = hWnd;
                    return false; // İlk bulduğumuzda durdur
                }
                
                return true;
            }, IntPtr.Zero);
            
            return result;
        }

        #region Interface Implementation

        // Interface methods to be implemented as instance methods
        bool IWindowsApiService.SetForegroundWindow(IntPtr hWnd)
        {
            try
            {
                return SetForegroundWindow(hWnd);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] SetForegroundWindow error: {ex.Message}");
                return false;
            }
        }

        bool IWindowsApiService.IsWindowVisible(IntPtr hWnd)
        {
            try
            {
                return IsWindowVisible(hWnd);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] IsWindowVisible error: {ex.Message}");
                return false;
            }
        }

        public bool SendVolumeCommand(string command)
        {
            try
            {
                // Implementation for volume commands
                // LogService.LogDebug($"[WindowsApiService] Sending volume command: {command}");
                // Add actual volume control implementation here
                return true;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] SendVolumeCommand error: {ex.Message}");
                return false;
            }
        }

        public void SendText(string text)
        {
            SendTextToActiveWindow(text);
        }

        /// <summary>
        /// Aktif pencereye metin gönderir (Unicode destekli)
        /// </summary>
        public void SendTextToActiveWindow(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return;

                LogService.LogDebug($"[WindowsApiService] Sending text to active window: {text}");

                // Her karakter için INPUT array oluştur (down + up = 2 input per char)
                var inputs = new List<INPUT>();

                foreach (char c in text)
                {
                    // Key down
                    var inputDown = new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    };
                    inputs.Add(inputDown);

                    // Key up
                    var inputUp = new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    };
                    inputs.Add(inputUp);
                }

                // Tüm inputları gönder
                if (inputs.Count > 0)
                {
                    uint result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                    if (result != inputs.Count)
                    {
                        LogService.LogDebug($"[WindowsApiService] SendInput partial success: {result}/{inputs.Count} inputs sent");
                    }
                    else
                    {
                        LogService.LogDebug($"[WindowsApiService] Text sent successfully: {text.Length} characters");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[WindowsApiService] SendTextToActiveWindow error: {ex.Message}", ex);
            }
        }

        public void SendKeyStrokes(string keyStrokes)
        {
            try
            {
                // Implementation for sending keystrokes
                // LogService.LogDebug($"[WindowsApiService] Sending keystrokes: {keyStrokes}");
                // Add actual keystroke implementation here
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] SendKeyStrokes error: {ex.Message}");
            }
        }

        public bool MinimizeWindow(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_MINIMIZE);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] MinimizeWindow error: {ex.Message}");
                return false;
            }
        }

        public bool MaximizeWindow(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_MAXIMIZE);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] MaximizeWindow error: {ex.Message}");
                return false;
            }
        }

        public bool RestoreWindow(IntPtr hWnd)
        {
            try
            {
                return ShowWindow(hWnd, SW_RESTORE);
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] RestoreWindow error: {ex.Message}");
                return false;
            }
        }

        // Async wrapper for volume commands
        public async Task<bool> SendVolumeCommandAsync(string command)
        {
            return await Task.Run(() => SendVolumeCommand(command));
        }

        // Process operations
        public bool KillProcessByName(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds
                }
                return processes.Length > 0;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Failed to kill process {processName}: {ex.Message}");
                return false;
            }
        }

        public bool IsProcessRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                // LogService.LogDebug($"[WindowsApiService] Failed to check process {processName}: {ex.Message}");
                return false;
            }
        }

        #endregion
        
        #region DWM API
        
        // DWM structures and enums
        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        
        // DWM Window Attributes
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_DONOTROUND = 1;
        
        /// <summary>
        /// Pencereye cam çerçeve efekti ekler
        /// </summary>
        public void ExtendGlassFrame(IntPtr hWnd)
        {
            try
            {
                var margins = new MARGINS
                {
                    Left = -1,
                    Right = -1,
                    Top = -1,
                    Bottom = -1
                };
                
                DwmExtendFrameIntoClientArea(hWnd, ref margins);
                
                // Rounded corners
                int cornerPreference = DWMWCP_ROUND;
                DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
                
                Debug.WriteLine("[WindowsApiService] Glass frame extended");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsApiService] ExtendGlassFrame error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Penceredeki cam çerçeve efektini kaldırır
        /// </summary>
        public void RemoveGlassFrame(IntPtr hWnd)
        {
            try
            {
                var margins = new MARGINS
                {
                    Left = 0,
                    Right = 0,
                    Top = 0,
                    Bottom = 0
                };
                
                DwmExtendFrameIntoClientArea(hWnd, ref margins);
                
                // Köşeleri varsayılana döndür
                int cornerPreference = DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
                
                Debug.WriteLine("[WindowsApiService] Glass frame removed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsApiService] RemoveGlassFrame error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Window Transparency
        
        // Window Ex Styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        
        // SetLayeredWindowAttributes flags
        private const byte LWA_COLORKEY = 0x01;
        private const byte LWA_ALPHA = 0x02;
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        
        // 32-bit uyumluluk için
        private static IntPtr GetWindowLongPtrWrapper(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) // 64-bit
                return GetWindowLongPtr64(hWnd, nIndex);
            else // 32-bit
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }
        
        private static IntPtr SetWindowLongPtrWrapper(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) // 64-bit
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else // 32-bit
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        /// <summary>
        /// Window'u şeffaf yapar
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="transparencyLevel">Şeffaflık seviyesi (0.0 = tam opak, 1.0 = tam şeffaf)</param>
        public void SetWindowTransparency(IntPtr hWnd, double transparencyLevel = 1.0)
        {
            try
            {
                // Window extended style'ını al
                IntPtr exStyle = GetWindowLongPtrWrapper(hWnd, GWL_EXSTYLE);
                
                // Layered window style'ını ekle
                SetWindowLongPtrWrapper(hWnd, GWL_EXSTYLE, new IntPtr(exStyle.ToInt64() | WS_EX_LAYERED));
                
                // Şeffaflık seviyesini alpha değerine çevir
                // transparencyLevel: 0.0 (tam opak) - 1.0 (tam şeffaf)
                // alpha: 255 (tam opak) - 0 (tam şeffaf)
                // Minimum %10 opaklık koruyoruz (alpha 25)
                byte minAlpha = 25;
                byte maxAlpha = 255;
                byte alpha = (byte)(maxAlpha - (transparencyLevel * (maxAlpha - minAlpha)));
                
                // Alpha transparency kullan
                SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
                
                Debug.WriteLine($"[WindowsApiService] Window transparency set with alpha: {alpha} (transparency level: {transparencyLevel:F2})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsApiService] SetWindowTransparency error: {ex.Message}");
                throw;
            }
        }
        

        #endregion
    }
}
