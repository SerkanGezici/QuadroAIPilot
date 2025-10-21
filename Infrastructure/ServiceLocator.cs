using QuadroAIPilot.Interfaces;
using System;

namespace QuadroAIPilot.Infrastructure
{
    /// <summary>
    /// Temporary service locator for runtime-created services
    /// </summary>
    public static class ServiceLocator
    {
        private static IWebViewManager? _webViewManager;
        private static readonly object _lock = new object();

        /// <summary>
        /// Sets the WebViewManager instance
        /// </summary>
        public static void SetWebViewManager(IWebViewManager webViewManager)
        {
            lock (_lock)
            {
                _webViewManager = webViewManager;
                Services.LogService.LogInfo("[ServiceLocator] WebViewManager registered");
            }
        }

        /// <summary>
        /// Gets the WebViewManager instance
        /// </summary>
        public static IWebViewManager? GetWebViewManager()
        {
            lock (_lock)
            {
                if (_webViewManager == null)
                {
                    Services.LogService.LogWarning("[ServiceLocator] WebViewManager requested but not registered");
                }
                return _webViewManager;
            }
        }

        /// <summary>
        /// Clears all registered services
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _webViewManager = null;
                Services.LogService.LogInfo("[ServiceLocator] Services cleared");
            }
        }
    }
}