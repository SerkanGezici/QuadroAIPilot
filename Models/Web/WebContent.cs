using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.Web
{
    /// <summary>
    /// Generic web content model
    /// </summary>
    public class WebContent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public ContentType Type { get; set; }
        public DateTime PublishedDate { get; set; }
        public DateTime RetrievedDate { get; set; } = DateTime.Now;
        public string Language { get; set; } = "tr";
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public double? ConfidenceScore { get; set; }
        public bool IsFromCache { get; set; }
        
        // Translation properties
        public bool IsTranslated { get; set; }
        public string OriginalLanguage { get; set; }
        public string OriginalTitle { get; set; }
        public string OriginalContent { get; set; }
    }

    public enum ContentType
    {
        Unknown,
        Wikipedia,
        News,
        Weather,
        Finance,
        TwitterTrend,
        WebPage,
        RSS
    }

    public class ContentRequest
    {
        public string Query { get; set; }
        public ContentType? PreferredType { get; set; }
        public string Language { get; set; } = "tr";
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int MaxResults { get; set; } = 10;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class SummaryOptions
    {
        public int MaxSentences { get; set; } = 3;
        public int MaxTokens { get; set; } = 300;
        public bool UseCloudServices { get; set; } = true;
        public string Language { get; set; } = "tr";
        public SummaryType Type { get; set; } = SummaryType.Extractive;
    }

    public enum SummaryType
    {
        Extractive,
        Abstractive,
        Hybrid
    }
}