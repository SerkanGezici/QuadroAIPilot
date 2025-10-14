using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.Web
{
    /// <summary>
    /// Web scraper configuration and models
    /// </summary>
    public class ScraperConfig
    {
        public string DriverPath { get; set; }
        public bool HeadlessMode { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public bool DisableImages { get; set; } = true;
        public bool DisableJavaScript { get; set; } = false;
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        public ProxyConfig Proxy { get; set; }
    }

    public class ProxyConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Type { get; set; } = "http"; // http, socks5
    }

    public class ScraperResult
    {
        public bool Success { get; set; }
        public string Html { get; set; }
        public string Url { get; set; }
        public DateTime ScrapedAt { get; set; }
        public int ResponseTimeMs { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string Error { get; set; }
    }







    public class ScraperSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public List<string> VisitedUrls { get; set; } = new();
        public Dictionary<string, string> Cookies { get; set; } = new();
        public int RequestCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public long TotalBytesDownloaded { get; set; }
    }
}