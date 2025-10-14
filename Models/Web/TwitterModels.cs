using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.Web
{
    /// <summary>
    /// Twitter/X trend related models
    /// </summary>
    public class TrendingTopic
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Url { get; set; }
        public int? TweetVolume { get; set; }
        public int Rank { get; set; }
        public DateTime TrendingAt { get; set; } = DateTime.Now;
        public string Location { get; set; }
        public TrendCategory Category { get; set; }
        public bool IsPromoted { get; set; }
        public string Source { get; set; } // Twitter, Google Trends, Ekşi, Reddit etc.
    }

    public class TrendAnalysis
    {
        public TrendingTopic Topic { get; set; }
        public string Summary { get; set; }
        public double SentimentScore { get; set; } // -1 to 1
        public string SentimentLabel { get; set; } // Positive, Negative, Neutral
        public TrendCategory Category { get; set; }
        public List<string> TopTweets { get; set; } = new();
        public List<string> RelatedHashtags { get; set; } = new();
        public int SampleCount { get; set; }
        public DateTime AnalysisTime { get; set; } = DateTime.Now;
        public Dictionary<string, int> KeywordFrequency { get; set; } = new();
    }

    public enum TrendCategory
    {
        Unknown,
        Politics,
        Sports,
        Entertainment,
        Technology,
        Business,
        Health,
        Education,
        Social,
        Breaking
    }

    public class TrendSearchResult
    {
        public string Query { get; set; }
        public List<TrendingTopic> Trends { get; set; } = new();
        public int TotalResults { get; set; }
        public DateTime SearchTime { get; set; } = DateTime.Now;
        public bool IsFromCache { get; set; }
    }

    public class TrendCacheEntry
    {
        public List<TrendingTopic> Trends { get; set; }
        public DateTime CachedAt { get; set; }
        public string Location { get; set; }
        public TimeSpan Age => DateTime.Now - CachedAt;
        public bool IsExpired(TimeSpan maxAge) => Age > maxAge;
    }
}