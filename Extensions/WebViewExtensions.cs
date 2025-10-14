using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace QuadroAIPilot.Extensions
{
    /// <summary>
    /// WebView2 extension metotları
    /// </summary>
    public static class WebViewExtensions
    {
        /// <summary>
        /// WebView'ın ekran koordinatlarını alır
        /// </summary>
        public static Rect GetBoundingRect(this WebView2 webView)
        {
            // WebView'ın transform'unu al
            var transform = webView.TransformToVisual(null);
            var point = transform.TransformPoint(new Point(0, 0));
            
            return new Rect(point.X, point.Y, webView.ActualWidth, webView.ActualHeight);
        }
    }
}
