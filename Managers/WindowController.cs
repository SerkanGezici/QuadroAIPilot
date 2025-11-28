using Microsoft.UI.Dispatching;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Manages window positioning and basic window operations.
    /// Note: Eski Win+H dikte sistemine ait focus/click simülasyon kodları kaldırıldı.
    /// Web Speech API bu mekanizmalara ihtiyaç duymuyor.
    /// </summary>
    public class WindowController : IDisposable
    {
        #region Fields

        private readonly IntPtr _mainWindowHandle;
        private readonly WindowManager _windowManager;
        private readonly IDictationManager _dictationManager;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _disposed = false;

        #endregion

        #region Constructor

        public WindowController(
            IntPtr mainWindowHandle,
            WindowManager windowManager,
            IDictationManager dictationManager,
            IWebViewManager webViewManager,
            DispatcherQueue dispatcherQueue)
        {
            _mainWindowHandle = mainWindowHandle;
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _dictationManager = dictationManager ?? throw new ArgumentNullException(nameof(dictationManager));
            // webViewManager artık kullanılmıyor ama constructor signature'ı koruyoruz (breaking change önlemek için)
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        #endregion

        #region Dictation Control

        /// <summary>
        /// Starts dictation directly via Web Speech API (simplified - no focus manipulation)
        /// </summary>
        public async Task StartDictationWithFocusAsync(bool forceRestart = false)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_dictationManager.IsActive && !forceRestart)
                {
                    return;
                }

                // Web Speech API doğrudan başlatılıyor - focus/click manipülasyonuna gerek yok
                await _dictationManager.StartAsync();
            }, "StartDictationWithFocus");
        }

        /// <summary>
        /// Restarts dictation via Web Speech API (simplified)
        /// </summary>
        public async Task RestartDictationWithFocusAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_dictationManager.IsRestarting)
                {
                    return;
                }

                // Mevcut dikteyi durdur
                await StopDictationIfActiveAsync();

                // Kısa bekleme
                await Task.Delay(100);

                // Web Speech API'yi yeniden başlat
                await _dictationManager.StartAsync();
            }, "RestartDictationWithFocus");
        }

        /// <summary>
        /// Stops dictation if currently active
        /// </summary>
        public async Task StopDictationIfActiveAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                if (_dictationManager.IsActive)
                {
                    _dictationManager.Stop();
                    await Task.Delay(200);
                }
            }, "StopDictationIfActive");
        }

        #endregion

        #region Window Management

        /// <summary>
        /// Brings the main window to foreground
        /// </summary>
        public void BringWindowToForeground()
        {
            ErrorHandler.SafeExecute(() =>
            {
                _windowManager.BringToForeground();
            }, "BringWindowToForeground");
        }

        /// <summary>
        /// Gets current window rectangle
        /// </summary>
        public RECT GetWindowRect()
        {
            return ErrorHandler.SafeExecute(() =>
            {
                if (GetWindowRect(_mainWindowHandle, out RECT rect))
                {
                    return rect;
                }
                else
                {
                    return new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 };
                }
            }, "GetWindowRect", new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 });
        }

        #endregion

        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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
                _disposed = true;
            }
        }

        #endregion
    }
}
