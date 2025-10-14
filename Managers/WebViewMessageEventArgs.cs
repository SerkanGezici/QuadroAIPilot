using System;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// WebView message event arguments
    /// </summary>
    public class WebViewMessageEventArgs : EventArgs
    {
        public string Action { get; set; }
        public object Data { get; set; }
        
        public WebViewMessageEventArgs(string action, object data = null)
        {
            Action = action;
            Data = data;
        }
    }
}
