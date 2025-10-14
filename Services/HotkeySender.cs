using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuadroAIPilot.Services
{
    public static class HotkeySender
    {
        /// <summary>
        /// Alt+Tab ile odağı bir önceki pencereye geçirir.
        /// </summary>
        public static void AltTab()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_TAB, 0, 0, 0);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Hedef uygulamayı öne getirir (örnek: en son aktif pencereyi odaklar).
        /// Artırılmış versiyon - daha güvenilir ve detaylı log içerir.
        /// </summary>
        public static void FocusTargetWindow()
        {
            try
            {
                Debug.WriteLine("[HotkeySender] FocusTargetWindow çağrıldı - hedef pencereye odaklanma başlatılıyor...");

                // Aktif pencereyi doğrudan odakla (Alt+Tab sonrası)
                var hWnd = GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    Debug.WriteLine($"[HotkeySender] Aktif pencere bulundu: {hWnd}");

                    // Pencereyi öne getirmek ve odaklamak için çoklu yöntemler kullan
                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);

                    // Thread input ataması - odak problemlerini çözmek için
                    uint foregroundThreadId = GetWindowThreadProcessId(hWnd, out _);
                    uint currentThreadId = GetCurrentThreadId();

                    if (foregroundThreadId != currentThreadId)
                    {
                        Debug.WriteLine($"[HotkeySender] Thread input ataması yapılıyor ({currentThreadId} -> {foregroundThreadId})");
                        if (AttachThreadInput(currentThreadId, foregroundThreadId, true))
                        {
                            SetFocus(hWnd);
                            AttachThreadInput(currentThreadId, foregroundThreadId, false);
                        }
                        else
                        {
                            Debug.WriteLine("[HotkeySender] Thread input ataması başarısız oldu");
                        }
                    }

                    Debug.WriteLine("[HotkeySender] Pencere odaklama işlemi tamamlandı");
                }
                else
                {
                    Debug.WriteLine("[HotkeySender] Aktif pencere bulunamadı!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeySender] Pencere odaklama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Basit klavye tuş-enjeksiyonu yardımcı sınıfı.
        /// Yalnızca Win32 <c>keybd_event</c> API'sini kullanır; ek paket gerektirmez.        /// </summary>
        // Sanal tuş kodları
        private const byte VK_LWIN = 0x5B;
        private const byte VK_LCTRL = 0xA2;
        private const byte VK_LSHIFT = 0xA0;
        private const byte VK_LALT = 0xA4;
        private const byte VK_TAB = 0x09;
        private const byte VK_ESCAPE = 0x1B;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_SPACE = 0x20;
        private const byte VK_F2 = 0x71;
        private const byte VK_F4 = 0x73;
        private const byte VK_F5 = 0x74;
        private const byte VK_F9 = 0x78;
        private const byte VK_HOME = 0x24;
        private const byte VK_END = 0x23;
        private const byte VK_PGUP = 0x21;
        private const byte VK_PGDN = 0x22;
        private const byte VK_LEFT = 0x25;
        private const byte VK_UP = 0x26;
        private const byte VK_RIGHT = 0x27;
        private const byte VK_DOWN = 0x28;
        private const byte VK_INSERT = 0x2D;
        private const byte VK_DELETE = 0x2E;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_CAPSLOCK = 0x14;
        private const byte VK_A = 0x41;
        private const byte VK_B = 0x42;
        private const byte VK_C = 0x43;
        private const byte VK_D = 0x44;
        private const byte VK_E = 0x45;
        private const byte VK_F = 0x46;
        private const byte VK_G = 0x47;
        private const byte VK_H = 0x48;
        private const byte VK_I = 0x49;
        private const byte VK_J = 0x4A;
        private const byte VK_K = 0x4B;
        private const byte VK_L = 0x4C;
        private const byte VK_M = 0x4D;
        private const byte VK_N = 0x4E;
        private const byte VK_O = 0x4F;
        private const byte VK_P = 0x50;
        private const byte VK_Q = 0x51;
        private const byte VK_R = 0x52;
        private const byte VK_S = 0x53;
        private const byte VK_T = 0x54;
        private const byte VK_U = 0x55;
        private const byte VK_V = 0x56;
        private const byte VK_W = 0x57;
        private const byte VK_X = 0x58;
        private const byte VK_Y = 0x59;
        private const byte VK_Z = 0x5A;
        private const byte VK_1 = 0x31;
        private const byte VK_2 = 0x32;
        private const byte VK_3 = 0x33;
        private const byte VK_4 = 0x34;
        private const byte VK_5 = 0x35;
        private const byte VK_6 = 0x36;
        private const byte VK_7 = 0x37;
        private const byte VK_8 = 0x38;
        private const byte VK_9 = 0x39;

        // keybd_event bayrakları
        private const int KEYEVENTF_KEYUP = 0x2;
        private const int KEYEVENTF_EXTENDEDKEY = 0x1;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();


        //────────────────────  GENEL AMAÇLI TUŞLAR  ───────────────────

        /// <summary>Enter tuşu gönderir.</summary>
        public static void SendEnter()
        {
            keybd_event(VK_RETURN, 0, 0, 0);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Tab tuşu gönderir.</summary>
        public static void SendTab()
        {
            keybd_event(VK_TAB, 0, 0, 0);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Space (boşluk) tuşu gönderir.</summary>
        public static void SendSpace()
        {
            keybd_event(VK_SPACE, 0, 0, 0);
            keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>"Kabul et" komutu için varsayılan tuş kombinasyonu (Enter).</summary>
        public static void SendAccept()
        {
            SendEnter();
        }

        /// <summary>"Vazgeç" komutu için varsayılan tuş kombinasyonu (Escape).</summary>
        public static void SendCancel()
        {
            SendEscape();
        }

        /// <summary>Escape – ör. dikte panelini kapatmak için.</summary>
        public static void SendEscape()
        {
            keybd_event(VK_ESCAPE, 0, 0, 0);
            keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Bilgisayarı kilitle (Win + L).</summary>
        public static void LockComputer()
        {
            try
            {
                // Windows'un yerleşik kilitleme API'si
                if (!LockWorkStation())
                {
                    // Eğer başarısız olursa Win+L ile deneyelim
                    keybd_event(VK_LWIN, 0, 0, 0);
                    keybd_event(VK_L, 0, 0, 0);
                    keybd_event(VK_L, 0, KEYEVENTF_KEYUP, 0);
                    keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch
            {
                // Son çare olarak yine Win+L
                keybd_event(VK_LWIN, 0, 0, 0);
                keybd_event(VK_L, 0, 0, 0);
                keybd_event(VK_L, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
            }
        }


        /// <summary>Masaüstünü göster (Win + D).</summary>
        public static void ShowDesktop()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_D, 0, 0, 0);
            keybd_event(VK_D, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Pano geçmişini aç (Win + V).</summary>
        public static void OpenClipboardHistory()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_V, 0, 0, 0);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Ekran görüntüsü al (Win + Shift + S).</summary>
        public static void CaptureScreenshot()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_S, 0, 0, 0);
            keybd_event(VK_S, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Uygulamayı kapat (Alt + F4).</summary>
        public static void CloseApplication()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_F4, 0, 0, 0);
            keybd_event(VK_F4, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Güvenli şekilde aktif pencereyi kapat - asistan penceresini değil
        /// </summary>
        /// <param name="windowsApiService">API servisi</param>
        /// <returns>İşlem başarılı mı?</returns>
        public static bool CloseApplicationSafely(WindowsApiService windowsApiService)
        {
            try
            {
                // Aktif pencere bilgisi alınır
                var activeWindow = windowsApiService.GetActiveWindow();
                string windowTitle = windowsApiService.GetWindowTitle(activeWindow);

                // Eğer pencere başlığında "QuadroAIPilot" veya "Asistan" geçiyorsa kapatmama korumas
                bool isAssistantWindow = windowTitle.Contains("QuadroAIPilot", StringComparison.OrdinalIgnoreCase) ||
                                         windowTitle.Contains("Asistan", StringComparison.OrdinalIgnoreCase);

                Debug.WriteLine($"[HotkeySender] Kapatma işlemi için pencere kontrolü: {windowTitle}, Asistan penceresi mi: {isAssistantWindow}");

                if (!isAssistantWindow)
                {
                    // Asistan değilse kapat
                    CloseApplication();
                    return true;
                }
                else
                {
                    Debug.WriteLine("[HotkeySender] Asistan penceresi algılandı, kapatma işlemi engellendi!");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeySender] Pencere kapatma hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>Çalıştır penceresini aç (Win + R).</summary>
        public static void OpenRun()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_R, 0, 0, 0);
            keybd_event(VK_R, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Görev görünümünü aç (Win + Tab).</summary>
        public static void OpenTaskView()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_TAB, 0, 0, 0);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yeni klasör oluştur (Ctrl + Shift + N).</summary>
        public static void CreateNewFolder()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_N, 0, 0, 0);
            keybd_event(VK_N, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sekmeyi kapat (Ctrl + W).</summary>
        public static void CloseTab()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_W, 0, 0, 0);
            keybd_event(VK_W, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Bul (Ctrl + F).</summary>
        public static void Find()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_F, 0, 0, 0);
            keybd_event(VK_F, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yazdır (Ctrl + P).</summary>
        public static void Print()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_P, 0, 0, 0);
            keybd_event(VK_P, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Kaydet (Ctrl + S).</summary>
        public static void Save()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_S, 0, 0, 0);
            keybd_event(VK_S, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Kopyala (Ctrl + C).</summary>
        public static void Copy()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_C, 0, 0, 0);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Kes (Ctrl + X).</summary>
        public static void Cut()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_X, 0, 0, 0);
            keybd_event(VK_X, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yapıştır (Ctrl + V).</summary>
        public static void Paste()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_V, 0, 0, 0);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Geri al (Ctrl + Z).</summary>
        public static void Undo()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_Z, 0, 0, 0);
            keybd_event(VK_Z, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>İleri al (Ctrl + Y).</summary>
        public static void Redo()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_Y, 0, 0, 0);
            keybd_event(VK_Y, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Tümünü seç (Ctrl + A).</summary>
        public static void SelectAll()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_A, 0, 0, 0);
            keybd_event(VK_A, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sayfa başına git (Ctrl + Home).</summary>
        public static void GoToPageStart()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_HOME, 0, 0, 0);
            keybd_event(VK_HOME, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sayfa sonuna git (Ctrl + End).</summary>
        public static void GoToPageEnd()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_END, 0, 0, 0);
            keybd_event(VK_END, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yenile (F5).</summary>
        public static void Refresh()
        {
            keybd_event(VK_F5, 0, 0, 0);
            keybd_event(VK_F5, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yeniden adlandır (F2).</summary>
        public static void Rename()
        {
            keybd_event(VK_F2, 0, 0, 0);
            keybd_event(VK_F2, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Pencereyi sağa hizala (Win + Sağ).</summary>
        public static void SnapWindowRight()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_RIGHT, 0, 0, 0);
            keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Pencereyi sola hizala (Win + Sol).</summary>
        public static void SnapWindowLeft()
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_LEFT, 0, 0, 0);
            keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Tarayıcıda ileri git (Alt + Sağ).</summary>
        public static void BrowserForward()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_RIGHT, 0, 0, 0);
            keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Tarayıcıda geri git (Alt + Sol).</summary>
        public static void BrowserBack()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_LEFT, 0, 0, 0);
            keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Sesi kısar (Volume Down).
        /// </summary>
        public static void VolumeDown()
        {
            // 5 kez basarak %10 azalış sağla
            for (int i = 0; i < 5; i++)
            {
                keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                System.Threading.Thread.Sleep(50); // Kısa bekleme
            }
        }

        /// <summary>
        /// Sesi yükseltir (Volume Up).
        /// </summary>
        public static void VolumeUp()
        {
            // 5 kez basarak %10 artış sağla
            for (int i = 0; i < 5; i++)
            {
                keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                System.Threading.Thread.Sleep(50); // Kısa bekleme
            }
        }

        /// <summary>
        /// Sesi kapatır/açar (Volume Mute).
        /// </summary>
        public static void VolumeMute()
        {
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yeni sekme aç (Ctrl + T).</summary>
        public static void NewTab()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_T, 0, 0, 0);
            keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yeni pencere aç (Ctrl + N).</summary>
        public static void NewWindow()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_N, 0, 0, 0);
            keybd_event(VK_N, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yeni sayfa oluştur (Ctrl + Enter) - Word ve diğer ofis uygulamaları için.</summary>
        public static void NewPage()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_RETURN, 0, 0, 0);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Enter tuşu.</summary>
        public static void PressEnter()
        {
            keybd_event(VK_RETURN, 0, 0, 0);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sayfayı aşağı kaydır (Page Down).</summary>
        public static void PageDown()
        {
            keybd_event(VK_PGDN, 0, 0, 0);
            keybd_event(VK_PGDN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sayfayı yukarı kaydır (Page Up).</summary>
        public static void PageUp()
        {
            keybd_event(VK_PGUP, 0, 0, 0);
            keybd_event(VK_PGUP, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Caps Lock aç/kapat.</summary>
        public static void ToggleCapsLock()
        {
            keybd_event(VK_CAPSLOCK, 0, 0, 0);
            keybd_event(VK_CAPSLOCK, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sağ ok tuşu.</summary>
        public static void PressRightArrow()
        {
            keybd_event(VK_RIGHT, 0, 0, 0);
            keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Sol ok tuşu.</summary>
        public static void PressLeftArrow()
        {
            keybd_event(VK_LEFT, 0, 0, 0);
            keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Yukarı ok tuşu.</summary>
        public static void PressUpArrow()
        {
            keybd_event(VK_UP, 0, 0, 0);
            keybd_event(VK_UP, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Aşağı ok tuşu.</summary>
        public static void PressDownArrow()
        {
            keybd_event(VK_DOWN, 0, 0, 0);
            keybd_event(VK_DOWN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Tab tuşu (Sonraki).</summary>
        public static void PressTab()
        {
            keybd_event(VK_TAB, 0, 0, 0);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Shift + Tab tuşu (Önceki).</summary>
        public static void PressShiftTab()
        {
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_TAB, 0, 0, 0);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Escape tuşu.</summary>
        public static void PressEscape()
        {
            keybd_event(VK_ESCAPE, 0, 0, 0);
            keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>Belirtilen klasörü açar veya zaten açıksa öne getirir</summary>
        public static void OpenFolder(string folderPath)
        {
            try
            {
                // Güvenlik kontrolü - path traversal ve güvenli olmayan yollar
                if (!string.IsNullOrWhiteSpace(folderPath) && !SecurityValidator.IsPathSafe(folderPath))
                {
                    LoggingService.LogWarning($"Unsafe folder path blocked: {folderPath}");
                    return;
                }

                // Sadece explorer.exe ile aç, açık pencereyi öne getirme WinUI 3 ve .NET 8 ile desteklenmiyor
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
                Debug.WriteLine($"[HotkeySender] Klasör açıldı: {folderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeySender] Klasör açma hatası: {ex.Message}");
            }
        }

        /// <summary>Varsayılan klasörleri açar veya zaten açıksa öne getirir</summary>
        public static void OpenSpecialFolder(string folderName)
        {
            try
            {
                string path = folderName.ToLowerInvariant() switch
                {
                    "belgelerim" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "belgeler" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "resimlerim" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "resimler" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "müziklerim" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "müzik" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "videolarım" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "videolar" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "indirilenler" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "masaüstü" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    _ => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                // OpenFolder metodunu kullan (zaten açık klasörü bulma mantığıyla)
                OpenFolder(path);
                Debug.WriteLine($"[HotkeySender] Özel klasör açma/öne getirme isteği: {folderName} - {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeySender] Özel klasör açma hatası: {ex.Message}");
            }
        }

        // Ek P/Invoke tanımları
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        //---------------------- OUTLOOK KOMUTLARI ----------------------

        /// <summary>
        /// Outlook'ta yeni bir e-posta oluşturur (Ctrl+N)
        /// </summary>
        public static void OutlookNewMail()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_N, 0, 0, 0);
            keybd_event(VK_N, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta seçili e-postayı yanıtlar (Ctrl+R)
        /// </summary>
        public static void OutlookReply()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_R, 0, 0, 0);
            keybd_event(VK_R, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta seçili e-postayı tüm alıcılara yanıtlar (Ctrl+Shift+R)
        /// </summary>
        public static void OutlookReplyAll()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_R, 0, 0, 0);
            keybd_event(VK_R, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta seçili e-postayı başkalarına iletir (Ctrl+F)
        /// </summary>
        public static void OutlookForward()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_F, 0, 0, 0);
            keybd_event(VK_F, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta hazırlanan e-postayı gönderir (Alt+S)
        /// </summary>
        public static void OutlookSendMail()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_S, 0, 0, 0);
            keybd_event(VK_S, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta posta kutusuna gider (Ctrl+1)
        /// </summary>
        public static void OutlookGoToInbox()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_1, 0, 0, 0);
            keybd_event(VK_1, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta takvim görünümüne geçer (Ctrl+2)
        /// </summary>
        public static void OutlookGoToCalendar()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_2, 0, 0, 0);
            keybd_event(VK_2, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta kişiler bölümüne geçer (Ctrl+3)
        /// </summary>
        public static void OutlookGoToContacts()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_3, 0, 0, 0);
            keybd_event(VK_3, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta görevler bölümüne geçer (Ctrl+4)
        /// </summary>
        public static void OutlookGoToTasks()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_4, 0, 0, 0);
            keybd_event(VK_4, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta notlar bölümüne geçer (Ctrl+5)
        /// </summary>
        public static void OutlookGoToNotes()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_5, 0, 0, 0);
            keybd_event(VK_5, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta sonraki e-postaya geçer (Aşağı ok tuşu)
        /// </summary>
        public static void OutlookNextMail()
        {
            keybd_event(VK_DOWN, 0, 0, 0);
            keybd_event(VK_DOWN, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta önceki e-postaya geçer (Yukarı ok tuşu)
        /// </summary>
        public static void OutlookPreviousMail()
        {
            keybd_event(VK_UP, 0, 0, 0);
            keybd_event(VK_UP, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta yeni bir toplantı oluşturur (Ctrl+Shift+Q)
        /// </summary>
        public static void OutlookNewMeeting()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_Q, 0, 0, 0);
            keybd_event(VK_Q, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta yeni bir randevu oluşturur (Ctrl+Shift+A)
        /// </summary>
        public static void OutlookNewAppointment()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_A, 0, 0, 0);
            keybd_event(VK_A, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postaya dosya ekler (Alt+N, A)
        /// Async hale getirildi
        /// </summary>
        public static async Task OutlookAttachFileAsync()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_N, 0, 0, 0);
            keybd_event(VK_N, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);

            // Task.Delay kullanarak async bekleme
            await Task.Delay(100);

            keybd_event(VK_A, 0, 0, 0);
            keybd_event(VK_A, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postaya dosya ekler (Sync versiyon - geriye uyumluluk için)
        /// </summary>
        public static void OutlookAttachFile()
        {
            // Deadlock riskini önlemek için Task.Run kullan
            var task = Task.Run(OutlookAttachFileAsync);
            try
            {
                task.Wait(TimeSpan.FromSeconds(10)); // Timeout ekle
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"[HotkeySender] OutlookAttachFile error: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Outlook'ta mailleri kontrol etmek için Gönder/Al işlemini tetikler (F9)
        /// </summary>
        public static void OutlookCheckMails()
        {
            keybd_event(VK_F9, 0, 0, 0);
            keybd_event(VK_F9, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta arama kutusuna odaklanır (Ctrl+E)
        /// </summary>
        public static void OutlookSearchMail()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_E, 0, 0, 0);
            keybd_event(VK_E, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta tüm filtreleri ve aramaları temizler (Ctrl+6)
        /// </summary>
        public static void OutlookClearFilters()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_6, 0, 0, 0);
            keybd_event(VK_6, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı kategorize eder (Ctrl+Shift+C)
        /// </summary>
        public static void OutlookCategorize()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_C, 0, 0, 0);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta arama yapar (Ctrl+E)
        /// </summary>
        public static void OutlookSearch()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_E, 0, 0, 0);
            keybd_event(VK_E, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta gelen kutusunu filtrelemek için (Ctrl+Shift+F)
        /// </summary>
        public static void OutlookFilter()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_F, 0, 0, 0);
            keybd_event(VK_F, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta okunmamış e-postalara gitmek için (Ctrl+U)
        /// Async hale getirildi
        /// </summary>
        public static async Task OutlookGoToUnreadMailAsync()
        {
            // Önce posta kutusuna git
            OutlookGoToInbox();

            // Task.Delay kullanarak async bekleme
            await Task.Delay(200);

            // Okunmamışları filtrele (Ctrl+U)
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_U, 0, 0, 0);
            keybd_event(VK_U, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta okunmamış e-postalara gitmek için (Sync versiyon)
        /// </summary>
        public static void OutlookGoToUnreadMail()
        {
            var task = Task.Run(OutlookGoToUnreadMailAsync);
            try
            {
                task.Wait(TimeSpan.FromSeconds(10)); // Timeout ekle
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"[HotkeySender] OutlookGoToUnreadMail error: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Outlook'ta taslaklar klasörüne gider (Ctrl+Y ile klasör seçimi)
        /// Düzeltildi: Outlook'ta doğru yöntem kullanılıyor
        /// </summary>
        public static void OutlookGoToDrafts()
        {
            // Ctrl+Y ile klasör listesini aç
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_Y, 0, 0, 0);
            keybd_event(VK_Y, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);

            // Not: Kullanıcı buradan "Taslaklar" klasörünü seçmeli
            Debug.WriteLine("[HotkeySender] Klasör seçimi açıldı - Taslaklar seçilmeli");
        }

        /// <summary>
        /// Outlook'ta gönderilen öğeler klasörüne gider (Ctrl+Y ile klasör seçimi)
        /// Düzeltildi: Outlook'ta doğru yöntem kullanılıyor
        /// </summary>
        public static void OutlookGoToSentItems()
        {
            // Ctrl+Y ile klasör listesini aç
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_Y, 0, 0, 0);
            keybd_event(VK_Y, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);

            // Not: Kullanıcı buradan "Gönderilen Öğeler" klasörünü seçmeli
            Debug.WriteLine("[HotkeySender] Klasör seçimi açıldı - Gönderilen Öğeler seçilmeli");
        }

        /// <summary>
        /// Outlook'ta e-postayı siler (Delete)
        /// </summary>
        public static void OutlookDeleteMail()
        {
            keybd_event(VK_DELETE, 0, 0, 0);
            keybd_event(VK_DELETE, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı arşivler (E tuşu)
        /// </summary>
        public static void OutlookArchiveMail()
        {
            keybd_event(VK_E, 0, 0, 0);
            keybd_event(VK_E, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı okunmadı olarak işaretler (Ctrl+U)
        /// </summary>
        public static void OutlookMarkAsUnread()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_U, 0, 0, 0);
            keybd_event(VK_U, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı okundu olarak işaretler (Ctrl+Q)
        /// </summary>
        public static void OutlookMarkAsRead()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_Q, 0, 0, 0);
            keybd_event(VK_Q, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı bayrakla işaretler (Insert)
        /// </summary>
        public static void OutlookFlagMail()
        {
            keybd_event(VK_INSERT, 0, 0, 0);
            keybd_event(VK_INSERT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta randevulara gider
        /// Async hale getirildi
        /// </summary>
        public static async Task OutlookGoToAppointmentsAsync()
        {
            // Önce takvime git
            OutlookGoToCalendar();

            // Task.Delay kullanarak async bekleme
            await Task.Delay(200);

            // Randevulara git (Alt+2)
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_2, 0, 0, 0);
            keybd_event(VK_2, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta randevulara gider (Sync versiyon)
        /// </summary>
        public static void OutlookGoToAppointments()
        {
            var task = Task.Run(OutlookGoToAppointmentsAsync);
            try
            {
                task.Wait(TimeSpan.FromSeconds(10)); // Timeout ekle
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"[HotkeySender] OutlookGoToAppointments error: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Outlook'ta postaları gönderir/alır
        /// </summary>
        public static void OutlookSendReceive()
        {
            keybd_event(VK_F9, 0, 0, 0);
            keybd_event(VK_F9, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta öğeyi yazdırır (Ctrl+P)
        /// </summary>
        public static void OutlookPrint()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_P, 0, 0, 0);
            keybd_event(VK_P, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta bir e-postayı önemli olarak işaretler (Ctrl+Shift+I)
        /// </summary>
        public static void OutlookMarkAsImportant()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_LSHIFT, 0, 0, 0);
            keybd_event(VK_I, 0, 0, 0);
            keybd_event(VK_I, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı iletilerinizin arasına ekler (Alt+J, Alt+A)
        /// Async hale getirildi
        /// </summary>
        public static async Task OutlookAddToMessagesAsync()
        {
            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_J, 0, 0, 0);
            keybd_event(VK_J, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);

            // Task.Delay kullanarak async bekleme
            await Task.Delay(100);

            keybd_event(VK_LALT, 0, 0, 0);
            keybd_event(VK_A, 0, 0, 0);
            keybd_event(VK_A, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LALT, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Outlook'ta e-postayı iletilerinizin arasına ekler (Sync versiyon)
        /// </summary>
        public static void OutlookAddToMessages()
        {
            var task = Task.Run(OutlookAddToMessagesAsync);
            try
            {
                task.Wait(TimeSpan.FromSeconds(10)); // Timeout ekle
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"[HotkeySender] OutlookAddToMessages error: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Outlook'ta hızlı yanıt verir (Ctrl+R)
        /// </summary>
        public static void OutlookQuickReply()
        {
            keybd_event(VK_LCTRL, 0, 0, 0);
            keybd_event(VK_R, 0, 0, 0);
            keybd_event(VK_R, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCTRL, 0, KEYEVENTF_KEYUP, 0);
        }        /// <summary>
        /// Özel tuş kombinasyonu gönderir (CommandMetadata'dan alınan).
        /// </summary>
        /// <param name="keyCombination">Tuş kombinasyonu string formatında: "Ctrl+Alt+Z", "Win+D" gibi</param>
        /// <returns>İşlemin başarılı olup olmadığı</returns>
        public static async Task<bool> SendCustomKeyCombination(string keyCombination)
        {
            if (string.IsNullOrWhiteSpace(keyCombination))
            {
                Debug.WriteLine("[HotkeySender] Boş tuş kombinasyonu belirtildi.");
                return false;
            }

            try
            {
                Debug.WriteLine($"[HotkeySender] Tuş kombinasyonu gönderiliyor: {keyCombination}");
                
                // Tuş kombinasyonunu parçalara ayır
                string[] keys = keyCombination.Split('+');
                List<byte> modifiers = new List<byte>();
                byte? mainKey = null;
                
                foreach (string key in keys)
                {
                    string trimmedKey = key.Trim();
                    
                    // Modifier tuşları belirle
                    switch (trimmedKey.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                            modifiers.Add(VK_LCTRL);
                            break;
                        case "alt":
                            modifiers.Add(VK_LALT);
                            break;
                        case "shift":
                            modifiers.Add(VK_LSHIFT);
                            break;
                        case "win":
                        case "windows":
                            modifiers.Add(VK_LWIN);
                            break;
                        // Özel tuşlar
                        case "enter":
                        case "return":
                            mainKey = VK_RETURN;
                            break;
                        case "esc":
                        case "escape":
                            mainKey = VK_ESCAPE;
                            break;
                        case "space":
                        case "boşluk":
                            mainKey = VK_SPACE;
                            break;                        case "tab":
                            mainKey = VK_TAB;
                            break;
                        // Ok tuşları
                        case "up":
                        case "yukarı":
                            mainKey = VK_UP;
                            break;
                        case "down":
                        case "aşağı":
                            mainKey = VK_DOWN;
                            break;
                        case "left":
                        case "sol":
                            mainKey = VK_LEFT;
                            break;                        case "right":
                        case "sağ":
                            mainKey = VK_RIGHT;
                            break;
                        // Sayfa navigasyon tuşları
                        case "home":
                        case "ev":
                        case "başlangıç":
                            mainKey = VK_HOME;
                            break;
                        case "end":
                        case "son":
                        case "bitiş":
                            mainKey = VK_END;
                            break;
                        case "pageup":
                        case "pgup":
                        case "sayfa yukarı":
                            mainKey = VK_PGUP;
                            break;
                        case "pagedown":
                        case "pgdn":
                        case "sayfa aşağı":
                            mainKey = VK_PGDN;
                            break;
                        // Tek karakter tuşlar
                        default:
                            if (trimmedKey.Length == 1 && char.IsLetterOrDigit(trimmedKey[0]))
                            {
                                // Harf veya rakam
                                char ch = trimmedKey.ToUpperInvariant()[0];
                                mainKey = (byte)ch.GetHashCode();
                            }
                            else
                            {
                                Debug.WriteLine($"[HotkeySender] Bilinmeyen tuş: {trimmedKey}");
                            }
                            break;
                    }
                }
                
                // Modifier tuşları basılı tut
                foreach (byte modifier in modifiers)
                {
                    keybd_event(modifier, 0, 0, 0);
                }
                
                // Ana tuşu bas ve bırak (eğer belirtildiyse)
                if (mainKey.HasValue)
                {
                    keybd_event(mainKey.Value, 0, 0, 0);
                    keybd_event(mainKey.Value, 0, KEYEVENTF_KEYUP, 0);
                }
                  // Modifier tuşları ters sırada bırak
                for (int i = modifiers.Count - 1; i >= 0; i--)
                {
                    keybd_event(modifiers[i], 0, KEYEVENTF_KEYUP, 0);
                }
                
                // Kısa bir bekleme süresi ekleyerek tuşların işlenmesini bekle
                await Task.Delay(50);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeySender] Tuş kombinasyonu gönderme hatası: {ex.Message}");
                return false;
            }
        }
    }
}