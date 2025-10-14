using System;
using System.Runtime.InteropServices;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Basit kopyala-yapıştır gibi Ctrl kombinasyonlarını sanal olarak basar
    /// </summary>
    public static class VirtualKeyPresses
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x1;
        private const int KEYEVENTF_KEYUP = 0x2;
        private const byte VK_CONTROL = 0x11;
        private const byte C_KEY = 0x43;
        private const byte V_KEY = 0x56;

        public static void Copy() => PressCtrlCombo(C_KEY);
        public static void Paste() => PressCtrlCombo(V_KEY);

        private static void PressCtrlCombo(byte key)
        {
            keybd_event(VK_CONTROL, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, 0);

            keybd_event(key, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
        }
    }
}