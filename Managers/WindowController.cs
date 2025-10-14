using Microsoft.UI.Dispatching;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Manages window positioning, focus operations, and mouse simulation
    /// </summary>
    public class WindowController : IDisposable
    {
        #region Fields

        private readonly IntPtr _mainWindowHandle;
        private readonly WindowManager _windowManager;
        private readonly IDictationManager _dictationManager;
        private readonly IWebViewManager _webViewManager;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _disposed = false;

        // Textarea coordinate system (thread-safe)
        private double _textareaScreenLeft = -1;
        private double _textareaScreenTop = -1;
        private double _textareaScreenWidth = -1;
        private double _textareaScreenHeight = -1;
        private readonly object _coordinateLock = new object();

        // P/Invoke constants
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MAX_DIKTE_ATTEMPTS = 3;

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
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        #endregion

        #region Properties

        public bool HasValidTextareaCoordinates
        {
            get
            {
                lock (_coordinateLock)
                {
                    return _textareaScreenLeft >= 0 && _textareaScreenTop >= 0 &&
                           _textareaScreenWidth > 0 && _textareaScreenHeight > 0;
                }
            }
        }

        #endregion

        #region Textarea Coordinate Management

        /// <summary>
        /// Updates textarea coordinates for accurate focus operations
        /// </summary>
        public void UpdateTextareaCoordinates(double left, double top, double width, double height)
        {
            ErrorHandler.SafeExecute(() =>
            {
                lock (_coordinateLock)
                {
                    _textareaScreenLeft = left;
                    _textareaScreenTop = top;
                    _textareaScreenWidth = width;
                    _textareaScreenHeight = height;
                }

                // Textarea coordinates updated: L={left}, T={top}, W={width}, H={height}
            }, "UpdateTextareaCoordinates");
        }

        #endregion

        #region Focus Operations

        /// <summary>
        /// Focuses WebView textarea with multiple strategies
        /// </summary>
        public async Task FocusWebViewTextAreaAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                // Focus strategy: Window focus + JS focus

                // 1. Bring window to foreground
                _windowManager.BringToForeground();
                await Task.Delay(200);

                // 2. JavaScript focus on textarea
                await _webViewManager.ExecuteScript(@"
                    var textarea = document.getElementById('txtCikti');
                    if (textarea) {
                        textarea.click();
                        textarea.focus();
                        textarea.setSelectionRange(textarea.value.length, textarea.value.length);
                        console.log('[JS-FOCUS] Textarea focused');
                        
                        setTimeout(function() {
                            textarea.focus();
                        }, 100);
                    } else {
                        console.error('[JS-FOCUS] Textarea not found!');
                    }
                ");

                // Textarea focused successfully
            }, "FocusWebViewTextArea");
        }

        /// <summary>
        /// Simulates mouse click on WebView for reliable focus
        /// </summary>
        public async Task SimulateClickOnWebViewAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                // SimulateClickOnWebView started

                var (clickX, clickY) = CalculateClickCoordinates();
                
                // Safely execute P/Invoke operations
                try
                {
                    if (!SetCursorPos(clickX, clickY))
                    {
                        // SetCursorPos failed
                        return;
                    }
                    
                    await Task.Delay(100);

                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    await Task.Delay(50);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }
                catch (Exception)
                {
                    // P/Invoke mouse operation failed
                    return;
                }

                // Mouse click completed at ({clickX}, {clickY})
            }, "SimulateClickOnWebView");
        }

        #endregion

        #region Dictation Control

        /// <summary>
        /// Starts dictation with focus and restart handling
        /// </summary>
        public async Task StartDictationWithFocusAsync(bool forceRestart = false)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                // StartDictationWithFocus called, forceRestart: {forceRestart}

                if (_dictationManager.IsActive && !forceRestart)
                {
                    // Dictation already active, exiting
                    return;
                }

                // Clear text output
                await ClearTextOutputAsync();

                // Focus textarea
                await FocusWebViewTextAreaAsync();
                await Task.Delay(300);

                // Start dictation with retry logic
                bool dikteStarted = await StartDictationWithRetriesAsync();

                if (!dikteStarted)
                {
                    // Failed to start dictation after retries
                }
            }, "StartDictationWithFocus");
        }

        /// <summary>
        /// Restarts dictation with improved focus handling
        /// </summary>
        public async Task RestartDictationWithFocusAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                // RestartDictationWithFocus started (simplified approach)

                if (_dictationManager.IsRestarting)
                {
                    // Already restarting, exiting
                    return;
                }

                // 1. Stop current dictation and reset state
                await StopDictationIfActiveAsync();
                // Current dictation stopped

                // 2. Focus with mouse click (simplified approach)
                await SimulateClickOnWebViewAsync();
                // Mouse click performed

                // 3. Short delay then send Win+H (accelerated approach)
                await Task.Delay(100);

                // Win+H sending loop starting (simplified)
                bool dikteStarted = await StartDictationWithRetriesAsync();

                if (!dikteStarted)
                {
                    // Win+H could not be sent (simplified approach)
                }

                // RestartDictationWithFocus completed
            }, "RestartDictationWithFocus");
        }

        /// <summary>
        /// Stops dictation if currently active
        /// </summary>
        public async Task StopDictationIfActiveAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                bool isManagerActive = _dictationManager.IsActive;
                
                // StopDictationIfActive - ManagerActive: {isManagerActive}

                // Stop any active dictation
                if (isManagerActive)
                {
                    _dictationManager.Stop();
                    // Dictation stopped
                    await Task.Delay(500);
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
                // Window brought to foreground
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
                    // Failed to get window rect, using default
                    return new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 };
                }
            }, "GetWindowRect", new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates optimal click coordinates based on textarea position
        /// </summary>
        private (int x, int y) CalculateClickCoordinates()
        {
            int clickX, clickY;

            lock (_coordinateLock)
            {
                bool hasValidCoords = _textareaScreenLeft >= 0 && _textareaScreenTop >= 0 &&
                                     _textareaScreenWidth > 0 && _textareaScreenHeight > 0;

                if (hasValidCoords)
                {
                    clickX = (int)(_textareaScreenLeft + _textareaScreenWidth / 2);
                    clickY = (int)(_textareaScreenTop + _textareaScreenHeight / 2);
                    // Using textarea coordinates: X={clickX}, Y={clickY}
                }
                else
                {
                    var rect = GetWindowRect();
                    clickX = rect.Left + (rect.Right - rect.Left) / 2;
                    clickY = rect.Top + 350;
                    // Using fallback coordinates: X={clickX}, Y={clickY}
                }
            }

            return (clickX, clickY);
        }

        /// <summary>
        /// Starts dictation with retry logic
        /// </summary>
        private async Task<bool> StartDictationWithRetriesAsync()
        {
            // Starting Win+H sending loop
            bool dikteStarted = false;

            for (int i = 0; i < MAX_DIKTE_ATTEMPTS && !dikteStarted; i++)
            {
                // Win+H attempt {i + 1}/{MAX_DIKTE_ATTEMPTS}

                // Start Web Speech API dictation directly
                await _dictationManager.StartAsync();
                dikteStarted = true;
                await Task.Delay(200);
            }

            return dikteStarted;
        }

        /// <summary>
        /// Clears text output in WebView
        /// </summary>
        private async Task ClearTextOutputAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                _webViewManager.UpdateText("");

                await _webViewManager.ExecuteScript(@"
                   document.getElementById('txtCikti').value = '';
                   checkButtonStates();
               ");
                
                // Text output cleared
                await Task.Delay(100);
            }, "ClearTextOutput");
        }

        #endregion

        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

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
                try
                {
                    // Thread-safe coordinate reset
                    lock (_coordinateLock)
                    {
                        _textareaScreenLeft = -1;
                        _textareaScreenTop = -1;
                        _textareaScreenWidth = -1;
                        _textareaScreenHeight = -1;
                    }
                    
                    _disposed = true;
                    // Disposed
                }
                catch (Exception)
                {
                    // Dispose error
                }
            }
        }

        #endregion
    }
}