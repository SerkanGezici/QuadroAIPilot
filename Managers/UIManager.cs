using Microsoft.UI.Dispatching;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// Manages UI state, WebView operations, and user feedback
    /// </summary>
    public class UIManager : IDisposable
    {
        #region Fields

        private readonly IWebViewManager _webViewManager;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _disposed = false;

        // UI State (thread-safe)
        private volatile bool _isProcessing = false;
        private volatile string _lastFeedbackMessage = string.Empty;
        private readonly object _stateLock = new object();
        
        // Cancellation support for delayed operations
        private CancellationTokenSource? _feedbackCancellationSource;

        #endregion

        #region Constructor

        public UIManager(IWebViewManager webViewManager, DispatcherQueue dispatcherQueue)
        {
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            
        }

        #endregion

        #region Properties

        public bool IsProcessing => _isProcessing;

        #endregion

        #region UI State Management

        /// <summary>
        /// Sets the processing state and updates UI accordingly
        /// </summary>
        public void SetProcessingState(bool isProcessing)
        {
            lock (_stateLock)
            {
                if (_isProcessing != isProcessing)
                {
                    _isProcessing = isProcessing;
                    UpdateUIProcessingIndicator();
                }
            }
        }

        /// <summary>
        /// Shows success feedback to user
        /// </summary>
        public async Task ShowSuccessFeedbackAsync(string message)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                lock (_stateLock)
                {
                    _lastFeedbackMessage = message;
                }
                
                
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendFeedback($"✅ {message}");
                });
                
                // Use cancellable delay to avoid blocking and allow cancellation
                _feedbackCancellationSource?.Cancel();
                _feedbackCancellationSource = new CancellationTokenSource();
                
                try
                {
                    await Task.Delay(2000, _feedbackCancellationSource.Token); // Show for 2 seconds
                }
                catch (OperationCanceledException)
                {
                    // Expected when feedback is cancelled for new feedback
                }
            }, "ShowSuccessFeedback");
        }

        /// <summary>
        /// Shows error feedback to user
        /// </summary>
        public async Task ShowErrorFeedbackAsync(string message)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                lock (_stateLock)
                {
                    _lastFeedbackMessage = message;
                }
                
                
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendOutput($"❌ {message}", true);
                });
                
                // Use cancellable delay for error feedback
                _feedbackCancellationSource?.Cancel();
                _feedbackCancellationSource = new CancellationTokenSource();
                
                try
                {
                    await Task.Delay(3000, _feedbackCancellationSource.Token); // Show errors longer
                }
                catch (OperationCanceledException)
                {
                    // Expected when feedback is cancelled for new feedback
                }
            }, "ShowErrorFeedback");
        }

        /// <summary>
        /// Shows informational message to user
        /// </summary>
        public async Task ShowInfoMessageAsync(string message)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                lock (_stateLock)
                {
                    _lastFeedbackMessage = message;
                }
                
                
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendOutput($"ℹ️ {message}");
                });
            }, "ShowInfoMessage");
        }

        /// <summary>
        /// Clears all content from the UI
        /// </summary>
        public async Task ClearContentAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                await _webViewManager.ClearContent();
                
                lock (_stateLock)
                {
                    _lastFeedbackMessage = string.Empty;
                }
                
            }, "ClearContent");
        }

        /// <summary>
        /// Forces content clear with JavaScript execution
        /// </summary>
        public async Task ClearContentForceAsync()
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                
                // WebViewManager'daki güçlü temizleme metodunu kullan
                await _webViewManager.ClearTextForce();
               
                lock (_stateLock)
                {
                    _lastFeedbackMessage = string.Empty;
                }
                
                
                // Use cancellable delay
                using var cts = new CancellationTokenSource();
                try
                {
                    await Task.Delay(100, cts.Token); // Brief pause for UI update
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation
                }
            }, "ClearContentForce");
        }

        #endregion

        #region WebView Operations

        /// <summary>
        /// Appends feedback message to WebView
        /// </summary>
        public void AppendFeedback(string message)
        {
            ErrorHandler.SafeExecute(() =>
            {
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendFeedback(message);
                });
            }, "AppendFeedback");
        }

        /// <summary>
        /// Appends output message to WebView
        /// </summary>
        public void AppendOutput(string message, bool isError = false)
        {
            ErrorHandler.SafeExecute(() =>
            {
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendOutput(message, isError);
                });
            }, "AppendOutput");
        }

        /// <summary>
        /// Executes JavaScript in WebView
        /// </summary>
        public async Task ExecuteScriptAsync(string script)
        {
            await ErrorHandler.SafeExecuteAsync(async () =>
            {
                await _webViewManager.ExecuteScript(script);
            }, "ExecuteScript");
        }

        #endregion

        #region Speech Integration

        /// <summary>
        /// Handles TTS speech generation for UI feedback
        /// </summary>
        public void HandleSpeechGenerated(string text)
        {
            ErrorHandler.SafeExecute(() =>
            {
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendFeedback(text);
                });
            }, "HandleSpeechGenerated");
        }

        /// <summary>
        /// Handles TTS output generation
        /// </summary>
        public void HandleOutputGenerated(string text)
        {
            ErrorHandler.SafeExecute(() =>
            {
                // Output içeriğini loglama - gereksiz
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.AppendOutput(text);
                });
            }, "HandleOutputGenerated");
        }


        #endregion

        #region Private Methods

        /// <summary>
        /// Updates UI processing indicator
        /// </summary>
        private void UpdateUIProcessingIndicator()
        {
            ErrorHandler.SafeExecute(() =>
            {
                var script = _isProcessing 
                    ? "document.body.classList.add('processing');"
                    : "document.body.classList.remove('processing');";
                    
                _dispatcherQueue.TryEnqueue(async () => 
                {
                    await _webViewManager.ExecuteScript(script);
                });
            }, "UpdateUIProcessingIndicator");
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
                    // Unsubscribe from events
                    
                    // Cancel any pending feedback operations
                    _feedbackCancellationSource?.Cancel();
                    _feedbackCancellationSource?.Dispose();
                    
                    lock (_stateLock)
                    {
                        _lastFeedbackMessage = string.Empty;
                        _isProcessing = false;
                    }
                    
                    _disposed = true;
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
    }
}