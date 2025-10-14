using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;
using QuadroAIPilot.Services.WebServices.Providers;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services.WebServices
{
    /// <summary>
    /// Main orchestrator service for web content retrieval
    /// </summary>
    public class WebContentService : IWebContentService
    {
        private readonly List<IContentProvider> _providers;
        private readonly IContentCache _cache;
        private readonly IContentSummaryService _summaryService;
        private readonly IGoogleTranslateService _translateService;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Dictionary<string, DateTime> _providerLastAccess;

        public WebContentService(
            IContentCache cache = null,
            IContentSummaryService summaryService = null,
            IGoogleTranslateService translateService = null)
        {
            _cache = cache ?? new ContentCacheService();
            _summaryService = summaryService ?? new ContentSummaryService();
            _translateService = translateService;
            
            _providers = new List<IContentProvider>();
            _providerLastAccess = new Dictionary<string, DateTime>();
            _rateLimiter = new SemaphoreSlim(3, 3); // Max 3 concurrent requests

            InitializeProviders();
        }

        private void InitializeProviders()
        {
            // Add providers in priority order
            _providers.Add(new TwitterTrendsProvider());
            _providers.Add(new RSSProvider(translateService: _translateService));
            _providers.Add(new WikipediaProvider());
            _providers.Add(new WebScraperProvider()); // Selenium-based scraper as fallback
            
            // Sort by priority (lower number = higher priority)
            _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public async Task<WebContent> GetContentAsync(ContentRequest request)
        {
            try
            {
                Debug.WriteLine($"[WebContentService] Processing request: {request.Query}");

                // Check cache first (skip for news content)
                var cacheKey = GenerateCacheKey(request);
                
                // Haber içeriği için cache'i atla
                if (request.PreferredType != ContentType.News)
                {
                    var (found, cachedContent) = await _cache.TryGetAsync<WebContent>(cacheKey);
                    
                    if (found && cachedContent != null)
                    {
                        Debug.WriteLine($"[WebContentService] Cache hit for: {request.Query}");
                        cachedContent.IsFromCache = true;
                        return cachedContent;
                    }
                }
                else
                {
                    Debug.WriteLine($"[WebContentService] Cache skipped for news content: {request.Query}");
                }

                // Find suitable provider
                var provider = await FindBestProvider(request);
                if (provider == null)
                {
                    throw new Exception("Uygun içerik sağlayıcı bulunamadı");
                }

                // Rate limiting
                await _rateLimiter.WaitAsync();
                try
                {
                    // Check provider rate limit
                    await EnforceProviderRateLimit(provider.Name);

                    // Get content from provider
                    var content = await provider.GetContentAsync(request);
                    
                    if (content != null)
                    {
                        // Generate summary if requested
                        if (request.Parameters.GetValueOrDefault("summarize", false) is bool summarize && summarize)
                        {
                            content.Summary = await _summaryService.SummarizeAsync(
                                content.Content, 
                                new SummaryOptions { MaxSentences = 3 });
                        }

                        // Cache the result (skip for news content)
                        if (content.Type != ContentType.News)
                        {
                            await _cache.SetAsync(cacheKey, content);
                        }
                        
                        return content;
                    }
                }
                finally
                {
                    _rateLimiter.Release();
                }

                throw new Exception("İçerik alınamadı");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebContentService] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WebContent>> SearchAsync(ContentRequest request)
        {
            var results = new List<WebContent>();
            var tasks = new List<Task<WebContent>>();

            // Get content from multiple providers in parallel
            foreach (var provider in _providers)
            {
                if (await provider.CanHandleAsync(request))
                {
                    tasks.Add(GetContentSafelyAsync(provider, request));
                }
            }

            if (!tasks.Any())
            {
                return results;
            }

            // Wait for all tasks with timeout
            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks.Where(c => c != null));

            // Sort by relevance/date
            return results
                .OrderByDescending(c => c.PublishedDate)
                .Take(request.MaxResults)
                .ToList();
        }

        public async Task<TrendAnalysis> AnalyzeTrendAsync(string topic)
        {
            // Find Twitter provider
            var twitterProvider = _providers.OfType<ITrendProvider>().FirstOrDefault();
            if (twitterProvider == null)
            {
                throw new Exception("Trend analizi servisi bulunamadı");
            }

            // Create trend topic
            var trend = new TrendingTopic
            {
                Name = topic,
                DisplayName = topic,
                TrendingAt = DateTime.Now
            };

            return await twitterProvider.AnalyzeTrendAsync(trend);
        }

        public async Task<List<TrendingTopic>> GetTrendsAsync(string location = "turkey")
        {
            // Check cache
            var cacheKey = $"trends_{location}";
            var (found, cachedTrends) = await _cache.TryGetAsync<List<TrendingTopic>>(cacheKey);
            
            if (found && cachedTrends != null)
            {
                return cachedTrends;
            }

            // Find trend provider - artık ilk bulunanı kullanmıyor, tüm provider'ları deniyor
            var trendProviders = _providers.OfType<ITrendProvider>().ToList();
            if (!trendProviders.Any())
            {
                throw new Exception("Trend servisi bulunamadı");
            }

            List<TrendingTopic> trends = null;
            
            // Tüm trend provider'ları sırayla dene
            foreach (var provider in trendProviders)
            {
                try
                {
                    trends = await provider.GetTrendsAsync(location);
                    if (trends != null && trends.Any())
                    {
                        Debug.WriteLine($"[WebContentService] Got trends from {provider.GetType().Name}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebContentService] {provider.GetType().Name} failed: {ex.Message}");
                }
            }
            
            if (trends == null || !trends.Any())
            {
                throw new Exception("Hiçbir kaynaktan trend verisi alınamadı");
            }
            
            // Cache for 5 minutes (daha sık güncelleme için)
            await _cache.SetAsync(cacheKey, trends, TimeSpan.FromMinutes(5));
            
            return trends;
        }

        public async Task<WebContent> GetSummarizedContentAsync(ContentRequest request, SummaryOptions options = null)
        {
            // Get content first
            var content = await GetContentAsync(request);
            
            if (content == null)
                return null;

            // Apply summarization
            options ??= new SummaryOptions { MaxSentences = 3 };
            content.Summary = await _summaryService.SummarizeAsync(content.Content, options);
            
            return content;
        }

        public async Task<WebContent> SearchAsync(string query, ContentType? preferredType = null)
        {
            var request = new ContentRequest
            {
                Query = query,
                PreferredType = preferredType,
                MaxResults = 10
            };

            var results = await SearchAsync(request);
            return results.FirstOrDefault();
        }

        public async Task<bool> IsAvailableAsync()
        {
            // Check if at least one provider is available
            var availabilityTasks = _providers.Select(p => p.IsAvailableAsync());
            var results = await Task.WhenAll(availabilityTasks);
            
            return results.Any(r => r);
        }

        private async Task<IContentProvider> FindBestProvider(ContentRequest request)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    if (await provider.CanHandleAsync(request))
                    {
                        return provider;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebContentService] Provider {provider.Name} check failed: {ex.Message}");
                }
            }

            return null;
        }

        private async Task<WebContent> GetContentSafelyAsync(IContentProvider provider, ContentRequest request)
        {
            try
            {
                return await provider.GetContentAsync(request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebContentService] Provider {provider.Name} failed: {ex.Message}");
                return null;
            }
        }

        private string GenerateCacheKey(ContentRequest request)
        {
            var key = $"content_{request.Query}";
            
            if (request.PreferredType.HasValue)
                key += $"_{request.PreferredType}";
            
            if (request.DateFrom.HasValue)
                key += $"_{request.DateFrom:yyyyMMdd}";
            
            if (request.DateTo.HasValue)
                key += $"_{request.DateTo:yyyyMMdd}";

            return key.ToLowerInvariant();
        }

        private async Task EnforceProviderRateLimit(string providerName)
        {
            const int MinIntervalMs = 1000; // 1 second between requests per provider

            if (_providerLastAccess.TryGetValue(providerName, out var lastAccess))
            {
                var elapsed = DateTime.UtcNow - lastAccess;
                if (elapsed.TotalMilliseconds < MinIntervalMs)
                {
                    await Task.Delay(MinIntervalMs - (int)elapsed.TotalMilliseconds);
                }
            }

            _providerLastAccess[providerName] = DateTime.UtcNow;
        }
    }
}