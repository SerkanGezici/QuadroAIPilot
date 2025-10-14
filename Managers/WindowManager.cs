using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Pencere yönetimi ve AppBar işlemleri için ayrılmış sınıf
    /// </summary>
    public class WindowManager : IDisposable
    {
        #region P/Invoke Declarations
        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        private const uint SPI_GETWORKAREA = 0x0030;
        private const uint SPI_SETWORKAREA = 0x002F;
        private const uint SPIF_SENDCHANGE = 0x0002;
        private const uint SPIF_UPDATEINIFILE = 0x0001;        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        private const int ABM_NEW = 0x0;
        private const int ABM_REMOVE = 0x1;
        private const int ABM_QUERYPOS = 0x2;
        private const int ABM_SETPOS = 0x3;

        private const uint ABE_RIGHT = 2;
        private const uint WM_APPBARNOTIFY = 0x0400 + 100; // WM_USER + 100        
        [DllImport("shell32.dll")]
        private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        #endregion

        #region Fields
        
        private IntPtr _hWnd;
        private RECT _originalWorkArea;
        private bool _isAppBarRegistered;
        private int _appBarWidth;
        private volatile bool _disposed = false;
        
        #endregion

        #region Constructor

        public WindowManager(IntPtr hWnd, int appBarWidth = 300) // Normal pencere boyutu
        {
            _hWnd = hWnd;
            _appBarWidth = appBarWidth;
        }

        #endregion

        #region Public Methods

        public void Initialize(IntPtr hWnd, int appBarWidth)
        {
            _hWnd = hWnd;
            _appBarWidth = appBarWidth;
            SaveCurrentWorkArea();
            RegisterAppBar(hWnd, appBarWidth);
        }

        public void Cleanup()
        {
            if (_isAppBarRegistered)
            {
                UnregisterAppBar();
            }
        }

        /// <summary>
        /// Pencereyi öne getirir ve aktif hale getirir
        /// </summary>
        public void BringToForeground()
        {
            if (_disposed) return;
            
            try
            {
                if (_hWnd != IntPtr.Zero)
                {
                    // Pencereyi göster ve öne getir
                    ShowWindow(_hWnd, SW_SHOW);
                    SetForegroundWindow(_hWnd);
                    BringWindowToTop(_hWnd);
                    SetActiveWindow(_hWnd);
                }
            }
            catch (Exception)
            {
                // Error handled silently
            }
        }

        #region Additional P/Invoke for Window Management
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOW = 5;
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);
        
        #endregion

        public void SaveCurrentWorkArea()
        {
            if (_disposed) return;
            
            try
            {
                SystemParametersInfo(SPI_GETWORKAREA, 0, ref _originalWorkArea, 0);
            }
            catch (Exception)
            {
                // Error handled silently
            }
        }

        public RECT? GetWindowRect(IntPtr hWnd)
        {
            if (_disposed) return null;
            
            try
            {
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    return rect;
                }
                return null;
            }
            catch (Exception)
            {
                // Error handled silently
                return null;
            }
        }
        
        public void RegisterAppBar(IntPtr hWnd, int appBarWidth)
        {
            if (_disposed) return;
            
            _hWnd = hWnd;
            _appBarWidth = appBarWidth;
            
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            abd.hWnd = hWnd;
            abd.uCallbackMessage = WM_APPBARNOTIFY;

            uint result = SHAppBarMessage(ABM_NEW, ref abd);
            if (result != 1)
            {
                // AppBar registration failed
                return;
            }

            abd.uEdge = ABE_RIGHT;
            abd.rc.Left = screenWidth - appBarWidth;
            abd.rc.Top = 0;
            abd.rc.Right = screenWidth;
            abd.rc.Bottom = screenHeight;

            SHAppBarMessage(ABM_QUERYPOS, ref abd);
            SHAppBarMessage(ABM_SETPOS, ref abd);            
            RECT workArea = new RECT();
            workArea.Left = 0;
            workArea.Top = 0;
            workArea.Right = screenWidth - appBarWidth;
            workArea.Bottom = screenHeight;

            SystemParametersInfo(SPI_SETWORKAREA, 0, ref workArea, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);

            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32
            {
                X = abd.rc.Left,
                Y = abd.rc.Top,
                Width = abd.rc.Right - abd.rc.Left,
                Height = abd.rc.Bottom - abd.rc.Top
            });

            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            _isAppBarRegistered = true;
        }

        public void UnregisterAppBar()
        {
            if (_disposed || !_isAppBarRegistered) return;
            
            try
            {
                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = Marshal.SizeOf(abd);
                abd.hWnd = _hWnd;
                SHAppBarMessage(ABM_REMOVE, ref abd);

                SystemParametersInfo(SPI_SETWORKAREA, 0, ref _originalWorkArea, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
                _isAppBarRegistered = false;
            }
            catch (Exception)
            {
                // Error handled silently
            }
        }

        public void RestoreWorkArea()
        {
            if (_disposed) return;
            
            try
            {
                SystemParametersInfo(SPI_SETWORKAREA, 0, ref _originalWorkArea, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
            }
            catch (Exception)
            {
                // Error handled silently
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // AppBar cleanup
                    if (_isAppBarRegistered)
                    {
                        UnregisterAppBar();
                    }

                    // Restore work area if we modified it
                    RestoreWorkArea();

                    _disposed = true;
                }
                catch (Exception)
                {
                    // Dispose error handled silently
                }
            }
        }

        #endregion
    }
}
