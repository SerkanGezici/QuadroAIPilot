using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;
using QuadroAIPilot.Services;
using QuadroAIPilot.Infrastructure;

namespace QuadroAIPilot.Services.WebServices.Providers
{
    /// <summary>
    /// RSS feed provider for news and blog content
    /// </summary>
    public class RSSProvider : IContentProvider
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<RSSCategory, List<RSSSource>> _feedSources;
        private readonly Dictionary<string, SourceHealthInfo> _sourceHealth;
        private readonly object _healthLock = new object();
        private readonly IGoogleTranslateService _translateService;
        private readonly ConfigurationService _configService;

        public string Name => "RSS Provider";
        public int Priority => 2;

        public RSSProvider(HttpClient httpClient = null, IGoogleTranslateService translateService = null)
        {
            _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
            {
                // DÜZELTME: Connection pooling ve socket lifecycle optimizasyonu
                PooledConnectionLifetime = TimeSpan.FromMinutes(5), // 5 dakikada bir connection yenilenir
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2), // 2 dakika idle sonrası kapat
                MaxConnectionsPerServer = 10, // Sunucu başına max 10 connection
                EnableMultipleHttp2Connections = true, // HTTP/2 için çoklu bağlantı
                KeepAlivePingDelay = TimeSpan.FromSeconds(30), // 30 saniyede bir keepalive ping
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10), // Ping timeout 10 saniye
                ConnectTimeout = TimeSpan.FromSeconds(10) // Bağlantı timeout 10 saniye
            });

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QuadroAIPilot/1.0");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive"); // Keep-alive header ekle
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Global timeout 30 saniye

            _feedSources = InitializeFeedSources();
            _sourceHealth = new Dictionary<string, SourceHealthInfo>();
            _translateService = translateService;
            _configService = ServiceContainer.GetOptionalService<ConfigurationService>();
        }

        public async Task<bool> CanHandleAsync(ContentRequest request)
        {
            if (request.PreferredType == ContentType.RSS || request.PreferredType == ContentType.News)
                return true;

            var keywords = new[] { "haber", "haberler", "gündem", "son dakika", "news" };
            return keywords.Any(k => request.Query.ToLowerInvariant().Contains(k));
        }

        public async Task<WebContent> GetContentAsync(ContentRequest request)
        {
            try
            {
                // Debug.WriteLine($"[RSSProvider] Haber içeriği istendi: {request.Query}");

                // Önce request parametrelerinden kategori kontrolü yap
                var category = RSSCategory.General;
                if (request.Parameters.ContainsKey("category"))
                {
                    var categoryParam = request.Parameters["category"].ToString().ToLowerInvariant();
                    category = categoryParam switch
                    {
                        "economy" or "ekonomi" => RSSCategory.Economy,
                        "business" or "finance" => RSSCategory.Business,
                        "sports" => RSSCategory.Sports,
                        "technology" => RSSCategory.Technology,
                        "health" => RSSCategory.Health,
                        "entertainment" => RSSCategory.Entertainment,
                        "world" => RSSCategory.World,
                        _ => DetermineCategory(request.Query)
                    };
                }
                else
                {
                    category = DetermineCategory(request.Query);
                }
                var feeds = GetFeedsForCategory(category);
                
                // Sadece sağlıklı kaynakları al
                var healthyFeeds = feeds.Where(f => IsSourceHealthy(f.FeedUrl)).ToList();
                
                if (!healthyFeeds.Any())
                {
                    // Debug.WriteLine($"[RSSProvider] No healthy feeds available for category: {category}");
                    throw new Exception($"{GetCategoryName(category)} kategorisi için kullanılabilir haber kaynağı bulunamadı.");
                }
                
                var allItems = new List<RSSItem>();

                // Fetch from multiple sources in parallel
                var tasks = healthyFeeds.Select(source => FetchFeedAsync(source)).ToList();
                var results = await Task.WhenAll(tasks);

                foreach (var result in results.Where(r => r != null))
                {
                    allItems.AddRange(result.Items);
                }

                // Önce haberleri filtrele (duplicate, spam, boş link vs.)
                var filteredItems = FilterNewsItems(allItems);
                
                // Yeni: Kaynak çeşitliliği sağlayan hibrit algoritma
                var topItems = BalanceNewsSources(filteredItems, request.MaxResults, healthyFeeds);
                
                // Debug: Kaynak dağılımını logla
                var sourceDistribution = topItems.GroupBy(i => i.Source)
                    .ToDictionary(g => g.Key, g => g.Count());
                Debug.WriteLine($"[RSSProvider] Kaynak dağılımı: {string.Join(", ", sourceDistribution.Select(kv => $"{kv.Key}: {kv.Value}"))}");

                LogService.LogInfo($"[RSSProvider] {topItems.Count} haber çekildi ({GetCategoryName(category)} kategorisi)");

                // Debug: Metadata'ya eklenen haberleri logla
                LogService.LogInfo($"[RSSProvider] Metadata'ya {topItems.Count} haber ekleniyor (RSSItems key)");

                return new WebContent
                {
                    Title = $"{GetCategoryName(category)} Haberleri",
                    Content = FormatNewsContent(topItems),
                    Source = "RSS Aggregator",
                    Type = ContentType.RSS,
                    PublishedDate = topItems.FirstOrDefault()?.PublishDate ?? DateTime.Now,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = category,
                        ["ItemCount"] = topItems.Count,
                        ["Sources"] = feeds.Select(f => f.Name).ToList(),
                        ["SourceDistribution"] = sourceDistribution, // Kaynak dağılımı bilgisi
                        ["TTSContent"] = FormatNewsContentForTTS(topItems),
                        ["RSSItems"] = topItems // Haber listesini metadata'ya ekle
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception("RSS içeriği alınamadı", ex);
            }
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        private Dictionary<RSSCategory, List<RSSSource>> InitializeFeedSources()
        {
            return new Dictionary<RSSCategory, List<RSSSource>>
            {
                [RSSCategory.Technology] = new List<RSSSource>
                {
                    // Google News Teknoloji
                    new RSSSource
                    {
                        Name = "Google News Teknoloji",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqJggKIiBDQkFTRWdvSUwyMHZNRGRqTVhZU0FuUnlHZ0pVVWlnQVAB?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Technology,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Donanimhaber",
                        FeedUrl = "https://www.donanimhaber.com/rss/tum/",
                        Category = RSSCategory.Technology,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "Webrazzi",
                        FeedUrl = "https://webrazzi.com/feed/",
                        Category = RSSCategory.Technology,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "ShiftDelete",
                        FeedUrl = "https://shiftdelete.net/feed",
                        Category = RSSCategory.Technology,
                        Priority = 4
                    }
                },
                [RSSCategory.Economy] = new List<RSSSource>
                {
                    // Google News Ekonomi
                    new RSSSource
                    {
                        Name = "Google News Ekonomi",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqJggKIiBDQkFTRWdvSUwyMHZNRGx6TVdZU0FuUnlHZ0pVVWlnQVAB?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Economy,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "BloombergHT",
                        FeedUrl = "https://www.bloomberght.com/rss",
                        Category = RSSCategory.Economy,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "AA Ekonomi",
                        FeedUrl = "https://www.aa.com.tr/tr/rss/default?cat=ekonomi",
                        Category = RSSCategory.Economy,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "Para Analiz",
                        FeedUrl = "https://www.paraanaliz.com/rss",
                        Category = RSSCategory.Economy,
                        Priority = 4
                    }
                },
                [RSSCategory.Sports] = new List<RSSSource>
                {
                    // Google News Spor
                    new RSSSource
                    {
                        Name = "Google News Spor",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqJggKIiBDQkFTRWdvSUwyMHZNRFp1ZEdvU0FuUnlHZ0pVVWlnQVAB?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Sports,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Hürriyet Spor",
                        FeedUrl = "https://www.hurriyet.com.tr/rss/spor",
                        Category = RSSCategory.Sports,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "AA Spor",
                        FeedUrl = "https://www.aa.com.tr/tr/rss/default?cat=spor",
                        Category = RSSCategory.Sports,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "CNN Türk Spor",
                        FeedUrl = "https://www.cnnturk.com/feed/rss/spor",
                        Category = RSSCategory.Sports,
                        Priority = 4
                    }
                },
                [RSSCategory.General] = new List<RSSSource>
                {
                    // Google News Türkiye
                    new RSSSource
                    {
                        Name = "Google News Türkiye",
                        FeedUrl = "https://news.google.com/rss?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.General,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Hürriyet",
                        FeedUrl = "https://www.hurriyet.com.tr/rss/gundem",
                        Category = RSSCategory.General,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "CNN Türk",
                        FeedUrl = "https://www.cnnturk.com/feed/rss/all",
                        Category = RSSCategory.General,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "TRT Haber",
                        FeedUrl = "https://www.trthaber.com/xml_mobile.php?tur=xml_genel&kategori=gundem&adet=20&selectEx=yorumSay,okunmaadedi,anasayfamanset,kategorimanset",
                        Category = RSSCategory.General,
                        Priority = 4
                    },
                    new RSSSource
                    {
                        Name = "T24",
                        FeedUrl = "https://t24.com.tr/rss",
                        Category = RSSCategory.General,
                        Priority = 5
                    },
                    new RSSSource
                    {
                        Name = "Cumhuriyet",
                        FeedUrl = "https://www.cumhuriyet.com.tr/rss/son_dakika.xml",
                        Category = RSSCategory.General,
                        Priority = 6
                    },
                    new RSSSource
                    {
                        Name = "Anadolu Ajansı",
                        FeedUrl = "https://www.aa.com.tr/tr/rss/default?cat=guncel",
                        Category = RSSCategory.General,
                        Priority = 7
                    },
                },
                [RSSCategory.Entertainment] = new List<RSSSource>
                {
                    // Google News Magazin
                    new RSSSource
                    {
                        Name = "Google News Magazin",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqJggKIiBDQkFTRWdvSUwyMHZNREpxYW5RU0FuUnlHZ0pVVWlnQVAB?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Entertainment,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Hürriyet Magazin",
                        FeedUrl = "https://www.hurriyet.com.tr/rss/magazin",
                        Category = RSSCategory.Entertainment,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "Posta Magazin",
                        FeedUrl = "https://www.posta.com.tr/rss.xml",
                        Category = RSSCategory.Entertainment,
                        Priority = 3
                    }
                },
                [RSSCategory.Health] = new List<RSSSource>
                {
                    // Google News Sağlık
                    new RSSSource
                    {
                        Name = "Google News Sağlık",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqIQgKIhtDQkFTRGdvSUwyMHZNR3QwTlRFU0FuUnlLQUFQAQ?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Health,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Medimagazin",
                        FeedUrl = "https://www.medimagazin.com.tr/rss/",
                        Category = RSSCategory.Health,
                        Priority = 2
                    }
                },
                [RSSCategory.Science] = new List<RSSSource>
                {
                    // Google News Bilim
                    new RSSSource
                    {
                        Name = "Google News Bilim",
                        FeedUrl = "https://news.google.com/rss/topics/CAAqKAgKIiJDQkFTRXdvSkwyMHZNR1ptZHpWbUVnSjBjaG9DVkZJb0FBUAE?hl=tr&gl=TR&ceid=TR:tr",
                        Category = RSSCategory.Science,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "TÜBİTAK Bilim Genç",
                        FeedUrl = "https://bilimgenc.tubitak.gov.tr/rss",
                        Category = RSSCategory.Science,
                        Priority = 2
                    }
                },
                [RSSCategory.Politics] = new List<RSSSource>
                {
                    // Uluslararası Politik Haberler
                    new RSSSource
                    {
                        Name = "BBC Türkçe",
                        FeedUrl = "https://feeds.bbci.co.uk/turkce/rss.xml",
                        Category = RSSCategory.Politics,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Deutsche Welle Türkçe",
                        FeedUrl = "https://rss.dw.com/rdf/rss-tur-all",
                        Category = RSSCategory.Politics,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "VOA Türkçe",
                        FeedUrl = "https://www.voaturkce.com/api/zg$otevtiy",
                        Category = RSSCategory.Politics,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "Euronews Türkçe",
                        FeedUrl = "https://tr.euronews.com/rss",
                        Category = RSSCategory.Politics,
                        Priority = 4
                    }
                },
                [RSSCategory.International] = new List<RSSSource>
                {
                    // Uluslararası İngilizce Haberler
                    new RSSSource
                    {
                        Name = "BBC World",
                        FeedUrl = "https://feeds.bbci.co.uk/news/world/rss.xml",
                        Category = RSSCategory.International,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "CNN International",
                        FeedUrl = "http://rss.cnn.com/rss/edition_world.rss",
                        Category = RSSCategory.International,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "Reuters World News",
                        FeedUrl = "https://feeds.reuters.com/reuters/topNews",
                        Category = RSSCategory.International,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "The Guardian World",
                        FeedUrl = "https://www.theguardian.com/world/rss",
                        Category = RSSCategory.International,
                        Priority = 4
                    },
                    new RSSSource
                    {
                        Name = "Al Jazeera English",
                        FeedUrl = "https://www.aljazeera.com/xml/rss/all.xml",
                        Category = RSSCategory.International,
                        Priority = 5
                    },
                    new RSSSource
                    {
                        Name = "France 24",
                        FeedUrl = "https://www.france24.com/en/rss",
                        Category = RSSCategory.International,
                        Priority = 6
                    }
                },
                [RSSCategory.Business] = new List<RSSSource>
                {
                    // Uluslararası Ekonomi/İş Haberleri
                    new RSSSource
                    {
                        Name = "Bloomberg",
                        FeedUrl = "https://feeds.bloomberg.com/markets/news.rss",
                        Category = RSSCategory.Business,
                        Priority = 1
                    },
                    new RSSSource
                    {
                        Name = "Financial Times",
                        FeedUrl = "https://www.ft.com/?format=rss",
                        Category = RSSCategory.Business,
                        Priority = 2
                    },
                    new RSSSource
                    {
                        Name = "CNBC Top News",
                        FeedUrl = "https://www.cnbc.com/id/100003114/device/rss/rss.html",
                        Category = RSSCategory.Business,
                        Priority = 3
                    },
                    new RSSSource
                    {
                        Name = "Wall Street Journal",
                        FeedUrl = "https://feeds.a.dj.com/rss/RSSWorldNews.xml",
                        Category = RSSCategory.Business,
                        Priority = 4
                    },
                    new RSSSource
                    {
                        Name = "Forbes",
                        FeedUrl = "https://www.forbes.com/real-time/feed2/",
                        Category = RSSCategory.Business,
                        Priority = 5
                    }
                }
            };
        }

        private async Task<RSSFeed> FetchFeedAsync(RSSSource source)
        {
            // Sağlık kontrolü
            if (!IsSourceHealthy(source.FeedUrl))
            {
                // Sağlıksız kaynakları sessizce atla
                return null;
            }
            
            const int maxRetries = 2;
            int currentRetry = 0;
            
            while (currentRetry < maxRetries)
            {
                try
                {
                    // Configure request for Turkish content
                    var request = new HttpRequestMessage(HttpMethod.Get, source.FeedUrl);
                    request.Headers.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));
                    
                    // Timeout için CancellationToken kullan
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                
                // Fix common encoding issues
                content = FixTurkishEncoding(content);
                
                // XML temizleme ve düzeltme
                content = CleanAndFixXml(content);

                // XML parsing için güvenli okuyucu ayarları
                var xmlReaderSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreWhitespace = true,
                    ConformanceLevel = ConformanceLevel.Fragment,
                    CheckCharacters = false,
                    MaxCharactersFromEntities = 1024
                };

                try
                {
                    using var stringReader = new System.IO.StringReader(content);
                    using var xmlReader = XmlReader.Create(stringReader, xmlReaderSettings);
                    
                    var feed = SyndicationFeed.Load(xmlReader);
                    
                    var rssFeed = new RSSFeed
                    {
                    Title = feed.Title?.Text ?? source.Name,
                    Description = feed.Description?.Text ?? "",
                    Link = feed.Links?.FirstOrDefault()?.Uri?.ToString() ?? source.FeedUrl,
                    Language = feed.Language ?? "tr",
                    LastBuildDate = feed.LastUpdatedTime.DateTime,
                    Category = source.Category
                };

                foreach (var item in feed.Items.Take(20)) // Limit items per feed
                {
                    var cleanTitle = CleanHtml(item.Title?.Text ?? "");
                    var cleanDescription = CleanHtml(item.Summary?.Text ?? "");
                    var actualSource = source.Name;
                    
                    // Google News için özel işlemler
                    if (source.FeedUrl.Contains("news.google.com"))
                    {
                        // Gerçek kaynağı başlıktan çıkar
                        var extractedSource = ExtractSourceFromTitle(cleanTitle, out var titleWithoutSource);
                        if (!string.IsNullOrEmpty(extractedSource))
                        {
                            actualSource = extractedSource;
                            cleanTitle = titleWithoutSource;
                        }
                        
                        cleanDescription = CleanGoogleNewsDescription(cleanDescription, cleanTitle, actualSource);
                    }
                    else
                    {
                        // Diğer kaynaklar için genel uzunluk sınırlaması
                        if (cleanDescription.Length > 250)
                        {
                            // İlk 250 karakterde cümle sonu ara
                            var sentenceEnd = cleanDescription.LastIndexOfAny(new[] { '.', '!', '?' }, 250);
                            if (sentenceEnd > 100)
                            {
                                cleanDescription = cleanDescription.Substring(0, sentenceEnd + 1);
                            }
                            else
                            {
                                cleanDescription = cleanDescription.Substring(0, 247) + "...";
                            }
                        }
                    }
                    
                    var rssItem = new RSSItem
                    {
                        Title = cleanTitle,
                        Description = cleanDescription,
                        Link = item.Links?.FirstOrDefault()?.Uri?.ToString() ?? "",
                        PublishDate = item.PublishDate.DateTime,
                        Author = item.Authors?.FirstOrDefault()?.Name ?? "",
                        Guid = item.Id ?? Guid.NewGuid().ToString(),
                        Source = actualSource,
                        IsTranslated = false,
                        OriginalLanguage = null
                    };
                    
                    // İngilizce kaynaklar için çeviri yap
                    // Otomatik çeviri kontrolü
                    bool shouldTranslate = _translateService != null && IsEnglishSource(source.Name);
                    if (_configService != null)
                    {
                        shouldTranslate = shouldTranslate && _configService.User.NewsPreferences.AutoTranslateEnglishSources;
                    }
                    
                    if (shouldTranslate)
                    {
                        try
                        {
                            // Başlığı çevir
                            var translatedTitle = await _translateService.TranslateAsync(cleanTitle, "tr", "en");
                            if (!string.IsNullOrEmpty(translatedTitle) && translatedTitle != cleanTitle)
                            {
                                rssItem.OriginalTitle = cleanTitle;
                                rssItem.Title = translatedTitle;
                                rssItem.IsTranslated = true;
                                rssItem.OriginalLanguage = "en";
                            }
                            
                            // İçeriği çevir
                            if (!string.IsNullOrEmpty(cleanDescription))
                            {
                                var translatedDesc = await _translateService.TranslateAsync(cleanDescription, "tr", "en");
                                if (!string.IsNullOrEmpty(translatedDesc) && translatedDesc != cleanDescription)
                                {
                                    rssItem.OriginalDescription = cleanDescription;
                                    rssItem.Description = translatedDesc;
                                }
                            }
                            
                            // Kaynak ismini güncelle
                            if (rssItem.IsTranslated)
                            {
                                rssItem.Source = $"{actualSource} (İngilizce'den çeviri)";
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Çeviri hatası: {ex.Message}");
                            // Çeviri başarısız olursa orijinal metni kullan
                        }
                    }

                    // Extract categories
                    foreach (var category in item.Categories)
                    {
                        rssItem.Categories.Add(category.Name);
                    }

                    rssFeed.Items.Add(rssItem);
                }

                source.LastSuccessfulFetch = DateTime.Now;
                source.FailureCount = 0;
                
                // Başarılı kaynak kaydı
                RecordSourceSuccess(source.FeedUrl);

                return rssFeed;
                }
                catch (XmlException)
                {
                    // Inner try bloğunda XML parse hatası
                    throw; // Stack trace'i koruyarak dış catch'e gönder
                }
                }
                catch (Exception ex)
                {
                    currentRetry++;

                    // DÜZELTME: Exception türüne göre farklı işlemler
                    var errorType = ex switch
                    {
                        XmlException => "XML Parse Error",
                        HttpRequestException => "HTTP Request Error",
                        TaskCanceledException => "Timeout",
                        OperationCanceledException => "Cancelled",
                        System.Net.Sockets.SocketException => "Socket Error",
                        System.IO.IOException => "IO Error",
                        _ => "Unknown Error"
                    };

                    LogService.LogDebug($"[RSSProvider] {errorType} in {source.Name}: {ex.Message}");

                    // XML parse hatası ise alternatif yöntem dene
                    if (ex is XmlException xmlEx)
                    {
                        LogService.LogDebug($"[RSSProvider] XML parse hatası, basit parser deneniyor");
                        var simpleFeed = await TrySimpleXmlParsingAsync(source.FeedUrl, source);
                        if (simpleFeed != null)
                        {
                            RecordSourceSuccess(source.FeedUrl);
                            return simpleFeed;
                        }
                    }

                    // Socket/IO exception ise direkt retry yapma, kaynak sağlığını işaretle
                    if (ex is System.Net.Sockets.SocketException or System.IO.IOException)
                    {
                        LogService.LogWarning($"[RSSProvider] Socket/IO error in {source.Name}, marking unhealthy");
                        RecordSourceFailure(source.FeedUrl, $"{errorType}: {ex.Message}");
                        return null; // Retry yapma
                    }

                    // Timeout exception ise kısa retry yap
                    if (ex is TaskCanceledException or OperationCanceledException)
                    {
                        if (currentRetry < maxRetries)
                        {
                            var delayMs = 500; // Timeout için kısa gecikme
                            LogService.LogDebug($"[RSSProvider] Timeout retry {currentRetry}/{maxRetries} - {delayMs}ms bekle");
                            await Task.Delay(delayMs);
                            continue;
                        }
                    }

                    // Son deneme değilse, exponential backoff ile bekle
                    if (currentRetry < maxRetries)
                    {
                        var delayMs = Math.Min(1000 * (int)Math.Pow(2, currentRetry), 8000); // Max 8 saniye
                        LogService.LogDebug($"[RSSProvider] Retry {currentRetry}/{maxRetries} için {delayMs}ms bekleniyor");
                        await Task.Delay(delayMs);
                        continue;
                    }

                    // RSS hatalarını sessizce logla - kullanıcıyı rahatsız etme
                    if (!ex.Message.Contains("404") && !ex.Message.Contains("DTD"))
                    {
                        LogService.LogDebug($"[RSSProvider] RSS feed hatası ({source.Name}): {errorType}");
                    }
                    source.FailureCount++;

                    // Başarısız kaynak kaydı
                    RecordSourceFailure(source.FeedUrl, $"{errorType}: {ex.Message}");

                    return null;
                }
            }
            
            return null; // Retry'lar tükendi
        }

        private RSSCategory DetermineCategory(string query)
        {
            var lowerQuery = query.ToLowerInvariant();

            // Önce exact match dene
            // Teknoloji
            if (lowerQuery.Contains("teknoloji") || lowerQuery.Contains("bilim") || lowerQuery.Contains("yazılım"))
                return RSSCategory.Technology;
            
            // Ekonomi
            if (lowerQuery.Contains("ekonomi") || lowerQuery.Contains("borsa") || lowerQuery.Contains("dolar") || lowerQuery.Contains("euro"))
                return RSSCategory.Economy;
            
            // İş Dünyası (Business)
            if (lowerQuery.Contains("business") || lowerQuery.Contains("iş dünyası") || lowerQuery.Contains("şirket") || 
                lowerQuery.Contains("bloomberg") || lowerQuery.Contains("finans") || lowerQuery.Contains("yatırım"))
                return RSSCategory.Business;
            
            // Spor
            if (lowerQuery.Contains("spor") || lowerQuery.Contains("futbol") || lowerQuery.Contains("basketbol"))
                return RSSCategory.Sports;
            
            // Magazin
            if (lowerQuery.Contains("magazin") || lowerQuery.Contains("eğlence"))
                return RSSCategory.Entertainment;
            
            // Sağlık
            if (lowerQuery.Contains("sağlık") || lowerQuery.Contains("tıp") || lowerQuery.Contains("hastalık"))
                return RSSCategory.Health;
            
            // Bilim
            if (lowerQuery.Contains("bilim") || lowerQuery.Contains("araştırma") || lowerQuery.Contains("keşif"))
                return RSSCategory.Science;
            
            // Politika
            if (lowerQuery.Contains("politika") || lowerQuery.Contains("siyaset") || lowerQuery.Contains("seçim"))
                return RSSCategory.Politics;
            
            // Uluslararası
            if (lowerQuery.Contains("dünya") || lowerQuery.Contains("uluslararası") || lowerQuery.Contains("global") ||
                lowerQuery.Contains("international") || lowerQuery.Contains("bbc") || lowerQuery.Contains("cnn") || 
                lowerQuery.Contains("reuters"))
                return RSSCategory.International;

            // Exact match bulunamadı, fuzzy matching ile tekrar dene
            var categoryKeywords = new Dictionary<RSSCategory, string[]>
            {
                { RSSCategory.Sports, new[] { "spor", "futbol", "basketbol", "voleybol", "tenis" } },
                { RSSCategory.Technology, new[] { "teknoloji", "bilim", "yazılım", "bilgisayar" } },
                { RSSCategory.Economy, new[] { "ekonomi", "borsa", "dolar", "euro", "piyasa" } },
                { RSSCategory.Entertainment, new[] { "magazin", "eğlence", "sinema", "dizi" } },
                { RSSCategory.Health, new[] { "sağlık", "tıp", "hastalık", "doktor" } },
                { RSSCategory.Politics, new[] { "politika", "siyaset", "seçim", "parti" } },
                { RSSCategory.Science, new[] { "bilim", "araştırma", "keşif", "uzay" } }
            };

            // Her kelime için fuzzy matching yap
            var words = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                foreach (var categoryPair in categoryKeywords)
                {
                    foreach (var keyword in categoryPair.Value)
                    {
                        // Levenshtein distance hesapla
                        var distance = GetLevenshteinDistance(word, keyword);
                        
                        // Eğer distance 2 veya daha az ise (typo toleransı)
                        if (distance <= 2 && distance < keyword.Length / 2)
                        {
                            Debug.WriteLine($"[RSSProvider] Fuzzy match: '{word}' -> '{keyword}' (distance: {distance}) => {categoryPair.Key}");
                            return categoryPair.Key;
                        }
                    }
                }
            }

            return RSSCategory.General;
        }

        private List<RSSSource> GetFeedsForCategory(RSSCategory category)
        {
            if (_feedSources.TryGetValue(category, out var feeds))
            {
                var enabledFeeds = feeds.Where(f => f.IsEnabled).ToList();
                
                // Kullanıcı tercihlerine göre filtrele
                if (_configService != null)
                {
                    var preferences = _configService.User.NewsPreferences;
                    
                    // Debug log ekleyelim
                    Debug.WriteLine($"[RSSProvider] ShowAllSources: {preferences.ShowAllSources}");
                    Debug.WriteLine($"[RSSProvider] SelectedNewsSources count: {preferences.SelectedNewsSources?.Count ?? 0}");
                    if (preferences.SelectedNewsSources != null)
                    {
                        Debug.WriteLine($"[RSSProvider] Selected sources: {string.Join(", ", preferences.SelectedNewsSources)}");
                    }
                    
                    // Eğer ShowAllSources false ise, sadece kullanıcının seçtiği kaynakları göster
                    if (!preferences.ShowAllSources && preferences.SelectedNewsSources != null && preferences.SelectedNewsSources.Any())
                    {
                        var beforeCount = enabledFeeds.Count;
                        enabledFeeds = enabledFeeds.Where(f => preferences.SelectedNewsSources.Contains(f.Name)).ToList();
                        Debug.WriteLine($"[RSSProvider] Filtered feeds: {beforeCount} -> {enabledFeeds.Count}");
                        Debug.WriteLine($"[RSSProvider] Remaining sources: {string.Join(", ", enabledFeeds.Select(f => f.Name))}");
                    }
                }
                
                return enabledFeeds.OrderBy(f => f.Priority).ToList();
            }

            // Fallback to general news
            return _feedSources[RSSCategory.General];
        }

        private string GetCategoryName(RSSCategory category)
        {
            return category switch
            {
                RSSCategory.Technology => "Teknoloji",
                RSSCategory.Economy => "Ekonomi",
                RSSCategory.Sports => "Spor",
                RSSCategory.Entertainment => "Magazin",
                RSSCategory.Politics => "Politika",
                RSSCategory.Health => "Sağlık",
                RSSCategory.Science => "Bilim",
                RSSCategory.International => "Uluslararası",
                RSSCategory.Business => "İş Dünyası",
                RSSCategory.World => "Dünya",
                RSSCategory.Local => "Yerel",
                _ => "Genel"
            };
        }

        private string FormatNewsContent(List<RSSItem> items)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // HTML entity decode işlemi ekle
                var decodedTitle = System.Net.WebUtility.HtmlDecode(item.Title);

                // Description zaten CleanHtml'den geçmiş olmalı ama bazen kaçabilir
                // Tekrar temizle - özellikle Google News için
                var cleanDescription = item.Description;
                if (!string.IsNullOrEmpty(cleanDescription))
                {
                    // HTML içeriyor mu kontrol et
                    if (cleanDescription.Contains("<") && cleanDescription.Contains(">"))
                    {
                        // HTML varsa temizle
                        cleanDescription = CleanHtml(cleanDescription);
                    }
                    else
                    {
                        // HTML yoksa sadece decode et
                        cleanDescription = System.Net.WebUtility.HtmlDecode(cleanDescription);
                    }
                }

                sb.AppendLine($"{i + 1}. {decodedTitle}");
                if (!string.IsNullOrEmpty(cleanDescription))
                {
                    sb.AppendLine();
                    sb.AppendLine(cleanDescription);
                }
                sb.AppendLine();
                sb.AppendLine($"Kaynak: {item.Source} | {item.PublishDate:dd.MM.yyyy HH:mm}");
                if (i < items.Count - 1) // Son haberden sonra ekstra boşluk ekleme
                {
                    sb.AppendLine(); // Haberler arası boşluk
                }
            }

            return sb.ToString();
        }

        private string FormatNewsContentForTTS(List<RSSItem> items)
        {
            var sb = new StringBuilder();
            
            for (int i = 0; i < items.Count; i++)
            {
                // HTML entity decode işlemi ekle
                var decodedTitle = System.Net.WebUtility.HtmlDecode(items[i].Title);
                
                // Edge TTS doğal karakterleri desteklediği için minimal temizlik yapıyoruz
                // Sadece gerçekten sorunlu olabilecek karakterleri temizle
                decodedTitle = decodedTitle.Trim();
                
                // Sadece başlığı ekle, numara ekleme
                sb.Append(decodedTitle);
                
                // Son haber değilse nokta ve duraklama ekle
                if (i < items.Count - 1)
                {
                    sb.Append(". "); // Nokta ve boşluk, TTS için daha doğal duraklama sağlar
                }
            }

            return sb.ToString();
        }

        private string FixTurkishEncoding(string content)
        {
            // First decode HTML entities (handles &#231; → ç, &#246; → ö, etc.)
            content = System.Net.WebUtility.HtmlDecode(content);
            
            // Fix common Turkish character encoding issues
            content = content
                .Replace("Ä±", "ı")
                .Replace("Ä°", "İ")
                .Replace("ÄŸ", "ğ")
                .Replace("Ä", "Ğ")
                .Replace("Ã¼", "ü")
                .Replace("Ãœ", "Ü")
                .Replace("Å", "ş")
                .Replace("Å", "Ş")
                .Replace("Ã¶", "ö")
                .Replace("Ã–", "Ö")
                .Replace("Ã§", "ç")
                .Replace("Ã‡", "Ç")
                // Additional common encoding issues
                .Replace("\u2019", "'")
                .Replace("\u201c", "\"")
                .Replace("\u201d", "\"")
                .Replace("\u2026", "...")
                .Replace("\u2013", "-")
                .Replace("\u2014", "-");

            return content;
        }

        private string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            // Debug log for Google News
            if (html.Contains("<ol>") && html.Contains("<li>") && html.Contains("<font"))
            {
                LogService.LogDebug($"[CleanHtml] Google News HTML detected, length: {html.Length}");
            }

            // Remove script and style content first
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove HTML comments
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<!--[\s\S]*?-->", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // AGRESIF Google News liste temizleme
            if (html.Contains("<ol>") || html.Contains("<ul>") || html.Contains("<li>"))
            {
                // Önce complex pattern - <li><a>başlık</a> <font>kaynak</font></li>
                html = System.Text.RegularExpressions.Regex.Replace(html,
                    @"<li[^>]*>\s*<a[^>]*>([^<]+)</a>\s*<font[^>]*>([^<]+)</font>\s*</li>",
                    "\n• $1 - $2",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

                // Sadece link olan li'ler
                html = System.Text.RegularExpressions.Regex.Replace(html,
                    @"<li[^>]*>\s*<a[^>]*>([^<]+)</a>\s*</li>",
                    "\n• $1",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // İç içe taglar olan li'ler için - önce içindeki tagları temizle
                html = System.Text.RegularExpressions.Regex.Replace(html,
                    @"<li[^>]*>([\s\S]*?)</li>",
                    delegate(System.Text.RegularExpressions.Match m)
                    {
                        var content = m.Groups[1].Value;
                        // İçerideki tüm tagları temizle
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<a[^>]*>([^<]+)</a>", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<font[^>]*>([^<]+)</font>", " - $1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        return "\n• " + content.Trim();
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                // Ol ve ul taglarını kaldır
                html = System.Text.RegularExpressions.Regex.Replace(html, @"</?[ou]l[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Genel link temizleme (liste dışındakiler için)
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<a[^>]*>([^<]+)</a>", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Font taglarını temizle
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<font[^>]*>([^<]*)</font>", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace common HTML tags with appropriate spacing
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"</div>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<p[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<div[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove all remaining HTML tags
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Clean up any remaining tag fragments
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^[^<]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // HTML decode - iki kez yap (nested entities için)
            html = System.Net.WebUtility.HtmlDecode(html);
            html = System.Net.WebUtility.HtmlDecode(html);

            // Additional manual replacements
            html = html.Replace("&nbsp;", " ")
                      .Replace("&amp;", "&")
                      .Replace("&lt;", "<")
                      .Replace("&gt;", ">")
                      .Replace("&quot;", "\"")
                      .Replace("&#39;", "'")
                      .Replace("&apos;", "'");
            
            // Clean up multiple spaces and normalize whitespace
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");
            
            return html.Trim();
        }

        private string ExtractSourceFromTitle(string title, out string cleanedTitle)
        {
            cleanedTitle = title;
            
            // Bilinen haber kaynakları listesi
            var knownSources = new[] { 
                "NTV Haber", "NTVSpor", "NTV Spor", "Habertürk", "CNN TÜRK", "TRT Haber",
                "Hürriyet", "Milliyet", "Sabah", "Sözcü", "Cumhuriyet", "Fanatik",
                "Anadolu Ajansı", "AA", "DHA", "İHA", "BBC", "Reuters", "Bloomberg",
                "Forbes", "Euronews", "Sputnik", "VOA Türkçe", "Dünya", "Akşam",
                "Star", "Takvim", "Posta", "Yeni Şafak", "Yeni Akit", "BirGün",
                "Evrensel", "Haber7", "A Haber", "A Spor", "beIN SPORTS", "Sporx",
                "Goal.com", "Transfermarkt", "Futbol Arena", "AMK Spor", "Gazete Oksijen",
                "Fenerbahçe Spor Kulübü", "Galatasaray Spor Kulübü", "Beşiktaş JK",
                "Spor Haberleri", "Futbol Haberleri", "Son Dakika Haberleri", "T24"
            };
            
            // İlk olarak bilinen kategorileri temizle
            var categoriesToRemove = new[] { 
                " - Spor Haberleri", " - Futbol Haberleri", " - Son Dakika Haberleri",
                " - Transfer Haberleri", " | Spor Haberleri", " | Futbol Haberleri",
                " - Gündem Haberleri", " - Magazin Haberleri", " - Ekonomi Haberleri",
                " - Dünya Haberleri", " - Türkiye Haberleri", " - Sağlık Haberleri",
                " - Bilim Haberleri", " - Teknoloji Haberleri", " - Yaşam Haberleri"
            };
            
            foreach (var category in categoriesToRemove)
            {
                if (title.Contains(category))
                {
                    title = title.Replace(category, "");
                }
            }
            
            // Takım isimlerini içeren kategorileri daha kapsamlı temizle
            // Regex ile " - Takım" veya " - Takım ..." formatlarını temizle
            var teamPattern = @" - (Beşiktaş|Fenerbahçe|Galatasaray|Trabzonspor|Başakşehir|Ankaragücü|Kayserispor|Sivasspor|Konyaspor|Antalyaspor|Alanyaspor|Adana Demirspor|Fatih Karagümrük|Ümraniyespor|Giresunspor|Hatayspor|Gaziantep|Kasımpaşa|Rizespor|İstanbulspor|Samsunspor|Kocaelispor|Sakaryaspor|Bursaspor|Eskişehirspor|Göztepe|Altay|Denizlispor|Balıkesirspor|Manisaspor|Elazığspor|Erzurumspor|Yeni Malatyaspor)(\s+[^-|]*)?$";
            title = System.Text.RegularExpressions.Regex.Replace(title, teamPattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Transfer haberleri formatını temizle (örn: "- Fenerbahçe transfer haberleri 2025 Temmuz")
            var transferPattern = @" - \w+\s+transfer\s+haberleri\s+\d{4}\s+\w+";
            title = System.Text.RegularExpressions.Regex.Replace(title, transferPattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Başlıktan kaynak bilgisini ayıkla
            // Format: "Başlık - Kaynak" veya "Başlık | Kaynak"
            var separatorIndex = title.LastIndexOf(" - ");
            if (separatorIndex == -1)
                separatorIndex = title.LastIndexOf(" | ");
            
            if (separatorIndex > 0 && separatorIndex < title.Length - 3)
            {
                var afterSeparator = title.Substring(separatorIndex + 3).Trim();
                var potentialSource = afterSeparator;
                
                // Bilinen kaynak mı kontrol et
                var matchedSource = knownSources.FirstOrDefault(s => 
                    s.Equals(potentialSource, StringComparison.OrdinalIgnoreCase));
                
                if (!string.IsNullOrEmpty(matchedSource))
                {
                    cleanedTitle = title.Substring(0, separatorIndex).Trim();
                    return matchedSource;
                }
                
                // Bilinen kaynak değilse ama makul uzunluktaysa kabul et
                if (potentialSource.Length < 30 && potentialSource.Split(' ').Length <= 3)
                {
                    cleanedTitle = title.Substring(0, separatorIndex).Trim();
                    return potentialSource;
                }
            }
            
            cleanedTitle = title;
            
            return null;
        }
        
        private string CleanGoogleNewsDescription(string description, string title, string sourceName)
        {
            if (string.IsNullOrEmpty(description))
                return "";

            // HTML liste formatı tespit edilirse boş dön (sadece başlık kullanılacak)
            if (description.Contains("<ol>") || description.Contains("<li>") || description.Contains("<font"))
            {
                LogService.LogDebug("[CleanGoogleNewsDescription] HTML list detected, returning empty description");
                return ""; // Google News listelerinin açıklama kısmını kullanma
            }

            // Önce başlığın tekrarını kaldır
            if (!string.IsNullOrEmpty(title) && description.StartsWith(title))
            {
                description = description.Substring(title.Length).TrimStart(' ', '-', '|', '!', ':');
            }

            // Bilinen tüm haber kaynaklarını listele - daha kapsamlı
            var knownSources = new[] {
                "Habertürk", "Fanatik", "NTVSpor", "NTV Spor", "Milliyet", "Hürriyet", "Sabah",
                "Sözcü", "CNN Türk", "CNN TÜRK", "BBC", "Reuters", "Bloomberg", "Forbes", "TRT Haber",
                "NTV Haber", "A Haber", "A Spor", "Haber7", "Euronews", "Gazete Oksijen",
                "İhlas Haber Ajansı", "AA", "DHA", "İHA", "Son Dakika Haberleri",
                "Spor Haberleri", "Futbol Haberleri", "beIN SPORTS", "beinsports", "Yeni Şafak",
                "Cumhuriyet", "T24", "Posta", "Star", "Akşam", "Takvim", "Anadolu Ajansı",
                "Sputnik", "VOA Türkçe", "Deutsche Welle", "Dünya", "BirGün", "Evrensel",
                "Tenis Haberleri", "Basketbol Haberleri", "Voleybol Haberleri", "Transfer Haberleri",
                "beinsports.com.tr", "Goal.com", "Transfermarkt", "Futbol Arena", "AMK Spor",
                "Fenerbahçe SK", "Galatasaray SK", "Beşiktaş JK", "Trabzonspor", "Başakşehir",
                "TFF", "UEFA", "FIFA", "Sporx", "Lig TV", "S Sport", "Smart Spor", "Tivibu Spor",
                "DAZN", "Digiturk", "beIN Connect", "Spor Toto", "İddaa", "Bilyoner", "Nesine",
                "Misli", "Oley", "Tuttur", "Sahadan", "Mackolik", "Flashscore", "Sofascore",
                "Onedio", "Webaslan", "Fotomaç", "Fotospor", "Hürriyet Spor", "Milliyet Spor",
                "Sabah Spor", "Takvim Spor", "Posta Spor", "Güneş Spor", "Türkiye Gazetesi",
                "Yeni Akit", "Yeni Şafak Spor", "Star Spor", "Akşam Spor", "Vatan", "Radikal",
                "GZT", "Karar"
            };

            // Sadece başında kaynak ismi varsa temizle (daha akıllı temizlik)
            foreach (var source in knownSources)
            {
                // Kaynak adı tam eşleşme ile başlıyorsa temizle
                if (description.StartsWith(source + " "))
                {
                    description = description.Substring(source.Length + 1);
                    break; // İlk eşleşmede dur, çok agresif temizlik yapma
                }
                else if (description.StartsWith(source + ":"))
                {
                    description = description.Substring(source.Length + 1);
                    break; // İlk eşleşmede dur
                }
                else if (description.StartsWith(source + "-"))
                {
                    description = description.Substring(source.Length + 1);
                    break; // İlk eşleşmede dur
                }
            }
            
            // Birden fazla boşluğu tek boşluğa indir
            description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+", " ");
            
            // Noktalama işaretlerinden önceki/sonraki gereksiz boşlukları temizle
            description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+([.!?,;:])", "$1");
            description = System.Text.RegularExpressions.Regex.Replace(description, @"([.!?])\s*([.!?])", "$1");
            
            // Kalın font işaretleyicilerini temizle
            description = System.Text.RegularExpressions.Regex.Replace(description, @"\*\*([^*]+)\*\*", "$1");
            
            // URL'leri temizle
            description = System.Text.RegularExpressions.Regex.Replace(description, @"https?://[^\s]+", "");
            
            // E-posta adreslerini temizle
            description = System.Text.RegularExpressions.Regex.Replace(description, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "");
            
            // Gereksiz sembolleri temizle
            description = description.Replace("...", ".").Replace("..", ".");
            
            // Baştaki ve sondaki boşlukları temizle
            description = description.Trim();
            
            // İlk cümleyi al (ama yeterince uzunsa)
            var sentences = System.Text.RegularExpressions.Regex.Split(description, @"(?<=[.!?])\s+");
            if (sentences.Length > 0)
            {
                var firstSentence = sentences[0].Trim();
                
                // Eğer ilk cümle çok kısaysa, ikinci cümleyi de ekle
                if (firstSentence.Length < 50 && sentences.Length > 1)
                {
                    description = firstSentence + " " + sentences[1].Trim();
                }
                else
                {
                    description = firstSentence;
                }
            }
            
            // Maksimum uzunluk kontrolü - daha uzun içerik için limit artırıldı
            if (description.Length > 500)
            {
                var cutIndex = 497;
                
                // Kelime ortasında kesmeyi önle
                while (cutIndex > 0 && !char.IsWhiteSpace(description[cutIndex]))
                {
                    cutIndex--;
                }
                
                if (cutIndex > 0)
                {
                    description = description.Substring(0, cutIndex).Trim() + "...";
                }
                else
                {
                    description = description.Substring(0, 497) + "...";
                }
            }
            
            // Eğer description çok kısa veya boşsa, varsayılan metin - daha kısa limit
            if (string.IsNullOrWhiteSpace(description) || description.Length < 10)
            {
                // Başlık varsa o kullanılsın, yoksa varsayılan metin
                if (!string.IsNullOrEmpty(title) && title.Length > 20)
                {
                    description = title; // Başlığı açıklama olarak kullan
                }
                else
                {
                    description = "Detaylı bilgi için habere tıklayın.";
                }
            }
            
            return description.Trim();
        }

        private bool IsSourceHealthy(string feedUrl)
        {
            lock (_healthLock)
            {
                if (!_sourceHealth.ContainsKey(feedUrl))
                    return true; // Yeni kaynak, sağlıklı varsayalım

                var health = _sourceHealth[feedUrl];
                
                // Son 5 dakika içinde 3'ten fazla hata varsa devre dışı
                if (health.ConsecutiveFailures >= 3)
                {
                    var timeSinceLastFailure = DateTime.Now - health.LastFailureTime;
                    if (timeSinceLastFailure < TimeSpan.FromMinutes(5))
                        return false; // Hala devre dışı
                    
                    // 5 dakika geçmiş, tekrar deneyelim
                    health.ConsecutiveFailures = 0;
                }
                
                return true;
            }
        }

        private void RecordSourceSuccess(string feedUrl)
        {
            lock (_healthLock)
            {
                if (_sourceHealth.ContainsKey(feedUrl))
                {
                    _sourceHealth[feedUrl].ConsecutiveFailures = 0;
                    _sourceHealth[feedUrl].LastSuccessTime = DateTime.Now;
                }
                else
                {
                    _sourceHealth[feedUrl] = new SourceHealthInfo
                    {
                        FeedUrl = feedUrl,
                        LastSuccessTime = DateTime.Now,
                        ConsecutiveFailures = 0
                    };
                }
            }
        }

        private void RecordSourceFailure(string feedUrl, string error)
        {
            lock (_healthLock)
            {
                if (_sourceHealth.ContainsKey(feedUrl))
                {
                    _sourceHealth[feedUrl].ConsecutiveFailures++;
                    _sourceHealth[feedUrl].LastFailureTime = DateTime.Now;
                    _sourceHealth[feedUrl].LastError = error;
                }
                else
                {
                    _sourceHealth[feedUrl] = new SourceHealthInfo
                    {
                        FeedUrl = feedUrl,
                        ConsecutiveFailures = 1,
                        LastFailureTime = DateTime.Now,
                        LastError = error
                    };
                }
            }
        }

        private class SourceHealthInfo
        {
            public string FeedUrl { get; set; }
            public int ConsecutiveFailures { get; set; }
            public DateTime LastSuccessTime { get; set; }
            public DateTime LastFailureTime { get; set; }
            public string LastError { get; set; }
        }
        
        /// <summary>
        /// İngilizce kaynak olup olmadığını kontrol eder
        /// </summary>
        private bool IsEnglishSource(string sourceName)
        {
            var englishSources = new[] 
            {
                "BBC World", "CNN International", "Reuters", "The Guardian",
                "Bloomberg", "Financial Times", "CNBC", "Wall Street Journal",
                "Forbes", "Al Jazeera English", "France 24"
            };
            
            return englishSources.Any(s => sourceName.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Haberleri filtreler: duplicate, spam, boş link kontrolü
        /// </summary>
        private List<RSSItem> FilterNewsItems(List<RSSItem> items)
        {
            var filtered = new List<RSSItem>();
            var seenTitles = new HashSet<string>();
            var seenHashes = new HashSet<int>();
            
            // Spam/clickbait kelimeleri - Genişletilmiş liste
            var spamKeywords = new[]
            {
                // Plaka/kod sorguları
                "nerenin plakası", "hangi ilin plakası", "plaka kodu", "telefon kodu", "alan kodu",
                
                // Astroloji/burç
                "burç yorumları", "astroloji", "falınız", "burç yorumu", "günlük burç", "haftalık burç",
                
                // Sağlık/diyet spam
                "zayıflama", "kilo verme", "mucize ilaç",
                
                // Clickbait
                "tıkla", "kazan", "fırsat kaçmaz", "şok", "flaş", "bomba", "olay",
                
                // Tarifler
                "tarifi", "nasıl yapılır", "nasıl pişirilir", "malzemeleri",
                
                // Promosyonlar
                "indirim", "kampanya", "fırsat", "ücretsiz", "bedava",
                
                // Listicle
                "en iyi", "en güzel", "top 10", "listesi"
            };
            
            foreach (var item in items)
            {
                // Boş link kontrolü
                if (string.IsNullOrWhiteSpace(item.Link))
                {
                    LogService.LogInfo($"[RSSProvider] Boş link filtrelendi: {item.Title}");
                    continue;
                }
                
                // Tarih damgası kontrolü
                if (item.PublishDate < DateTime.Now.AddMonths(-1) || // 1 aydan eski
                    item.PublishDate > DateTime.Now.AddDays(1)) // Gelecek tarihli
                {
                    LogService.LogInfo($"[RSSProvider] Geçersiz tarih filtrelendi: {item.Title} (Tarih: {item.PublishDate})");
                    continue;
                }
                
                // Noktalama işareti kontrolü
                if (item.Title.Contains("???") || item.Title.Contains("!!!") || item.Title.Contains("?!"))
                {
                    LogService.LogInfo($"[RSSProvider] Aşırı noktalama işareti filtrelendi: {item.Title}");
                    continue;
                }
                
                // Spam/clickbait kontrolü
                var titleLower = item.Title.ToLowerInvariant();
                if (spamKeywords.Any(spam => titleLower.Contains(spam)))
                {
                    LogService.LogInfo($"[RSSProvider] Spam haber filtrelendi: {item.Title}");
                    continue;
                }
                
                // Başlık uzunluk kontrolü - Güncellenmiş
                if (item.Title.Length < 20 || item.Title.Length > 150)
                {
                    LogService.LogInfo($"[RSSProvider] Uygunsuz başlık uzunluğu filtrelendi: {item.Title} (Uzunluk: {item.Title.Length})");
                    continue;
                }
                
                // Büyük harf oranı kontrolü
                var upperCaseCount = item.Title.Count(char.IsUpper);
                var letterCount = item.Title.Count(char.IsLetter);
                if (letterCount > 0)
                {
                    var upperCaseRatio = (double)upperCaseCount / letterCount;
                    if (upperCaseRatio > 0.4) // %40'tan fazla büyük harf
                    {
                        LogService.LogInfo($"[RSSProvider] Aşırı büyük harf kullanımı filtrelendi: {item.Title} (Oran: {upperCaseRatio:P})");
                        continue;
                    }
                }
                
                // SEO pattern algılama
                var seoPatterns = new[] { "nedir?", "nerede?", "ne zaman?", "kaç?", "kimdir?", "nasıl?", "ne kadar?" };
                if (seoPatterns.Any(pattern => titleLower.EndsWith(pattern)))
                {
                    LogService.LogInfo($"[RSSProvider] SEO pattern filtrelendi: {item.Title}");
                    continue;
                }
                
                // Emoji/özel karakter yoğunluğu kontrolü
                var emojiCount = System.Text.RegularExpressions.Regex.Matches(item.Title, @"[\u2600-\u27BF]|[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2B50-\u2B55]|[\u231A-\u23FF]|[\u25A0-\u25FF]").Count;
                if (emojiCount > 2)
                {
                    LogService.LogInfo($"[RSSProvider] Aşırı emoji kullanımı filtrelendi: {item.Title} (Emoji sayısı: {emojiCount})");
                    continue;
                }
                
                // Duplicate kontrolü - Tam eşleşme
                var normalizedTitle = NormalizeTitle(item.Title);
                if (seenTitles.Contains(normalizedTitle))
                {
                    LogService.LogInfo($"[RSSProvider] Duplicate başlık filtrelendi: {item.Title}");
                    continue;
                }
                
                // Duplicate kontrolü - Hash bazlı benzerlik
                var titleHash = GetTitleHash(normalizedTitle);
                if (seenHashes.Contains(titleHash))
                {
                    LogService.LogInfo($"[RSSProvider] Benzer haber filtrelendi: {item.Title}");
                    continue;
                }
                
                // Aynı konulu haber kontrolü - Levenshtein distance
                bool isDuplicate = false;
                foreach (var existing in filtered)
                {
                    var distance = GetLevenshteinDistance(normalizedTitle, NormalizeTitle(existing.Title));
                    var similarity = 1.0 - (double)distance / Math.Max(normalizedTitle.Length, NormalizeTitle(existing.Title).Length);
                    
                    if (similarity > 0.85) // %85 benzerlik
                    {
                        LogService.LogInfo($"[RSSProvider] Çok benzer haber filtrelendi: {item.Title} (Benzerlik: {similarity:P})");
                        isDuplicate = true;
                        break;
                    }
                }
                
                if (isDuplicate) continue;
                
                // Reklam/promosyon algılama
                if (System.Text.RegularExpressions.Regex.IsMatch(item.Title, @"[\d,]+\s*(TL|₺|%)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) || 
                    (titleLower.Contains("indirim") && System.Text.RegularExpressions.Regex.IsMatch(item.Title, @"\b[A-Z][A-Za-z]+\b")))
                {
                    LogService.LogInfo($"[RSSProvider] Reklam/promosyon içeriği filtrelendi: {item.Title}");
                    continue;
                }
                
                // Tekrarlayan kelime kontrolü
                var words = item.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var wordGroups = words.GroupBy(w => w.ToLowerInvariant()).Where(g => g.Count() >= 3);
                if (wordGroups.Any())
                {
                    LogService.LogInfo($"[RSSProvider] Tekrarlayan kelime filtrelendi: {item.Title} (Tekrar eden: {string.Join(", ", wordGroups.Select(g => g.Key))})");
                    continue;
                }
                
                // Duplicate kelime grupları kontrolü (yan yana aynı kelimeler)
                bool hasDuplicateWords = false;
                for (int i = 0; i < words.Length - 1; i++)
                {
                    if (words[i].Equals(words[i + 1], StringComparison.OrdinalIgnoreCase))
                    {
                        LogService.LogInfo($"[RSSProvider] Yan yana tekrarlayan kelime filtrelendi: {item.Title}");
                        hasDuplicateWords = true;
                        break;
                    }
                }
                if (hasDuplicateWords) continue;
                
                // Tüm kontrolleri geçti, haberi ekle
                filtered.Add(item);
                seenTitles.Add(normalizedTitle);
                seenHashes.Add(titleHash);
            }
            
            LogService.LogInfo($"[RSSProvider] Filtreleme sonucu: {items.Count} haberden {filtered.Count} kaldı");
            return filtered;
        }
        
        /// <summary>
        /// Başlığı normalize eder (küçük harf, gereksiz karakterler temizlenir)
        /// </summary>
        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            
            // Küçük harfe çevir
            var normalized = title.ToLowerInvariant();
            
            // Noktalama işaretlerini kaldır
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", " ");
            
            // Birden fazla boşluğu tek boşluğa çevir
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            
            return normalized.Trim();
        }
        
        /// <summary>
        /// Başlık için basit hash hesaplar (benzer kelimeleri yakalamak için)
        /// </summary>
        private int GetTitleHash(string normalizedTitle)
        {
            var words = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // İlk 5 kelimeyi al (sıralama önemli değil, benzerlik için)
            var significantWords = words.Take(5).OrderBy(w => w).ToList();
            
            return string.Join(" ", significantWords).GetHashCode();
        }

        /// <summary>
        /// Kaynak çeşitliliğini sağlayan dengeli haber seçim algoritması
        /// </summary>
        private List<RSSItem> BalanceNewsSources(List<RSSItem> allItems, int maxResults, List<RSSSource> sources)
        {
            var result = new List<RSSItem>();
            var itemsBySource = allItems.GroupBy(i => i.Source).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.PublishDate).ToList());
            
            // Her kaynaktan alınacak maksimum haber sayısı
            int maxItemsPerSource = Math.Max(3, maxResults / sources.Count);
            
            // Priority'ye göre kaynak ağırlıkları
            var sourceWeights = sources.ToDictionary(
                s => s.Name,
                s => s.Priority switch
                {
                    1 => 4, // En yüksek öncelik
                    2 => 3,
                    3 => 2,
                    _ => 1
                }
            );
            
            // İlk tur: Her kaynaktan en az 1 haber al (round-robin)
            foreach (var sourceGroup in itemsBySource.Where(kv => kv.Value.Any()))
            {
                result.Add(sourceGroup.Value.First());
                sourceGroup.Value.RemoveAt(0);
            }
            
            // İkinci tur: Priority ağırlıklarına göre kalan haberleri dağıt
            int remainingSlots = maxResults - result.Count;
            var sourceQuotas = new Dictionary<string, int>();
            
            // Her kaynağın kotasını hesapla
            int totalWeight = sourceWeights.Where(kv => itemsBySource.ContainsKey(kv.Key)).Sum(kv => kv.Value);
            foreach (var source in sourceWeights.Where(kv => itemsBySource.ContainsKey(kv.Key)))
            {
                int quota = Math.Min(
                    (int)Math.Ceiling((double)remainingSlots * source.Value / totalWeight),
                    maxItemsPerSource - 1 // İlk turda 1 haber aldık
                );
                sourceQuotas[source.Key] = quota;
            }
            
            // Kotalar dahilinde haberleri ekle
            foreach (var sourceGroup in itemsBySource)
            {
                if (sourceQuotas.TryGetValue(sourceGroup.Key, out int quota))
                {
                    var itemsToAdd = sourceGroup.Value.Take(quota).ToList();
                    result.AddRange(itemsToAdd);
                }
            }
            
            // Eğer hala yer varsa, en güncel haberlerle tamamla
            if (result.Count < maxResults)
            {
                var remainingItems = allItems.Except(result).OrderByDescending(i => i.PublishDate);
                result.AddRange(remainingItems.Take(maxResults - result.Count));
            }
            
            // Son sıralama: Tarih sırasına göre
            return result.OrderByDescending(i => i.PublishDate).Take(maxResults).ToList();
        }

        /// <summary>
        /// Levenshtein Distance algoritması ile iki string arasındaki mesafeyi hesaplar
        /// </summary>
        private int GetLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
            if (string.IsNullOrEmpty(target)) return source.Length;

            var distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost
                    );
                }
            }

            return distance[source.Length, target.Length];
        }
        
        /// <summary>
        /// Bozuk XML'i temizler ve düzeltir
        /// </summary>
        private string CleanAndFixXml(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return xml;
            
            // BOM karakterini kaldır
            if (xml.StartsWith("\uFEFF"))
            {
                xml = xml.Substring(1);
            }
            
            // Geçersiz karakterleri temizle
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
            
            // CDATA bölümlerindeki sorunları düzelt
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"<!\[CDATA\[(.+?)\]\]>", m =>
            {
                var content = m.Groups[1].Value;
                // CDATA içindeki özel karakterleri encode et
                content = content.Replace("&", "&amp;")
                                 .Replace("<", "&lt;")
                                 .Replace(">", "&gt;");
                return content;
            }, System.Text.RegularExpressions.RegexOptions.Singleline);
            
            // Kapanmamış tag'leri düzelt (basit düzeltme)
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"<([^/>]+)(?<!/)>", "<$1/>");
            
            // Çift encoded entity'leri düzelt
            xml = xml.Replace("&amp;amp;", "&amp;")
                    .Replace("&amp;lt;", "&lt;")
                    .Replace("&amp;gt;", "&gt;")
                    .Replace("&amp;quot;", "&quot;")
                    .Replace("&amp;apos;", "&apos;");
            
            return xml;
        }
        
        /// <summary>
        /// Basit XML parsing yöntemi (fallback için)
        /// </summary>
        private async Task<RSSFeed> TrySimpleXmlParsingAsync(string feedUrl, RSSSource source)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await _httpClient.GetStringAsync(feedUrl);
                
                var rssFeed = new RSSFeed
                {
                    Title = source.Name,
                    Description = "",
                    Link = feedUrl,
                    Language = "tr",
                    LastBuildDate = DateTime.Now,
                    Category = source.Category
                };
                
                // Basit regex ile item'ları bul
                var itemMatches = System.Text.RegularExpressions.Regex.Matches(response, 
                    @"<item[^>]*>(.+?)</item>", 
                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                foreach (System.Text.RegularExpressions.Match itemMatch in itemMatches.Take(20))
                {
                    var itemXml = itemMatch.Groups[1].Value;
                    
                    // Başlığı çıkar
                    var titleMatch = System.Text.RegularExpressions.Regex.Match(itemXml, @"<title[^>]*>(.+?)</title>", 
                        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // Açıklamayı çıkar
                    var descMatch = System.Text.RegularExpressions.Regex.Match(itemXml, @"<description[^>]*>(.+?)</description>", 
                        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // Link'i çıkar
                    var linkMatch = System.Text.RegularExpressions.Regex.Match(itemXml, @"<link[^>]*>(.+?)</link>", 
                        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // Tarih'i çıkar
                    var pubDateMatch = System.Text.RegularExpressions.Regex.Match(itemXml, @"<pubDate[^>]*>(.+?)</pubDate>", 
                        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    if (titleMatch.Success)
                    {
                        var rssItem = new RSSItem
                        {
                            Title = CleanHtml(titleMatch.Groups[1].Value),
                            Description = descMatch.Success ? CleanHtml(descMatch.Groups[1].Value) : "",
                            Link = linkMatch.Success ? linkMatch.Groups[1].Value.Trim() : "",
                            PublishDate = DateTime.Now,
                            Source = source.Name,
                            IsTranslated = false,
                            OriginalLanguage = null,
                            Guid = Guid.NewGuid().ToString()
                        };
                        
                        // Tarihi parse etmeye çalış
                        if (pubDateMatch.Success)
                        {
                            if (DateTime.TryParse(pubDateMatch.Groups[1].Value, out var pubDate))
                            {
                                rssItem.PublishDate = pubDate;
                            }
                        }
                        
                        rssFeed.Items.Add(rssItem);
                    }
                }
                
                if (rssFeed.Items.Any())
                {
                    LogService.LogDebug($"[RSSProvider] Basit parser ile {rssFeed.Items.Count} haber çekildi");
                    return rssFeed;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[RSSProvider] Basit XML parsing başarısız: {ex.Message}");
                return null;
            }
        }
    }
}