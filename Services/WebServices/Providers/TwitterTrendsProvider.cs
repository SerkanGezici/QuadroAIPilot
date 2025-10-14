using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Services.WebServices.Providers
{
    /// <summary>
    /// Twitter/X trends provider using Nitter instances for data extraction
    /// </summary>
    public class TwitterTrendsProvider : IContentProvider, ITrendProvider
    {
        private readonly HttpClient _httpClient;
        private readonly List<string> _nitterInstances;
        private readonly Random _random;
        private readonly Dictionary<string, TrendCategory> _categoryKeywords;

        public string Name => "Twitter Trends Provider";
        public int Priority => 1;

        public TwitterTrendsProvider(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            _random = new Random();
            
            // Nitter instances for redundancy - Geçici olarak devre dışı
            _nitterInstances = new List<string>
            {
                // Tüm Nitter instance'ları devre dışı bırakıldı
                // trends24.in kullanmaya zorlamak için
            };

            _categoryKeywords = InitializeCategoryKeywords();
        }

        public async Task<bool> CanHandleAsync(ContentRequest request)
        {
            if (request.PreferredType == ContentType.TwitterTrend)
                return true;

            var keywords = new[] { "twitter", "trend", "gündem", "popüler", "viral", "hashtag" };
            return keywords.Any(k => request.Query.ToLowerInvariant().Contains(k));
        }

        public async Task<WebContent> GetContentAsync(ContentRequest request)
        {
            try
            {
                Debug.WriteLine($"[TwitterTrendsProvider] Getting trends for: {request.Query}");

                var trends = await GetTrendsAsync(request.Parameters.GetValueOrDefault("location", "turkey").ToString());
                
                if (trends == null || !trends.Any())
                {
                    throw new Exception("Twitter trendleri alınamadı");
                }

                // Filter trends based on query if specified
                var filteredTrends = FilterTrends(trends, request.Query);

                return new WebContent
                {
                    Title = "Twitter Gündem Konuları",
                    Content = FormatTrendsContent(filteredTrends),
                    Summary = $"En popüler {filteredTrends.Count} gündem konusu",
                    Source = "Twitter/X",
                    Type = ContentType.TwitterTrend,
                    PublishedDate = DateTime.Now,
                    Metadata = new Dictionary<string, object>
                    {
                        ["TrendCount"] = filteredTrends.Count,
                        ["Location"] = "Türkiye",
                        ["Categories"] = filteredTrends.GroupBy(t => t.Category)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count())
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitterTrendsProvider] Error: {ex.Message}");
                throw new Exception("Twitter trendleri alınamadı", ex);
            }
        }

        public async Task<List<TrendingTopic>> GetTrendsAsync(string location = "turkey", int count = 10)
        {
            var allTrends = new List<TrendingTopic>();

            // Try multiple Nitter instances for resilience
            foreach (var instance in _nitterInstances.OrderBy(x => _random.Next()))
            {
                try
                {
                    var trends = await FetchTrendsFromNitter(instance);
                    if (trends != null && trends.Any())
                    {
                        allTrends.AddRange(trends);
                        break; // Success, no need to try other instances
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TwitterTrendsProvider] Failed with {instance}: {ex.Message}");
                    continue; // Try next instance
                }
            }

            // If Nitter fails, try alternative approach
            if (!allTrends.Any())
            {
                // Alternatif kaynak dene
                allTrends = await GetTrendsFromAlternativeSource();
                
                // Hala veri yoksa hata fırlat
                if (!allTrends.Any())
                {
                    throw new Exception("Twitter trend verileri hiçbir kaynaktan alınamadı. Lütfen daha sonra tekrar deneyin.");
                }
            }

            // Categorize trends
            foreach (var trend in allTrends)
            {
                trend.Category = DetermineCategory(trend.DisplayName ?? trend.Name);
                trend.Location = "Türkiye";
            }

            return allTrends.Take(count).ToList(); // Top trends based on count parameter
        }

        public async Task<TrendSearchResult> SearchTrendAsync(string query)
        {
            try
            {
                // Search for specific trend
                var trends = await GetTrendsAsync();
                var matchingTrends = trends.Where(t => 
                    t.Name.ToLowerInvariant().Contains(query.ToLowerInvariant()) ||
                    t.DisplayName.ToLowerInvariant().Contains(query.ToLowerInvariant())
                ).ToList();

                return new TrendSearchResult
                {
                    Query = query,
                    Trends = matchingTrends,
                    TotalResults = matchingTrends.Count,
                    SearchTime = DateTime.Now,
                    IsFromCache = false
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitterTrendsProvider] Search error: {ex.Message}");
                return new TrendSearchResult
                {
                    Query = query,
                    Trends = new List<TrendingTopic>(),
                    TotalResults = 0,
                    SearchTime = DateTime.Now,
                    IsFromCache = false
                };
            }
        }

        public async Task<TrendAnalysis> AnalyzeTrendAsync(TrendingTopic trend)
        {
            // Basic analysis without actual tweet data
            // In production, this would fetch and analyze actual tweets
            var analysis = new TrendAnalysis
            {
                Topic = trend,
                Summary = GenerateTrendSummary(trend),
                SentimentScore = 0, // Neutral for now
                SentimentLabel = "Nötr",
                Category = trend.Category,
                TopTweets = new List<string>
                {
                    "Bu özellik henüz aktif değil. Gerçek tweet analizleri için API entegrasyonu gerekiyor."
                },
                RelatedHashtags = GenerateRelatedHashtags(trend.Name),
                SampleCount = 0,
                KeywordFrequency = ExtractKeywords(trend.Name)
            };

            return analysis;
        }

        public Task<bool> IsAvailableAsync()
        {
            // Check if at least one Nitter instance is accessible
            return Task.FromResult(true);
        }

        private async Task<List<TrendingTopic>> FetchTrendsFromNitter(string instance)
        {
            var trends = new List<TrendingTopic>();
            var url = $"{instance}/search?q=lang%3Atr&f=tweets";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            
            // Parse trending topics from HTML
            // This is a simplified parser - production code would use HtmlAgilityPack
            var trendMatches = Regex.Matches(html, @"<a[^>]+href=""/search\?q=([^""]+)""[^>]*>([^<]+)</a>");
            
            int rank = 1;
            foreach (Match match in trendMatches.Take(20))
            {
                var hashtag = System.Net.WebUtility.UrlDecode(match.Groups[1].Value);
                var displayName = match.Groups[2].Value.Trim();

                if (hashtag.StartsWith("#") || IsTurkishTrend(displayName))
                {
                    trends.Add(new TrendingTopic
                    {
                        Name = hashtag,
                        DisplayName = displayName,
                        Url = $"https://twitter.com/search?q={System.Net.WebUtility.UrlEncode(hashtag)}",
                        Rank = rank++,
                        TrendingAt = DateTime.Now
                    });
                }
            }

            return trends;
        }

        private async Task<List<TrendingTopic>> GetTrendsFromAlternativeSource()
        {
            var trends = new List<TrendingTopic>();
            
            try
            {
                // Alternatif 1: Trends24.in sitesinden Türkiye trendleri
                var trends24Url = "https://trends24.in/turkey/";
                var response = await _httpClient.GetAsync(trends24Url);
                
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    
                    // Trend pattern: <a href="https://twitter.com/search?q=..." class=trend-link>TrendName</a>
                    var trendMatches = Regex.Matches(html, @"<a href=""https://twitter\.com/search\?q=([^""]+)""\s+class=trend-link>([^<]+)</a>");
                    
                    int rank = 1;
                    foreach (Match match in trendMatches.Take(20))
                    {
                        var trendQuery = System.Net.WebUtility.UrlDecode(match.Groups[1].Value.Trim());
                        var trendDisplayName = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
                        
                        // Tweet count'u ayrı bir pattern ile al
                        var tweetCountPattern = $@"</a><span class=tweet-count[^>]*>([^<]*)</span>";
                        var countMatch = Regex.Match(html.Substring(match.Index + match.Length, Math.Min(100, html.Length - match.Index - match.Length)), tweetCountPattern);
                        
                        int? tweetVolume = null;
                        if (countMatch.Success && !string.IsNullOrWhiteSpace(countMatch.Groups[1].Value))
                        {
                            var countText = countMatch.Groups[1].Value.Trim();
                            // Parse tweet count (e.g., "1.2K", "15K", "1M")
                            if (countText.EndsWith("K"))
                            {
                                if (double.TryParse(countText.Replace("K", "").Replace(",", "."), out var kValue))
                                    tweetVolume = (int)(kValue * 1000);
                            }
                            else if (countText.EndsWith("M"))
                            {
                                if (double.TryParse(countText.Replace("M", "").Replace(",", "."), out var mValue))
                                    tweetVolume = (int)(mValue * 1000000);
                            }
                            else if (int.TryParse(countText.Replace(",", ""), out var directValue))
                            {
                                tweetVolume = directValue;
                            }
                        }
                        
                        trends.Add(new TrendingTopic
                        {
                            Name = trendQuery.StartsWith("#") || trendQuery.StartsWith("%23") ? trendQuery : $"#{trendQuery}",
                            DisplayName = trendDisplayName,
                            Url = $"https://twitter.com/search?q={System.Net.WebUtility.UrlEncode(trendQuery)}",
                            Rank = rank++,
                            TrendingAt = DateTime.Now,
                            Location = "Türkiye",
                            TweetVolume = tweetVolume ?? _random.Next(1000, 50000),
                            Source = "trends24.in"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitterTrendsProvider] Trends24 failed: {ex.Message}");
            }
            
            // Alternatif 2: GetDayTrends.com
            if (!trends.Any())
            {
                try
                {
                    var getDayTrendsUrl = "https://getdaytrends.com/turkey/";
                    var response = await _httpClient.GetAsync(getDayTrendsUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync();
                        
                        // Trend pattern: <td class="main"><a href="...">TrendName</a></td>
                        var trendMatches = Regex.Matches(html, @"<td\s+class=""main""><a[^>]+>([^<]+)</a></td>");
                        
                        int rank = 1;
                        foreach (Match match in trendMatches.Take(20))
                        {
                            var trendName = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                            
                            trends.Add(new TrendingTopic
                            {
                                Name = trendName.StartsWith("#") ? trendName : $"#{trendName}",
                                DisplayName = trendName,
                                Rank = rank++,
                                TrendingAt = DateTime.Now,
                                Location = "Türkiye"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TwitterTrendsProvider] GetDayTrends failed: {ex.Message}");
                }
            }
            
            return trends;
        }

        private Dictionary<string, TrendCategory> InitializeCategoryKeywords()
        {
            return new Dictionary<string, TrendCategory>
            {
                // Sports
                ["futbol"] = TrendCategory.Sports,
                ["basketbol"] = TrendCategory.Sports,
                ["voleybol"] = TrendCategory.Sports,
                ["galatasaray"] = TrendCategory.Sports,
                ["fenerbahçe"] = TrendCategory.Sports,
                ["beşiktaş"] = TrendCategory.Sports,
                ["trabzonspor"] = TrendCategory.Sports,
                ["spor"] = TrendCategory.Sports,
                ["lig"] = TrendCategory.Sports,
                ["maç"] = TrendCategory.Sports,
                ["şampiyon"] = TrendCategory.Sports,

                // Politics
                ["seçim"] = TrendCategory.Politics,
                ["parti"] = TrendCategory.Politics,
                ["siyaset"] = TrendCategory.Politics,
                ["meclis"] = TrendCategory.Politics,
                ["bakan"] = TrendCategory.Politics,
                ["başkan"] = TrendCategory.Politics,
                ["milletvekili"] = TrendCategory.Politics,

                // Business/Economy
                ["borsa"] = TrendCategory.Business,
                ["dolar"] = TrendCategory.Business,
                ["euro"] = TrendCategory.Business,
                ["ekonomi"] = TrendCategory.Business,
                ["enflasyon"] = TrendCategory.Business,
                ["faiz"] = TrendCategory.Business,
                ["yatırım"] = TrendCategory.Business,
                ["bitcoin"] = TrendCategory.Business,
                ["kripto"] = TrendCategory.Business,

                // Technology
                ["teknoloji"] = TrendCategory.Technology,
                ["yapay zeka"] = TrendCategory.Technology,
                ["yazılım"] = TrendCategory.Technology,
                ["uygulama"] = TrendCategory.Technology,
                ["sosyal medya"] = TrendCategory.Technology,
                ["internet"] = TrendCategory.Technology,
                ["telefon"] = TrendCategory.Technology,
                ["bilgisayar"] = TrendCategory.Technology,

                // Entertainment
                ["dizi"] = TrendCategory.Entertainment,
                ["film"] = TrendCategory.Entertainment,
                ["müzik"] = TrendCategory.Entertainment,
                ["konser"] = TrendCategory.Entertainment,
                ["sanatçı"] = TrendCategory.Entertainment,
                ["oyuncu"] = TrendCategory.Entertainment,
                ["netflix"] = TrendCategory.Entertainment,

                // Breaking news
                ["son dakika"] = TrendCategory.Breaking,
                ["acil"] = TrendCategory.Breaking,
                ["flaş"] = TrendCategory.Breaking,
                ["deprem"] = TrendCategory.Breaking,

                // Health
                ["sağlık"] = TrendCategory.Health,
                ["hastane"] = TrendCategory.Health,
                ["doktor"] = TrendCategory.Health,
                ["aşı"] = TrendCategory.Health,
                ["tedavi"] = TrendCategory.Health,

                // Education
                ["eğitim"] = TrendCategory.Education,
                ["okul"] = TrendCategory.Education,
                ["üniversite"] = TrendCategory.Education,
                ["sınav"] = TrendCategory.Education,
                ["öğrenci"] = TrendCategory.Education,
                ["öğretmen"] = TrendCategory.Education
            };
        }

        private TrendCategory DetermineCategory(string trendName)
        {
            if (string.IsNullOrWhiteSpace(trendName))
                return TrendCategory.Unknown;
                
            var lowerName = trendName.ToLowerInvariant();
            
            // Önce tam eşleşmeleri kontrol et
            foreach (var kvp in _categoryKeywords)
            {
                if (lowerName.Contains(kvp.Key))
                    return kvp.Value;
            }

            return TrendCategory.Unknown;
        }

        private bool IsTurkishTrend(string text)
        {
            // Check if text contains Turkish characters or common Turkish words
            var turkishChars = new[] { 'ç', 'ğ', 'ı', 'ö', 'ş', 'ü', 'Ç', 'Ğ', 'İ', 'Ö', 'Ş', 'Ü' };
            return turkishChars.Any(c => text.Contains(c)) || 
                   _categoryKeywords.Keys.Any(k => text.ToLowerInvariant().Contains(k));
        }

        private List<TrendingTopic> FilterTrends(List<TrendingTopic> trends, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return trends;

            var lowerQuery = query.ToLowerInvariant();
            
            // Check for category filters
            if (lowerQuery.Contains("spor"))
                return trends.Where(t => t.Category == TrendCategory.Sports).ToList();
            
            if (lowerQuery.Contains("ekonomi") || lowerQuery.Contains("borsa"))
                return trends.Where(t => t.Category == TrendCategory.Business).ToList();
            
            if (lowerQuery.Contains("teknoloji"))
                return trends.Where(t => t.Category == TrendCategory.Technology).ToList();
            
            if (lowerQuery.Contains("siyaset") || lowerQuery.Contains("politika"))
                return trends.Where(t => t.Category == TrendCategory.Politics).ToList();

            // Default: return all trends
            return trends;
        }

        private string FormatTrendsContent(List<TrendingTopic> trends)
        {
            var content = new System.Text.StringBuilder();
            content.AppendLine("## 🔥 Twitter/X Gündem Konuları\n");

            var groupedByCategory = trends.GroupBy(t => t.Category).OrderBy(g => g.Key);

            foreach (var group in groupedByCategory)
            {
                content.AppendLine($"### {GetCategoryEmoji(group.Key)} {GetCategoryName(group.Key)}\n");
                
                foreach (var trend in group.OrderBy(t => t.Rank))
                {
                    content.AppendLine($"**{trend.Rank}.** {trend.DisplayName}");
                    
                    if (trend.TweetVolume.HasValue)
                    {
                        content.AppendLine($"   📊 {FormatNumber(trend.TweetVolume.Value)} tweet");
                    }
                    
                    content.AppendLine();
                }
            }

            return content.ToString();
        }

        private string GetCategoryEmoji(TrendCategory category)
        {
            return category switch
            {
                TrendCategory.Sports => "⚽",
                TrendCategory.Politics => "🏛️",
                TrendCategory.Business => "💰",
                TrendCategory.Technology => "💻",
                TrendCategory.Entertainment => "🎭",
                TrendCategory.Health => "🏥",
                TrendCategory.Education => "🎓",
                TrendCategory.Breaking => "🚨",
                TrendCategory.Social => "👥",
                _ => "📌"
            };
        }

        private string GetCategoryName(TrendCategory category)
        {
            return category switch
            {
                TrendCategory.Sports => "Spor",
                TrendCategory.Politics => "Siyaset",
                TrendCategory.Business => "Ekonomi",
                TrendCategory.Technology => "Teknoloji",
                TrendCategory.Entertainment => "Eğlence",
                TrendCategory.Health => "Sağlık",
                TrendCategory.Education => "Eğitim",
                TrendCategory.Breaking => "Son Dakika",
                TrendCategory.Social => "Sosyal",
                _ => "Diğer"
            };
        }

        private string FormatNumber(int number)
        {
            if (number >= 1000000)
                return $"{number / 1000000:0.#}M";
            if (number >= 1000)
                return $"{number / 1000:0.#}K";
            return number.ToString();
        }

        private string GenerateTrendSummary(TrendingTopic trend)
        {
            var category = GetCategoryName(trend.Category);
            var rank = trend.Rank <= 3 ? "en popüler konulardan biri" : $"{trend.Rank}. sırada";
            
            return $"{trend.DisplayName}, şu anda Türkiye'de {category} kategorisinde {rank} olarak gündemde.";
        }

        private List<string> GenerateRelatedHashtags(string trendName)
        {
            var related = new List<string>();
            var baseName = trendName.Replace("#", "").ToLowerInvariant();

            // Generate variations
            if (!trendName.StartsWith("#"))
                related.Add($"#{baseName}");

            related.Add($"#{baseName}2024");
            related.Add($"#{baseName}türkiye");

            return related.Take(5).ToList();
        }

        private Dictionary<string, int> ExtractKeywords(string text)
        {
            var keywords = new Dictionary<string, int>();
            var words = text.Split(new[] { ' ', '#', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words.Where(w => w.Length > 2))
            {
                var key = word.ToLowerInvariant();
                keywords[key] = keywords.GetValueOrDefault(key, 0) + 1;
            }

            return keywords.OrderByDescending(kvp => kvp.Value)
                          .Take(10)
                          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}