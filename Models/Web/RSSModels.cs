using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.Web
{
    /// <summary>
    /// RSS feed and item models
    /// </summary>
    public class RSSFeed
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public string Language { get; set; }
        public DateTime LastBuildDate { get; set; }
        public List<RSSItem> Items { get; set; } = new();
        public RSSCategory Category { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RSSItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public DateTime PublishDate { get; set; }
        public string Author { get; set; }
        public List<string> Categories { get; set; } = new();
        public string Guid { get; set; }
        public string ImageUrl { get; set; }
        public string Source { get; set; }
        public bool IsRead { get; set; }
        
        // Translation properties
        public bool IsTranslated { get; set; }
        public string OriginalLanguage { get; set; }
        public string OriginalTitle { get; set; }
        public string OriginalDescription { get; set; }
    }

    public enum RSSCategory
    {
        General,
        Technology,
        Economy,
        Sports,
        Entertainment,
        Politics,
        Health,
        Science,
        World,
        Local,
        International,
        Business
    }

    public class RSSSource
    {
        public string Name { get; set; }
        public string FeedUrl { get; set; }
        public RSSCategory Category { get; set; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastSuccessfulFetch { get; set; }
        public int FailureCount { get; set; }
        
        // Kullanıcı seçim özellikleri
        public bool IsUserSelected { get; set; } = false;
        public SourceType SourceType { get; set; } = SourceType.Turkish;
    }
    
    public enum SourceType
    {
        Turkish,
        English,
        International
    }
}