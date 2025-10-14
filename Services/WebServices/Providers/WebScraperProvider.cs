using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using Polly;
using Polly.Retry;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Services.WebServices.Providers
{
    /// <summary>
    /// Web scraper provider using Selenium WebDriver
    /// </summary>
    public class WebScraperProvider : IContentProvider, ITrendProvider, IDisposable
    {
        private readonly ScraperConfig _config;
        private readonly AsyncRetryPolicy _retryPolicy;
        private IWebDriver _driver;
        private readonly object _driverLock = new object();
        private ScraperSession _currentSession;

        public string Name => "Web Scraper Provider";
        public int Priority => 3; // Lower priority than API-based providers

        public WebScraperProvider(ScraperConfig config = null)
        {
            _config = config ?? new ScraperConfig();
            _currentSession = new ScraperSession();

            // Configure retry policy
            _retryPolicy = Policy
                .Handle<WebDriverException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        Debug.WriteLine($"[WebScraperProvider] Retry {retryCount} after {timeSpan}s due to: {exception.Message}");
                    });
        }

        public async Task<bool> CanHandleAsync(ContentRequest request)
        {
            // Handle Twitter/X scraping when other methods fail
            if (request.PreferredType == ContentType.TwitterTrend && 
                request.Parameters.GetValueOrDefault("force_scraper", false) is bool force && force)
            {
                return true;
            }

            // Handle specific scraper requests
            var scraperKeywords = new[] { "x.com", "twitter.com", "scrape", "javascript" };
            return scraperKeywords.Any(k => request.Query.ToLowerInvariant().Contains(k));
        }

        public async Task<WebContent> GetContentAsync(ContentRequest request)
        {
            try
            {
                Debug.WriteLine($"[WebScraperProvider] Scraping content for: {request.Query}");

                // Initialize driver if needed
                await EnsureDriverInitialized();

                var url = ExtractUrlFromRequest(request);
                if (string.IsNullOrEmpty(url))
                {
                    throw new ArgumentException("URL bulunamadı");
                }

                var result = await ScrapeUrlAsync(url);
                
                if (!result.Success)
                {
                    throw new Exception($"Scraping failed: {result.Error}");
                }

                // Parse content based on site
                WebContent content = null;
                
                if (url.Contains("x.com") || url.Contains("twitter.com"))
                {
                    content = await ParseTwitterContent(result);
                }
                else
                {
                    content = ParseGenericContent(result);
                }

                return content;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Error: {ex.Message}");
                throw new Exception("Web scraping başarısız oldu", ex);
            }
        }

        public async Task<List<TrendingTopic>> GetTrendsAsync(string location = "turkey", int count = 10)
        {
            try
            {
                Debug.WriteLine($"[WebScraperProvider] Scraping Twitter/X trends for: {location}");

                await EnsureDriverInitialized();

                // Navigate to X.com explore page
                var exploreUrl = "https://x.com/explore/tabs/trending";
                
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await NavigateToUrl(exploreUrl);
                    
                    // Wait for trends to load
                    var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                    wait.Until(driver => driver.FindElements(By.CssSelector("[data-testid='trend']")).Any());

                    return ParseTwitterTrends();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Trend scraping error: {ex.Message}");
                throw new Exception("Twitter/X trend scraping başarısız oldu", ex);
            }
        }

        public Task<TrendSearchResult> SearchTrendAsync(string query)
        {
            throw new NotImplementedException("Trend search not implemented for scraper");
        }

        public Task<TrendAnalysis> AnalyzeTrendAsync(TrendingTopic trend)
        {
            throw new NotImplementedException("Trend analysis not implemented for scraper");
        }

        public Task<bool> IsAvailableAsync()
        {
            try
            {
                lock (_driverLock)
                {
                    return Task.FromResult(_driver != null && !string.IsNullOrEmpty(_driver.CurrentWindowHandle));
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private async Task EnsureDriverInitialized()
        {
            lock (_driverLock)
            {
                if (_driver != null)
                {
                    try
                    {
                        // Check if driver is still alive
                        var _ = _driver.CurrentWindowHandle;
                        return;
                    }
                    catch
                    {
                        // Driver is dead, recreate it
                        DisposeDriver();
                    }
                }

                InitializeDriver();
            }
        }

        private void InitializeDriver()
        {
            try
            {
                // Try Edge first (more likely to be installed on Windows)
                if (TryInitializeEdgeDriver())
                    return;

                // Fallback to Chrome
                if (TryInitializeChromeDriver())
                    return;

                throw new Exception("No suitable browser driver found");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Driver initialization failed: {ex.Message}");
                throw;
            }
        }

        private bool TryInitializeEdgeDriver()
        {
            try
            {
                var options = new EdgeOptions();
                ConfigureDriverOptions(options);
                
                _driver = new EdgeDriver(options);
                Debug.WriteLine("[WebScraperProvider] Edge driver initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Edge driver failed: {ex.Message}");
                return false;
            }
        }

        private bool TryInitializeChromeDriver()
        {
            try
            {
                var options = new ChromeOptions();
                ConfigureDriverOptions(options);
                
                _driver = new ChromeDriver(options);
                Debug.WriteLine("[WebScraperProvider] Chrome driver initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Chrome driver failed: {ex.Message}");
                return false;
            }
        }

        private void ConfigureDriverOptions(dynamic options)
        {
            if (_config.HeadlessMode)
            {
                options.AddArgument("--headless");
            }

            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument($"--user-agent={_config.UserAgent}");

            if (_config.DisableImages)
            {
                options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
            }

            // Disable automation detection
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("--disable-blink-features=AutomationControlled");

            // Add custom headers if any
            foreach (var header in _config.CustomHeaders)
            {
                options.AddArgument($"--header={header.Key}:{header.Value}");
            }
        }

        private async Task<ScraperResult> ScrapeUrlAsync(string url)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ScraperResult
            {
                Url = url,
                ScrapedAt = DateTime.Now
            };

            try
            {
                await NavigateToUrl(url);
                
                // Wait for dynamic content
                await Task.Delay(2000);
                
                // Get page source
                result.Html = _driver.PageSource;
                result.Success = true;
                
                // Update session stats
                _currentSession.VisitedUrls.Add(url);
                _currentSession.RequestCount++;
                _currentSession.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _currentSession.FailureCount++;
            }
            finally
            {
                stopwatch.Stop();
                result.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        private async Task NavigateToUrl(string url)
        {
            _driver.Navigate().GoToUrl(url);
            
            // Handle common popups/modals
            await HandleCommonPopups();
        }

        private async Task HandleCommonPopups()
        {
            try
            {
                // Handle cookie consent
                var cookieButtons = _driver.FindElements(By.XPath("//button[contains(text(), 'Accept') or contains(text(), 'Kabul')]"));
                if (cookieButtons.Any())
                {
                    cookieButtons.First().Click();
                    await Task.Delay(500);
                }

                // Handle Twitter/X login prompt
                var closeButtons = _driver.FindElements(By.CssSelector("[aria-label='Close']"));
                if (closeButtons.Any())
                {
                    closeButtons.First().Click();
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Popup handling error: {ex.Message}");
            }
        }

        private List<TrendingTopic> ParseTwitterTrends()
        {
            var trends = new List<TrendingTopic>();

            try
            {
                var trendElements = _driver.FindElements(By.CssSelector("[data-testid='trend']"));
                int rank = 1;

                foreach (var element in trendElements.Take(20))
                {
                    try
                    {
                        // Extract trend name
                        var nameElement = element.FindElement(By.CssSelector("span"));
                        var trendName = nameElement.Text;

                        if (string.IsNullOrWhiteSpace(trendName))
                            continue;

                        // Extract tweet count if available
                        int? tweetCount = null;
                        try
                        {
                            var countElement = element.FindElement(By.XPath(".//span[contains(text(), 'posts') or contains(text(), 'Tweet')]"));
                            if (countElement != null)
                            {
                                var countText = countElement.Text;
                                tweetCount = ParseTweetCount(countText);
                            }
                        }
                        catch { }

                        trends.Add(new TrendingTopic
                        {
                            Name = trendName,
                            DisplayName = trendName,
                            Rank = rank++,
                            TweetVolume = tweetCount,
                            TrendingAt = DateTime.Now,
                            Location = "Türkiye",
                            Source = "X.com (Scraped)"
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebScraperProvider] Error parsing trend element: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Error parsing trends: {ex.Message}");
            }

            return trends;
        }

        private async Task<WebContent> ParseTwitterContent(ScraperResult result)
        {
            // Use HtmlAgilityPack for parsing
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(result.Html);

            var content = new WebContent
            {
                Title = "Twitter/X İçeriği",
                Source = "X.com",
                Type = ContentType.TwitterTrend,
                SourceUrl = result.Url,
                PublishedDate = DateTime.Now
            };

            // Extract tweets or other content
            var tweetNodes = doc.DocumentNode.SelectNodes("//article[@data-testid='tweet']");
            if (tweetNodes != null && tweetNodes.Any())
            {
                var tweets = new List<string>();
                foreach (var tweet in tweetNodes.Take(10))
                {
                    var textNode = tweet.SelectSingleNode(".//div[@data-testid='tweetText']");
                    if (textNode != null)
                    {
                        tweets.Add(textNode.InnerText.Trim());
                    }
                }

                content.Content = string.Join("\n\n", tweets);
                content.Summary = $"{tweets.Count} tweet bulundu";
            }

            return content;
        }

        private WebContent ParseGenericContent(ScraperResult result)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(result.Html);

            var content = new WebContent
            {
                Title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "Başlıksız",
                Source = new Uri(result.Url).Host,
                Type = ContentType.WebPage,
                SourceUrl = result.Url,
                PublishedDate = DateTime.Now
            };

            // Extract main content
            var contentNode = doc.DocumentNode.SelectSingleNode("//main") ??
                             doc.DocumentNode.SelectSingleNode("//article") ??
                             doc.DocumentNode.SelectSingleNode("//div[@class='content']") ??
                             doc.DocumentNode.SelectSingleNode("//body");

            if (contentNode != null)
            {
                content.Content = contentNode.InnerText.Trim();
            }

            return content;
        }

        private string ExtractUrlFromRequest(ContentRequest request)
        {
            // Check if URL is directly provided
            if (request.Parameters.ContainsKey("url"))
            {
                return request.Parameters["url"].ToString();
            }

            // Try to extract URL from query
            var urlMatch = System.Text.RegularExpressions.Regex.Match(
                request.Query, 
                @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)"
            );

            if (urlMatch.Success)
            {
                return urlMatch.Value;
            }

            // Default URLs for known requests
            if (request.Query.ToLowerInvariant().Contains("twitter") || 
                request.Query.ToLowerInvariant().Contains("x.com"))
            {
                return "https://x.com/explore/tabs/trending";
            }

            return null;
        }

        private int? ParseTweetCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            // Remove non-numeric characters except K, M
            var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d\.KMkm]", "");
            
            if (cleanText.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(cleanText.TrimEnd('K', 'k'), out double kValue))
                {
                    return (int)(kValue * 1000);
                }
            }
            else if (cleanText.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(cleanText.TrimEnd('M', 'm'), out double mValue))
                {
                    return (int)(mValue * 1000000);
                }
            }
            else if (int.TryParse(cleanText, out int value))
            {
                return value;
            }

            return null;
        }

        private void DisposeDriver()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebScraperProvider] Error disposing driver: {ex.Message}");
            }
            finally
            {
                _driver = null;
            }
        }

        public void Dispose()
        {
            _currentSession.EndTime = DateTime.Now;
            DisposeDriver();
        }
    }
}