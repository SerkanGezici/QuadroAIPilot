using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuadroAIPilot.Configuration;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Models;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services;
using QuadroAIPilot.Services.WebServices;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Command for web information retrieval (Wikipedia, news, trends)
    /// </summary>
    public class WebInfoCommand : ISystemCommand
    {
        private readonly IWebContentService _webService;
        private readonly List<string> _supportedCommands;

        public WebInfoCommand(IWebContentService webService = null)
        {
            _webService = webService ?? ServiceContainer.GetOptionalService<IWebContentService>() ?? new WebContentService();
            _supportedCommands = InitializeSupportedCommands();
        }

        private List<string> InitializeSupportedCommands()
        {
            return new List<string>
            {
                // Fiil bazlı haber komutları (kullanıcının istediği format)
                "haberlerini oku", "haberleri oku", "haberlerini göster", "haberleri göster",
                "haberlerini getir", "haberleri getir", "haberlerini listele", "haberleri listele",
                "haberlerde neler var", "haberlerde ne var", "haberlerinde neler var", "haberlerinde ne var",
                
                // Kategori + fiil bazlı komutlar - TÜM KATEGORİLER İÇİN OKU DESTEĞİ
                "spor haberlerini oku", "spor haberleri oku", "spor haberlerini göster", "spor haberleri göster",
                "ekonomi haberlerini oku", "ekonomi haberleri oku", "ekonomi haberlerini göster", "ekonomi haberleri göster",
                "teknoloji haberlerini oku", "teknoloji haberleri oku", "teknoloji haberlerini göster", "teknoloji haberleri göster",
                "sağlık haberlerini oku", "sağlık haberleri oku", "sağlık haberlerini göster", "sağlık haberleri göster",
                "dünya haberlerini oku", "dünya haberleri oku", "dünya haberlerini göster", "dünya haberleri göster",
                "magazin haberlerini oku", "magazin haberleri oku", "magazin haberlerini göster", "magazin haberleri göster",
                "siyaset haberlerini oku", "siyaset haberleri oku", "siyaset haberlerini göster", "siyaset haberleri göster",
                "finans haberlerini oku", "finans haberleri oku", "finans haberlerini göster", "finans haberleri göster",
                "borsa haberlerini oku", "borsa haberleri oku", "borsa haberlerini göster", "borsa haberleri göster",
                "bilim haberlerini oku", "bilim haberleri oku", "bilim haberlerini göster", "bilim haberleri göster",
                "kültür haberlerini oku", "kültür haberleri oku", "kültür haberlerini göster", "kültür haberleri göster",
                "sanat haberlerini oku", "sanat haberleri oku", "sanat haberlerini göster", "sanat haberleri göster",
                "eğitim haberlerini oku", "eğitim haberleri oku", "eğitim haberlerini göster", "eğitim haberleri göster",
                "otomobil haberlerini oku", "otomobil haberleri oku", "otomobil haberlerini göster", "otomobil haberleri göster",
                "emlak haberlerini oku", "emlak haberleri oku", "emlak haberlerini göster", "emlak haberleri göster",
                "turizm haberlerini oku", "turizm haberleri oku", "turizm haberlerini göster", "turizm haberleri göster",
                "güncel haberlerini oku", "güncel haberleri oku", "güncel haberlerini göster", "güncel haberleri göster",
                "yerel haberlerini oku", "yerel haberleri oku", "yerel haberlerini göster", "yerel haberleri göster",
                
                // Genel haber sorguları
                "haberlerde ne var", "neler oluyor", "gündemde neler var", "son haberler neler",
                "bugünkü haberler", "en son haberler", "güncel haberler",
                
                // Wikipedia bilgi sorguları (fiil bazlı)
                "nedir", "kimdir", "ne demek", "hakkında bilgi ver", "açıkla",
                "vikipedi'de ara", "wikipedia'da ara",
                
                // Twitter/X gündem
                "twitter gündem", "twitter trendleri", "x gündem", "x trendleri",
                "gündemde neler var", "trendler neler", "popüler konular",
                
                // Kombine istekler
                "haberler ve gündem", "gündem ve haberler", "son haberler ve trendler"
            };
        }

        public bool CanHandle(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;

            var lowerCommand = command.ToLowerInvariant().Trim();
            
            // Direk pattern matching
            var directMatch = _supportedCommands.Any(cmd => lowerCommand.Contains(cmd));
            if (directMatch) return true;
            
            // Gelişmiş pattern matching
            var words = lowerCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Haber kombinasyonları
            if ((words.Contains("en") && words.Contains("son") && words.Contains("haberler")) ||
                (words.Contains("bugünkü") && words.Contains("haberler")) ||
                (words.Contains("gündemde") && words.Contains("neler")) ||
                (words.Contains("neler") && words.Contains("oluyor")))
            {
                return true;
            }
            
            // Kategori bazlı haber komutları için daha esnek kontrol
            var newsCategories = new[] { "spor", "ekonomi", "teknoloji", "sağlık", "dünya", "magazin", "siyaset", "finans", "borsa", "bilim", "kültür", "sanat", "eğitim", "otomobil", "emlak", "turizm", "güncel", "yerel" };
            var newsKeywords = new[] { "haberleri", "haberlerini", "haber", "haberler", "haberlerde", "haberlerinden", "haberlerinde" };
            var newsActions = new[] { "aç", "göster", "oku", "getir", "listele", "bul", "ara", "söyle", "anlat" };
            
            // "spor haberleri oku", "ekonomi haberlerini aç" gibi varyasyonları yakala
            foreach (var category in newsCategories)
            {
                if (words.Contains(category))
                {
                    foreach (var keyword in newsKeywords)
                    {
                        if (words.Contains(keyword))
                        {
                            return true;
                        }
                    }
                    // Sadece kategori + eylem kombinasyonunu da kontrol et ("ekonomi aç" gibi)
                    foreach (var action in newsActions)
                    {
                        if (words.Contains(action))
                        {
                            return true;
                        }
                    }
                }
            }
            
            // Bilgi soruları
            if ((words.Contains("nedir") || words.Contains("kimdir") || 
                 words.Contains("ne") && words.Contains("demek")) ||
                (words.Contains("hakkında") && words.Contains("bilgi")))
            {
                return true;
            }
            
            // Twitter/X trendleri
            if ((words.Contains("twitter") || words.Contains("x")) && 
                (words.Contains("gündem") || words.Contains("trend")))
            {
                return true;
            }
            
            // Kategori + haber kombinasyonları
            var categories = new[] { "teknoloji", "spor", "ekonomi", "sağlık", "dünya", "magazin", "siyaset" };
            if (categories.Any(cat => words.Contains(cat)) && 
                (words.Contains("haber") || words.Contains("haberler")))
            {
                return true;
            }
            
            return false;
        }

        public async Task<CommandResponse> ExecuteAsync(CommandContext context)
        {
            try
            {

                var lowerCommand = context.RawCommand.ToLowerInvariant();
                
                // "En son haberler" komutu hem haberleri hem trendleri göstersin
                if (lowerCommand.Contains("son haberler") || lowerCommand.Contains("en son haberler") ||
                    (lowerCommand.Contains("haberler") && lowerCommand.Contains("neler")))
                {
                    return await HandleCombinedNewsAndTrends(context.RawCommand);
                }

                var request = ParseCommandToRequest(context.RawCommand);
                
                // Check if it's a trend-specific request
                if (IsTrendRequest(context.RawCommand))
                {
                    return await HandleTrendRequest(request);
                }

                // Get content from appropriate provider
                var content = await _webService.GetContentAsync(request);

                if (content == null)
                {
                    return new CommandResponse
                    {
                        IsSuccess = false,
                        Message = "İstediğiniz içerik bulunamadı.",
                        VoiceOutput = "Üzgünüm, istediğiniz içerik bulunamadı."
                    };
                }

                // Format response based on content type
                var response = FormatContentResponse(content);
                return response;
            }
            catch (Exception ex)
            {
                return new CommandResponse
                {
                    IsSuccess = false,
                    Message = $"Web içeriği alınırken hata oluştu: {ex.Message}",
                    VoiceOutput = "Web içeriği alınırken bir hata oluştu."
                };
            }
        }

        private ContentRequest ParseCommandToRequest(string command)
        {
            var request = new ContentRequest
            {
                Query = command,
                MaxResults = 10,
                Language = "tr"
            };

            var lowerCommand = command.ToLowerInvariant();

            // Determine content type preference
            if (lowerCommand.Contains("vikipedi") || lowerCommand.Contains("nedir") || 
                lowerCommand.Contains("kimdir") || lowerCommand.Contains("ne demek"))
            {
                request.PreferredType = ContentType.Wikipedia;
            }
            else if (lowerCommand.Contains("haber") || lowerCommand.Contains("gündem") ||
                     lowerCommand.Contains("son dakika"))
            {
                request.PreferredType = ContentType.News;
            }
            else if (lowerCommand.Contains("twitter") || lowerCommand.Contains("trend"))
            {
                request.PreferredType = ContentType.TwitterTrend;
            }
            
            // Kategori bazlı haber filtreleme
            if (lowerCommand.Contains("ekonomi") || lowerCommand.Contains("borsa") || lowerCommand.Contains("dolar") || lowerCommand.Contains("euro"))
            {
                request.Parameters["category"] = "economy"; // Türkçe ekonomi haberleri için
            }
            else if (lowerCommand.Contains("business") || lowerCommand.Contains("finans") || lowerCommand.Contains("iş dünyası"))
            {
                request.Parameters["category"] = "business"; // İngilizce business haberleri için
            }
            else if (lowerCommand.Contains("spor"))
            {
                request.Parameters["category"] = "sports";
            }
            else if (lowerCommand.Contains("teknoloji"))
            {
                request.Parameters["category"] = "technology";
            }
            else if (lowerCommand.Contains("sağlık"))
            {
                request.Parameters["category"] = "health";
            }
            else if (lowerCommand.Contains("magazin"))
            {
                request.Parameters["category"] = "entertainment";
            }
            else if (lowerCommand.Contains("dünya") || lowerCommand.Contains("uluslararası"))
            {
                request.Parameters["category"] = "world";
            }
            else if (lowerCommand.Contains("siyaset") || lowerCommand.Contains("politika"))
            {
                request.Parameters["category"] = "politics";
            }

            // Add summarization flag for long content
            if (lowerCommand.Contains("özet") || lowerCommand.Contains("kısa"))
            {
                request.Parameters["summarize"] = true;
            }

            // Extract date filters
            if (lowerCommand.Contains("bugün"))
            {
                request.DateFrom = DateTime.Today;
                request.DateTo = DateTime.Now;
            }
            else if (lowerCommand.Contains("dün"))
            {
                request.DateFrom = DateTime.Today.AddDays(-1);
                request.DateTo = DateTime.Today;
            }
            else if (lowerCommand.Contains("bu hafta"))
            {
                request.DateFrom = DateTime.Today.AddDays(-7);
                request.DateTo = DateTime.Now;
            }

            return request;
        }

        private bool IsTrendRequest(string command)
        {
            var lowerCommand = command.ToLowerInvariant();
            // Twitter veya X ile gündem/trend kelimelerini kontrol et
            return ((lowerCommand.Contains("twitter") || lowerCommand.Contains("x")) && 
                    (lowerCommand.Contains("gündem") || lowerCommand.Contains("trend")));
        }

        private async Task<CommandResponse> HandleTrendRequest(ContentRequest request)
        {
            try
            {
                var trends = await _webService.GetTrendsAsync("turkey");
                
                if (trends == null || !trends.Any())
                {
                    return new CommandResponse
                    {
                        IsSuccess = false,
                        Message = "Twitter gündem konuları alınamadı.",
                        VoiceOutput = "Twitter gündem konuları şu anda alınamıyor."
                    };
                }

                // İlk 10 trend'i göster
                var topTrends = trends.Take(10).ToList();

                var html = FormatTrendsHtml(topTrends);
                var voice = FormatTrendsVoice(topTrends); // Tüm 10 trend'i seslendir

                return new CommandResponse
                {
                    IsSuccess = true,
                    HtmlContent = html,
                    VoiceOutput = voice,
                    ActionType = CommandActionType.ShowHtml
                };
            }
            catch
            {
                throw;
            }
        }

        private async Task<CommandResponse> HandleCombinedNewsAndTrends(string command)
        {
            try
            {
                
                // Kullanıcı tercihlerini al
                var configService = ServiceContainer.GetOptionalService<ConfigurationService>();
                var userPreferences = configService?.User.NewsPreferences;
                
                // Haberleri al
                var newsRequest = new ContentRequest
                {
                    Query = command,
                    PreferredType = ContentType.News,
                    MaxResults = 10
                };
                
                // Kullanıcı tercihlerine göre kategorileri belirle
                if (userPreferences != null)
                {
                    var allSelectedCategories = new List<string>(userPreferences.SelectedCategories);
                    if (!string.IsNullOrEmpty(userPreferences.CustomCategory))
                    {
                        allSelectedCategories.Add(userPreferences.CustomCategory);
                    }
                    
                    // Kategori parametrelerini ekle
                    newsRequest.Parameters["preferredCategories"] = string.Join(",", allSelectedCategories);
                }
                
                var newsTask = _webService.GetContentAsync(newsRequest);
                var trendsTask = _webService.GetTrendsAsync("turkey");
                
                // İkisini paralel çek
                await Task.WhenAll(newsTask, trendsTask);
                
                var news = await newsTask;
                var trends = await trendsTask;
                
                // Haberleri NewsMemoryService'e kaydet
                if (news != null)
                {
                    LogService.LogInfo($"[WebInfoCommand] HandleCombinedNewsAndTrends - haber kaydediliyor");
                    StoreNewsItemsFromContent(news);
                }
                
                // Haberleri kullanıcı tercihlerine göre filtrele ve sırala
                if (news != null && userPreferences != null)
                {
                    news = FilterNewsByUserPreferences(news, userPreferences);
                }
                
                // HTML formatla
                var html = FormatCombinedHtml(news, trends);
                var voice = FormatCombinedVoice(news, trends);
                
                return new CommandResponse
                {
                    IsSuccess = true,
                    HtmlContent = html,
                    VoiceOutput = voice,
                    ActionType = CommandActionType.ShowHtml
                };
            }
            catch
            {
                return new CommandResponse
                {
                    IsSuccess = false,
                    Message = "Haberler ve trendler alınırken hata oluştu.",
                    VoiceOutput = "Haberler ve trendler alınırken bir hata oluştu."
                };
            }
        }

        private CommandResponse FormatContentResponse(WebContent content)
        {
            string html;
            string voice;
            string message = "";

            LogService.LogInfo($"[WebInfoCommand] FormatContentResponse çağrıldı. Content.Type: {content.Type}");

            switch (content.Type)
            {
                case ContentType.Wikipedia:
                    html = FormatWikipediaHtml(content);
                    voice = FormatWikipediaVoice(content);
                    message = $"{content.Title}\n\n{content.Summary ?? content.Content}";
                    break;

                case ContentType.News:
                case ContentType.RSS:
                    Debug.WriteLine($"[WebInfoCommand] ContentType.RSS case - HTML formatlanıyor");
                    html = FormatNewsHtml(content);
                    
                    Debug.WriteLine($"[WebInfoCommand] FormatNewsVoice çağrılıyor...");
                    voice = FormatNewsVoice(content);
                    Debug.WriteLine($"[WebInfoCommand] FormatNewsVoice tamamlandı - Voice null mu: {voice == null}");
                    Debug.WriteLine($"[WebInfoCommand] Voice içeriği uzunluğu: {voice?.Length ?? 0} karakter");
                    if (!string.IsNullOrEmpty(voice))
                    {
                        Debug.WriteLine($"[WebInfoCommand] Voice içeriği ilk 100 karakter: '{voice.Substring(0, Math.Min(100, voice.Length))}'");
                    }
                    
                    message = FormatNewsText(content);
                    
                    // Haberleri NewsMemoryService'e kaydet
                    StoreNewsItemsFromContent(content);
                    break;

                case ContentType.TwitterTrend:
                    html = FormatTrendContentHtml(content);
                    voice = content.Summary ?? "Twitter gündem konuları gösteriliyor.";
                    message = content.Content ?? content.Summary ?? "Twitter gündem konuları";
                    break;

                default:
                    html = FormatGenericHtml(content);
                    voice = content.Summary ?? content.Title;
                    message = content.Content ?? content.Summary ?? content.Title;
                    break;
            }

            // ÇÖZÜM: Browser Extension TTS sistemini kullan
            if (!string.IsNullOrEmpty(voice))
            {
                Debug.WriteLine($"[WebInfoCommand] TTS başlatılıyor - Voice uzunluğu: {voice.Length}");
                
                try
                {
                    // TextToSpeechService'i kullanarak TTS çal (Browser Extension sistemi)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await TextToSpeechService.SpeakTextAsync(voice);
                            Debug.WriteLine($"[WebInfoCommand] TTS başarıyla tamamlandı");
                        }
                        catch (Exception ttsEx)
                        {
                            Debug.WriteLine($"[WebInfoCommand] TTS hatası: {ttsEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebInfoCommand] TTS başlatma hatası: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[WebInfoCommand] Voice boş - TTS çalıştırılmıyor");
            }

            return new CommandResponse
            {
                IsSuccess = true,
                Message = message,
                HtmlContent = html,
                VoiceOutput = voice,
                ActionType = CommandActionType.ShowHtml,
                Data = content
            };
        }

        private string FormatWikipediaHtml(WebContent content)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='tr'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>Wikipedia - " + System.Net.WebUtility.HtmlEncode(content.Title) + "</title>");
            html.AppendLine(GetCommonStyles());
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<div class='container'>");
            html.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(content.Title)}</h1>");
            
            if (content.Tags?.Any() == true)
            {
                html.AppendLine("<div class='categories'>");
                foreach (var tag in content.Tags.Take(5))
                {
                    html.AppendLine($"<span class='category-badge'>{System.Net.WebUtility.HtmlEncode(tag)}</span>");
                }
                html.AppendLine("</div>");
            }

            // Convert markdown-style content to HTML
            var htmlContent = ConvertMarkdownToHtml(content.Content);
            html.AppendLine($"<div class='content'>{htmlContent}</div>");

            if (!string.IsNullOrEmpty(content.SourceUrl))
            {
                html.AppendLine($"<div class='source'><a href='{content.SourceUrl}' target='_blank'>Wikipedia'da Oku →</a></div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string FormatNewsHtml(WebContent content)
        {
            var html = new StringBuilder();
            
            // Sadece içerik HTML'i döndür, tam doküman değil
            html.AppendLine("<div class='news-container' style='padding: 20px; background-color: transparent; border-radius: 8px; margin: 10px 0;'>");
            html.AppendLine($"<h2 style='color: #2c3e50; margin-bottom: 20px;'>{System.Net.WebUtility.HtmlEncode(content.Title)}</h2>");
            
            // RSSItem listesini al
            List<RSSItem> newsItems = null;
            if (content.Metadata != null && content.Metadata.ContainsKey("RSSItems"))
            {
                newsItems = content.Metadata["RSSItems"] as List<RSSItem>;
            }
            else
            {
                if (content.Metadata != null)
                {
                    // Metadata keys available but no RSSItems
                }
            }
            
            // Özel haber HTML'i oluştur (tıklanabilir linklerle)
            string htmlContent;
            if (newsItems != null && newsItems.Any())
            {
                htmlContent = ConvertNewsToHtml(content.Content, newsItems);
            }
            else
            {
                // RSSItems yoksa normal markdown dönüşümü
                htmlContent = ConvertMarkdownToHtml(content.Content);
            }
            
            html.AppendLine($"<div class='news-content'>{htmlContent}</div>");

            html.AppendLine("</div>"); // news-container kapanışı

            return html.ToString();
        }

        private string FormatTrendsHtml(List<TrendingTopic> trends)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='tr'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>Twitter Gündem</title>");
            html.AppendLine(GetCommonStyles());
            html.AppendLine(GetTrendStyles());
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<div class='container'>");
            html.AppendLine("<h1>🔥 X (Twitter) Gündem Konuları</h1>");
            
            var groupedTrends = trends.GroupBy(t => t.Category).OrderBy(g => g.Key);
            
            foreach (var group in groupedTrends)
            {
                html.AppendLine($"<div class='trend-category'>");
                html.AppendLine($"<h2>{GetCategoryIcon(group.Key)} {GetCategoryDisplayName(group.Key)}</h2>");
                html.AppendLine("<div class='trend-list'>");
                
                foreach (var trend in group.OrderBy(t => t.Rank))
                {
                    html.AppendLine("<div class='trend-item'>");
                    html.AppendLine($"<span class='trend-rank'>{trend.Rank}</span>");
                    html.AppendLine($"<span class='trend-name'>{System.Net.WebUtility.HtmlEncode(trend.DisplayName)}</span>");
                    
                    if (trend.TweetVolume.HasValue)
                    {
                        html.AppendLine($"<span class='trend-volume'>{FormatNumber(trend.TweetVolume.Value)} tweet</span>");
                    }
                    
                    html.AppendLine("</div>");
                }
                
                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string FormatTrendContentHtml(WebContent content)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='tr'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>" + System.Net.WebUtility.HtmlEncode(content.Title) + "</title>");
            html.AppendLine(GetCommonStyles());
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<div class='container'>");
            var htmlContent = ConvertMarkdownToHtml(content.Content);
            html.AppendLine(htmlContent);
            html.AppendLine("</div>");
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string FormatGenericHtml(WebContent content)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='tr'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>" + System.Net.WebUtility.HtmlEncode(content.Title) + "</title>");
            html.AppendLine(GetCommonStyles());
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<div class='container'>");
            html.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(content.Title)}</h1>");
            
            if (!string.IsNullOrEmpty(content.Summary))
            {
                html.AppendLine($"<div class='summary'>{System.Net.WebUtility.HtmlEncode(content.Summary)}</div>");
            }

            var htmlContent = ConvertMarkdownToHtml(content.Content);
            html.AppendLine($"<div class='content'>{htmlContent}</div>");

            if (!string.IsNullOrEmpty(content.Source))
            {
                html.AppendLine($"<div class='source'>Kaynak: {System.Net.WebUtility.HtmlEncode(content.Source)}</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string FormatWikipediaVoice(WebContent content)
        {
            var voice = new StringBuilder();
            voice.Append($"{content.Title}. ");
            
            if (!string.IsNullOrEmpty(content.Summary))
            {
                voice.Append(content.Summary);
            }
            else
            {
                // Get first paragraph
                var firstParagraph = content.Content.Split('\n').FirstOrDefault(p => p.Length > 50);
                if (!string.IsNullOrEmpty(firstParagraph))
                {
                    voice.Append(firstParagraph);
                }
            }

            return voice.ToString();
        }

        private string FormatNewsText(WebContent content)
        {
            var text = new StringBuilder();
            
            // Kategori bilgisini al
            string category = "";
            if (content.Metadata != null)
            {
                if (content.Metadata.ContainsKey("category"))
                {
                    category = content.Metadata["category"].ToString();
                }
                else if (content.Metadata.ContainsKey("Category"))
                {
                    category = content.Metadata["Category"].ToString();
                }
            }
            
            // RSSCategory enum'ından string'e çevir
            if (category.Contains("RSSCategory"))
            {
                category = category.Replace("RSSCategory.", "").ToLowerInvariant();
            }
            
            // Başlık
            string categoryInTurkish = GetCategoryNameInTurkish(category);
            if (!string.IsNullOrEmpty(categoryInTurkish))
            {
                text.AppendLine($"=== EN SON {categoryInTurkish.ToUpperInvariant()} HABERLERİ ===");
            }
            else
            {
                text.AppendLine("=== EN SON HABERLER ===");
            }
            text.AppendLine();
            
            // Haberleri formatla - kaynak bilgileri ile birlikte
            var lines = content.Content.Split('\n');
            var newsIndex = 1;
            var currentNews = new StringBuilder();
            
            foreach (var line in lines)
            {
                if (line.Contains("**"))
                {
                    // Önceki haberi ekle
                    if (currentNews.Length > 0)
                    {
                        text.Append(currentNews.ToString());
                        text.AppendLine();
                        newsIndex++;
                    }
                    
                    // Yeni haber başla
                    currentNews.Clear();
                    var title = line.Replace("**", "").Replace("📰", "").Trim();
                    currentNews.AppendLine($"{newsIndex}. {title}");
                }
                else if (line.Contains("🔗 Kaynak:"))
                {
                    // Kaynak bilgisini ekle
                    var sourceLine = line.Replace("🔗", "").Trim();
                    currentNews.AppendLine($"   {sourceLine}");
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.Contains("⏰"))
                {
                    // Açıklama satırı
                    currentNews.AppendLine($"   {line.Trim()}");
                }
            }
            
            // Son haberi ekle
            if (currentNews.Length > 0)
            {
                text.Append(currentNews.ToString());
            }
            
            return text.ToString();
        }

        private string FormatNewsVoice(WebContent content)
        {
            Debug.WriteLine($"[FormatNewsVoice] Başlatıldı");
            Debug.WriteLine($"[FormatNewsVoice] content null mu: {content == null}");
            Debug.WriteLine($"[FormatNewsVoice] content.Metadata null mu: {content?.Metadata == null}");
            
            var voice = new StringBuilder();
            
            // Önce metadata'dan hazır TLS içeriğini kontrol et
            if (content.Metadata != null && content.Metadata.ContainsKey("TTSContent"))
            {
                Debug.WriteLine($"[FormatNewsVoice] TTSContent key bulundu");
                var ttsContent = content.Metadata["TTSContent"].ToString();
                Debug.WriteLine($"[FormatNewsVoice] TTSContent değeri null mu: {ttsContent == null}");
                Debug.WriteLine($"[FormatNewsVoice] TTSContent uzunluğu: {ttsContent?.Length ?? 0}");
                
                if (!string.IsNullOrEmpty(ttsContent))
                {
                    Debug.WriteLine($"[FormatNewsVoice] TTSContent dolu, işleniyor...");
                    Debug.WriteLine($"[FormatNewsVoice] TTSContent ilk 50 karakter: '{ttsContent.Substring(0, Math.Min(50, ttsContent.Length))}'");
                    
                    // HTML etiketlerini temizle
                    ttsContent = System.Text.RegularExpressions.Regex.Replace(ttsContent, @"<[^>]*>", "");
                    ttsContent = System.Net.WebUtility.HtmlDecode(ttsContent);
                    
                    // Kategori bilgisini ekle
                    string category = "";
                    if (content.Metadata.ContainsKey("category"))
                    {
                        category = content.Metadata["category"].ToString();
                    }
                    else if (content.Metadata.ContainsKey("Category"))
                    {
                        category = content.Metadata["Category"].ToString();
                    }
                    
                    // RSSCategory enum'ından string'e çevir
                    if (category.Contains("RSSCategory"))
                    {
                        category = category.Replace("RSSCategory.", "").ToLowerInvariant();
                    }
                    
                    // Kategori bazlı anons
                    string categoryInTurkish = GetCategoryNameInTurkish(category);
                    if (!string.IsNullOrEmpty(categoryInTurkish))
                    {
                        return $"En son {categoryInTurkish} haberleri. {ttsContent}";
                    }
                    else
                    {
                        return $"En son haberler. {ttsContent}";
                    }
                }
            }
            
            // Eğer TTSContent yoksa, content.Content'ten temiz metin oluştur
            if (!string.IsNullOrEmpty(content.Content))
            {
                
                // HTML etiketlerini temizle
                var cleanText = System.Text.RegularExpressions.Regex.Replace(content.Content, @"<[^>]*>", "");
                cleanText = System.Net.WebUtility.HtmlDecode(cleanText);
                
                // Kategori bilgisini metadata'dan al
                string category = "";
                if (content.Metadata != null)
                {
                    if (content.Metadata.ContainsKey("category"))
                    {
                        category = content.Metadata["category"].ToString();
                    }
                    else if (content.Metadata.ContainsKey("Category"))
                    {
                        category = content.Metadata["Category"].ToString();
                    }
                }
                
                // RSSCategory enum'ından string'e çevir
                if (category.Contains("RSSCategory"))
                {
                    category = category.Replace("RSSCategory.", "").ToLowerInvariant();
                }
                
                // Kategori bazlı anons
                string categoryInTurkish = GetCategoryNameInTurkish(category);
                if (!string.IsNullOrEmpty(categoryInTurkish))
                {
                    voice.Append($"En son {categoryInTurkish} haberleri. ");
                }
                else
                {
                    voice.Append("En son haberler. ");
                }
                
                voice.Append(cleanText);
                Debug.WriteLine($"[FormatNewsVoice] Content.Content'ten voice oluşturuldu - Uzunluk: {voice.Length}");
                var result = voice.ToString();
                Debug.WriteLine($"[FormatNewsVoice] Return edilen voice ilk 50 karakter: '{result.Substring(0, Math.Min(50, result.Length))}'");
                return result;
            }
            
            // Hiçbir içerik yoksa fallback
            Debug.WriteLine($"[FormatNewsVoice] Hiçbir TTSContent ve Content bulunamadı - fallback kullanılıyor");
            voice.AppendLine("Haber detayları yüklenemedi.");
            var fallbackResult = voice.ToString();
            Debug.WriteLine($"[FormatNewsVoice] Fallback voice: '{fallbackResult}'");
            return fallbackResult;
        }

        private string FormatTrendsVoice(List<TrendingTopic> trends)
        {
            var voice = new StringBuilder();
            voice.AppendLine("X gündem konuları:");
            
            foreach (var trend in trends)
            {
                voice.AppendLine($"{trend.Rank}. {trend.DisplayName}...");
            }

            return voice.ToString();
        }

        private string ConvertMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return "";

            // Haberler için özel işlem
            if (markdown.Contains("Kaynak:") && (markdown.Contains("HABERLERİ") || markdown.Contains("Haber")))
            {
                return ConvertNewsToHtmlWithLinks(markdown);
            }

            // HTML encode YAPMA - zaten güvenli HTML oluşturuyoruz
            var html = markdown;

            // Headers
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Bold
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

            // Line breaks
            html = html.Replace("\n\n", "</p><p>");
            html = html.Replace("\n", "<br>");
            html = $"<p>{html}</p>";

            // Links
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\[([^\]]+)\]\(([^)]+)\)", "<a href='$2' target='_blank'>$1</a>");

            // Emojis are already in the text
            
            return html;
        }

        /// <summary>
        /// Haber içeriğini linkli HTML'e dönüştürür
        /// </summary>
        private string ConvertNewsToHtmlWithLinks(string content)
        {
            var html = new StringBuilder();
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    html.AppendLine("<br>");
                    continue;
                }
                
                // Başlık satırı (7., 8., 9. gibi)
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\d+)\.\s+(.+)$");
                if (match.Success)
                {
                    var newsNumber = match.Groups[1].Value;
                    var newsTitle = match.Groups[2].Value;
                    
                    // Başlığı tıklanabilir yap - basit bir Google araması linki
                    var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(newsTitle)}";
                    html.AppendLine($"<p><strong>{newsNumber}.</strong> <a href='{searchUrl}' target='_blank' style='color: #0066cc; text-decoration: underline; cursor: pointer;'>{System.Net.WebUtility.HtmlEncode(newsTitle)}</a></p>");
                }
                else if (line.TrimStart().StartsWith("Kaynak:"))
                {
                    // Kaynak satırı
                    html.AppendLine($"<p style='color: #666; font-size: 0.9em; margin-left: 20px;'>{System.Net.WebUtility.HtmlEncode(line.Trim())}</p>");
                }
                else
                {
                    // Normal satır
                    html.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(line.Trim())}</p>");
                }
            }
            
            return html.ToString();
        }
        
        /// <summary>
        /// Basit haber HTML dönüşümü - her habere tıklanabilir buton ekler
        /// </summary>
        private string ConvertNewsToHtmlSimple(string content)
        {
            var html = new StringBuilder();
            var lines = content.Split('\n');
            var newsIndex = 0;
            bool inNewsItem = false;
            string currentTitle = "";

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inNewsItem)
                    {
                        // Haber sonunda buton ekle
                        html.AppendLine($"<div style='margin: 10px 0 20px 20px;'>");
                        html.AppendLine($"<button onclick='window.chrome.webview.postMessage(JSON.stringify({{\"action\":\"openNewsDetail\",\"index\":{newsIndex},\"title\":\"{System.Net.WebUtility.HtmlEncode(currentTitle)}\"}})' style='background-color: #0066cc; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 14px;'>📰 Haberin Detayını Göster</button>");
                        html.AppendLine("</div>");
                        inNewsItem = false;
                    }
                    html.AppendLine("<br>");
                    continue;
                }

                // Başlık satırı
                if (line.Contains("==="))
                {
                    html.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(line)}</h3>");
                }
                // Haber numarası ile başlayan satırlar
                else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s+"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\d+)\.\s+(.+)$");
                    if (match.Success)
                    {
                        newsIndex = int.Parse(match.Groups[1].Value);
                        currentTitle = match.Groups[2].Value;
                        html.AppendLine($"<p><strong style='color: #0066cc; text-decoration: underline;'>{System.Net.WebUtility.HtmlEncode(line)}</strong></p>");
                        inNewsItem = true;
                    }
                }
                else
                {
                    // Normal satır
                    html.AppendLine($"<p style='margin-left: 20px;'>{System.Net.WebUtility.HtmlEncode(line)}</p>");
                }
            }

            // Son haber için buton ekle
            if (inNewsItem)
            {
                html.AppendLine($"<div style='margin: 10px 0 20px 20px;'>");
                html.AppendLine($"<button onclick='window.chrome.webview.postMessage(JSON.stringify({{\"action\":\"openNewsDetail\",\"index\":{newsIndex},\"title\":\"{System.Net.WebUtility.HtmlEncode(currentTitle)}\"}})' style='background-color: #0066cc; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 14px;'>📰 Haberin Detayını Göster</button>");
                html.AppendLine("</div>");
            }

            return html.ToString();
        }

        /// <summary>
        /// Özel formatlanmış haber HTML'i oluşturur (tıklanabilir linklerle)
        /// </summary>
        private string ConvertNewsToHtml(string content, List<RSSItem> newsItems)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            var html = new StringBuilder();
            var lines = content.Split('\n');
            var currentNewsIndex = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    html.AppendLine("<br>");
                    continue;
                }

                // Haber numarası ile başlayan satırları tespit et (1. , 2. , vs.)
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\d+)\.\s+(.+)$");
                if (match.Success)
                {
                    var newsNumber = match.Groups[1].Value;
                    var newsTitle = match.Groups[2].Value;
                    
                    // NewsItems'dan ilgili haberi bul
                    if (newsItems != null && currentNewsIndex < newsItems.Count)
                    {
                        var newsItem = newsItems[currentNewsIndex];
                        
                        if (!string.IsNullOrEmpty(newsItem.Link))
                        {
                            // Tıklanabilir link olarak formatla - data-href kullan
                            var encodedLink = System.Net.WebUtility.HtmlEncode(newsItem.Link);
                            var linkHtml = $"<p><strong>{newsNumber}.</strong> <a href='{encodedLink}' data-href='{encodedLink}' class='news-link' style='color: #0066cc !important; text-decoration: underline !important; font-weight: bold; cursor: pointer; display: inline-block;' onmouseover='this.style.color=\"#0052cc\"' onmouseout='this.style.color=\"#0066cc\"'>{System.Net.WebUtility.HtmlEncode(newsTitle)}</a></p>";
                            html.AppendLine(linkHtml);
                        }
                        else
                        {
                            // Link yoksa normal başlık
                            html.AppendLine($"<p><strong>{newsNumber}. {System.Net.WebUtility.HtmlEncode(newsTitle)}</strong></p>");
                        }
                        currentNewsIndex++;
                    }
                    else
                    {
                        // NewsItem bulunamazsa normal başlık
                        html.AppendLine($"<p><strong>{System.Net.WebUtility.HtmlEncode(line)}</strong></p>");
                    }
                }
                else if (line.TrimStart().StartsWith("Kaynak:"))
                {
                    // Kaynak satırı
                    html.AppendLine($"<p style='color: #666; font-size: 0.9em; margin-left: 20px;'>{System.Net.WebUtility.HtmlEncode(line.Trim())}</p>");
                }
                else
                {
                    // Açıklama satırı
                    html.AppendLine($"<p style='margin-left: 20px;'>{System.Net.WebUtility.HtmlEncode(line.Trim())}</p>");
                }
            }

            return html.ToString();
        }

        private string GetCommonStyles()
        {
            return @"
<style>
    body {
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
        margin: 0;
        padding: 0;
        background-color: transparent;
        color: var(--text-primary, #333);
        line-height: 1.6;
    }
    .container {
        max-width: 800px;
        margin: 0 auto;
        padding: 20px;
        background-color: transparent;
        min-height: 100vh;
    }
    h1 {
        color: var(--primary-color, #2c3e50);
        margin-bottom: 20px;
        font-size: 28px;
    }
    h2 {
        color: var(--primary-color, #34495e);
        margin-top: 30px;
        margin-bottom: 15px;
        font-size: 22px;
        opacity: 0.9;
    }
    h3 {
        color: var(--text-primary, #555);
        margin-top: 20px;
        margin-bottom: 10px;
        font-size: 18px;
    }
    .content {
        font-size: 16px;
        line-height: 1.8;
    }
    .summary {
        background-color: rgba(232, 244, 248, 0.1);
        backdrop-filter: blur(10px);
        padding: 15px;
        border-radius: 8px;
        margin-bottom: 20px;
        border-left: 4px solid var(--primary-color, #3498db);
    }
    .categories {
        margin-bottom: 20px;
    }
    .category-badge {
        display: inline-block;
        background-color: var(--primary-color, #3498db);
        color: white;
        padding: 4px 12px;
        border-radius: 16px;
        font-size: 14px;
        margin-right: 8px;
        margin-bottom: 8px;
    }
    .source {
        margin-top: 30px;
        padding-top: 20px;
        border-top: 1px solid var(--border-color, rgba(221, 221, 221, 0.3));
        font-size: 14px;
        color: #666;
    }
    .source a {
        color: #3498db;
        text-decoration: none;
    }
    .source a:hover {
        text-decoration: underline;
    }
    a {
        color: #0066cc !important;
        text-decoration: underline !important;
        cursor: pointer !important;
        font-weight: bold !important;
    }
    a:hover {
        color: #0052cc !important;
        text-decoration: underline !important;
    }
</style>";
        }

        private string GetNewsStyles()
        {
            return @"
<style>
    .news-content {
        margin-top: 20px;
    }
    .news-content p {
        margin-bottom: 15px;
    }
    .news-content strong {
        color: #2c3e50;
    }
</style>";
        }

        private string GetTrendStyles()
        {
            return @"
<style>
    .trend-category {
        margin-bottom: 30px;
        background-color: rgba(248, 249, 250, 0.1);
        backdrop-filter: blur(10px);
        border: 1px solid var(--border-color, rgba(255, 255, 255, 0.1));
        padding: 20px;
        border-radius: 10px;
    }
    .trend-category h2 {
        margin-top: 0;
        color: #2c3e50;
        font-size: 20px;
    }
    .trend-list {
        margin-top: 15px;
    }
    .trend-item {
        display: flex;
        align-items: center;
        padding: 12px 0;
        border-bottom: 1px solid var(--border-color, rgba(224, 224, 224, 0.3));
    }
    .trend-item:last-child {
        border-bottom: none;
    }
    .trend-rank {
        font-weight: bold;
        color: #7f8c8d;
        margin-right: 15px;
        min-width: 30px;
        font-size: 18px;
    }
    .trend-name {
        flex-grow: 1;
        font-size: 16px;
        color: #2c3e50;
    }
    .trend-volume {
        color: #95a5a6;
        font-size: 14px;
        margin-left: 10px;
    }
</style>";
        }

        private string GetCategoryIcon(TrendCategory category)
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

        private string GetCategoryDisplayName(TrendCategory category)
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

        private string GetCategoryNameInTurkish(string category)
        {
            return category?.ToLowerInvariant() switch
            {
                "business" => "ekonomi",
                "economy" => "ekonomi",
                "technology" => "teknoloji",
                "sports" => "spor",
                "health" => "sağlık",
                "entertainment" => "magazin",
                "world" => "dünya",
                "politics" => "siyaset",
                "science" => "bilim",
                "general" => "",
                _ => ""
            };
        }

        private string FormatCombinedHtml(WebContent news, List<TrendingTopic> trends)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='tr'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>Son Haberler ve Gündem</title>");
            html.AppendLine(GetCommonStyles());
            html.AppendLine(GetCombinedStyles());
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("<div class='container'>");
            html.AppendLine("<h1>📰 Son Haberler ve Gündem</h1>");
            
            // İki sütunlu layout
            html.AppendLine("<div class='content-grid'>");
            
            // Sol taraf - Haberler
            html.AppendLine("<div class='news-section'>");
            html.AppendLine("<h2>🗞️ Son Dakika Haberler</h2>");
            if (news != null)
            {
                html.AppendLine(ConvertMarkdownToHtml(news.Content));
            }
            else
            {
                html.AppendLine("<p>Haberler yüklenemedi.</p>");
            }
            html.AppendLine("</div>");
            
            // Sağ taraf - X Trendleri
            html.AppendLine("<div class='trends-section'>");
            html.AppendLine("<h2>🔥 X Gündem</h2>");
            if (trends != null && trends.Any())
            {
                html.AppendLine("<div class='trend-list'>");
                foreach (var trend in trends.Take(10))
                {
                    html.AppendLine("<div class='trend-item'>");
                    html.AppendLine($"<span class='trend-rank'>{trend.Rank}</span>");
                    html.AppendLine($"<span class='trend-name'>{System.Net.WebUtility.HtmlEncode(trend.DisplayName)}</span>");
                    if (trend.TweetVolume.HasValue)
                    {
                        html.AppendLine($"<span class='trend-volume'>{FormatNumber(trend.TweetVolume.Value)}</span>");
                    }
                    html.AppendLine("</div>");
                }
                html.AppendLine("</div>");
            }
            else
            {
                html.AppendLine("<p>Trendler yüklenemedi.</p>");
            }
            html.AppendLine("</div>");
            
            html.AppendLine("</div>"); // content-grid
            html.AppendLine("</div>"); // container
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string FormatCombinedVoice(WebContent news, List<TrendingTopic> trends)
        {
            var voice = new StringBuilder();
            voice.AppendLine("İşte son haberler ve gündem konuları:");
            
            // İlk 3 haber başlığı
            if (news != null && !string.IsNullOrEmpty(news.Content))
            {
                voice.AppendLine("Son dakika haberleri:");
                var lines = news.Content.Split('\n');
                var newsCount = 0;
                foreach (var line in lines)
                {
                    if (line.Contains("**") && newsCount < 3)
                    {
                        var cleanLine = line.Replace("**", "").Replace("📰", "").Trim();
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            // HTML entity'leri decode et
                            cleanLine = System.Net.WebUtility.HtmlDecode(cleanLine);
                            voice.Append(cleanLine);
                            voice.Append(". "); // Nokta ve boşluk daha doğal duraklama sağlar
                            newsCount++;
                        }
                    }
                }
            }
            
            // İlk 5 trend
            if (trends != null && trends.Any())
            {
                voice.AppendLine("");
                voice.AppendLine("X gündemde:");
                foreach (var trend in trends.Take(5))
                {
                    var trendName = System.Net.WebUtility.HtmlDecode(trend.DisplayName);
                    voice.Append($"{trend.Rank}. {trendName}");
                    voice.Append(". "); // Nokta ve boşluk daha doğal duraklama sağlar
                }
            }
            
            return voice.ToString();
        }

        private string GetCombinedStyles()
        {
            return @"
<style>
    .content-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 30px;
        margin-top: 20px;
    }
    .news-section, .trends-section {
        background-color: rgba(248, 249, 250, 0.1);
        backdrop-filter: blur(10px);
        border: 1px solid var(--border-color, rgba(255, 255, 255, 0.1));
        padding: 20px;
        border-radius: 10px;
    }
    .news-section h2, .trends-section h2 {
        margin-top: 0;
        color: #2c3e50;
        font-size: 20px;
        margin-bottom: 15px;
    }
    .trend-list {
        margin-top: 10px;
    }
    .trend-item {
        display: flex;
        align-items: center;
        padding: 8px 0;
        border-bottom: 1px solid var(--border-color, rgba(224, 224, 224, 0.3));
    }
    .trend-item:last-child {
        border-bottom: none;
    }
    @media (max-width: 768px) {
        .content-grid {
            grid-template-columns: 1fr;
        }
    }
</style>";
        }

        /// <summary>
        /// Haberleri kullanıcı tercihlerine göre filtreler ve sıralar
        /// </summary>
        private WebContent FilterNewsByUserPreferences(WebContent news, NewsPreferences preferences)
        {
            if (news?.Metadata == null || preferences == null)
                return news;

            try
            {
                // Metadata'dan RSSItems'ı al
                List<RSSItem> newsItems = null;
                if (news.Metadata.ContainsKey("RSSItems"))
                {
                    newsItems = news.Metadata["RSSItems"] as List<RSSItem>;
                }
                
                if (newsItems == null || !newsItems.Any())
                {
                    return news;
                }
                
                var allSelectedCategories = new List<string>(preferences.SelectedCategories);
                if (!string.IsNullOrEmpty(preferences.CustomCategory))
                {
                    allSelectedCategories.Add(preferences.CustomCategory);
                }
                
                // Haberleri kategorilere göre grupla
                var preferredNews = new List<RSSItem>();
                var otherNews = new List<RSSItem>();
                
                foreach (var item in newsItems)
                {
                    bool isPreferred = false;
                    
                    // Haber kategorisi veya içeriği tercih edilen kategorilerle eşleşiyor mu kontrol et
                    foreach (var category in allSelectedCategories)
                    {
                        if (IsNewsInCategory(item, category))
                        {
                            isPreferred = true;
                            break;
                        }
                    }
                    
                    if (isPreferred)
                    {
                        preferredNews.Add(item);
                    }
                    else
                    {
                        otherNews.Add(item);
                    }
                }
                
                // Önce tercih edilen haberleri, sonra diğerlerini göster
                var sortedItems = new List<RSSItem>();
                sortedItems.AddRange(preferredNews);
                sortedItems.AddRange(otherNews.Take(Math.Max(0, 10 - preferredNews.Count))); // Toplam 10 haber
                
                // Yeni WebContent oluştur
                var filteredNews = new WebContent
                {
                    Title = news.Title,
                    Content = FormatFilteredNewsContent(sortedItems, preferredNews.Count),
                    Type = news.Type,
                    Source = news.Source,
                    SourceUrl = news.SourceUrl,
                    Tags = news.Tags,
                    PublishedDate = news.PublishedDate,
                    RetrievedDate = news.RetrievedDate,
                    Language = news.Language,
                    Summary = news.Summary,
                    ConfidenceScore = news.ConfidenceScore,
                    IsFromCache = news.IsFromCache,
                    Metadata = new Dictionary<string, object>(news.Metadata)
                };
                
                // RSSItems'ı güncelle
                filteredNews.Metadata["RSSItems"] = sortedItems;
                
                return filteredNews;
            }
            catch
            {
                return news; // Hata durumunda orijinal haberleri döndür
            }
        }
        
        /// <summary>
        /// Haberin belirtilen kategoride olup olmadığını kontrol eder
        /// </summary>
        private bool IsNewsInCategory(RSSItem item, string category)
        {
            if (item == null || string.IsNullOrEmpty(category))
                return false;
            
            var lowerCategory = category.ToLowerInvariant();
            var lowerTitle = item.Title?.ToLowerInvariant() ?? "";
            var lowerDescription = item.Description?.ToLowerInvariant() ?? "";
            var lowerSource = item.Source?.ToLowerInvariant() ?? "";
            
            // Kategori eşleşmeleri
            switch (lowerCategory)
            {
                case "teknoloji":
                case "technology":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "teknoloji", "yazılım", "donanım", "bilgisayar", "telefon", "uygulama", 
                        "yapay zeka", "ai", "internet", "siber", "dijital", "apple", "google", 
                        "microsoft", "samsung", "huawei", "xiaomi", "tesla", "spacex");
                        
                case "spor":
                case "sports":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "spor", "futbol", "basketbol", "voleybol", "tenis", "olimpiyat", 
                        "şampiyon", "lig", "maç", "transfer", "galatasaray", "fenerbahçe", 
                        "beşiktaş", "trabzonspor", "milli takım");
                        
                case "ekonomi":
                case "economy":
                case "business":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "ekonomi", "dolar", "euro", "borsa", "faiz", "enflasyon", "merkez bankası", 
                        "tcmb", "fed", "piyasa", "yatırım", "altın", "petrol", "bitcoin", 
                        "kripto", "imf", "dünya bankası", "ihracat", "ithalat");
                        
                case "sağlık":
                case "health":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "sağlık", "hastane", "doktor", "tedavi", "ilaç", "aşı", "covid", 
                        "korona", "virüs", "hastalık", "ameliyat", "kanser", "kalp", "beyin");
                        
                case "magazin":
                case "entertainment":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "magazin", "ünlü", "sanatçı", "oyuncu", "şarkıcı", "dizi", "film", 
                        "sinema", "konser", "albüm", "evlilik", "boşanma", "aşk");
                        
                case "siyaset":
                case "politics":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "siyaset", "hükümet", "meclis", "milletvekili", "bakan", "cumhurbaşkanı", 
                        "seçim", "parti", "muhalefet", "iktidar", "kanun", "yasa", "anayasa");
                        
                case "dünya":
                case "world":
                    return ContainsAny(lowerTitle + " " + lowerDescription, 
                        "dünya", "küresel", "uluslararası", "abd", "avrupa", "rusya", "çin", 
                        "nato", "birleşmiş milletler", "bm", "ab", "savaş", "barış");
                        
                default:
                    // Özel kategori - doğrudan kelime araması
                    return lowerTitle.Contains(lowerCategory) || 
                           lowerDescription.Contains(lowerCategory) ||
                           lowerSource.Contains(lowerCategory);
            }
        }
        
        /// <summary>
        /// Metinde belirtilen kelimelerin herhangi birinin bulunup bulunmadığını kontrol eder
        /// </summary>
        private bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Filtrelenmiş haberleri formatlar
        /// </summary>
        private string FormatFilteredNewsContent(List<RSSItem> items, int preferredCount)
        {
            var content = new StringBuilder();
            content.AppendLine("📰 **EN SON HABERLER**");
            content.AppendLine();
            
            if (preferredCount > 0)
            {
                content.AppendLine($"⭐ *İlgi alanlarınıza uygun {preferredCount} haber*");
                content.AppendLine();
            }
            
            int index = 1;
            foreach (var item in items)
            {
                // Tercih edilen haberleri işaretle
                if (index <= preferredCount)
                {
                    content.AppendLine($"⭐ **{index}. {item.Title}**");
                }
                else
                {
                    content.AppendLine($"**{index}. {item.Title}**");
                }
                
                if (!string.IsNullOrEmpty(item.Description))
                {
                    content.AppendLine($"{item.Description}");
                }
                
                if (!string.IsNullOrEmpty(item.Source))
                {
                    content.AppendLine($"🔗 Kaynak: {item.Source} | ⏰ {item.PublishDate:HH:mm}");
                }
                
                content.AppendLine();
                index++;
            }
            
            return content.ToString();
        }

        /// <summary>
        /// WebContent'ten RSS haberleri çıkarıp NewsMemoryService'e kaydeder
        /// </summary>
        private void StoreNewsItemsFromContent(WebContent content)
        {
            try
            {
                LogService.LogInfo($"[WebInfoCommand] StoreNewsItemsFromContent çağrıldı. Content null: {content == null}, Metadata null: {content?.Metadata == null}");
                
                if (content?.Metadata == null)
                {
                    LogService.LogInfo("[WebInfoCommand] Content veya Metadata null");
                    return;
                }

                LogService.LogInfo($"[WebInfoCommand] Metadata keys: {string.Join(", ", content.Metadata.Keys)}");

                // Metadata'dan RSSItem listesini almaya çalış
                if (content.Metadata.ContainsKey("RSSItems"))
                {
                    LogService.LogInfo("[WebInfoCommand] RSSItems key bulundu");
                    
                    var items = content.Metadata["RSSItems"] as List<RSSItem>;
                    if (items != null && items.Any())
                    {
                        LogService.LogInfo($"[WebInfoCommand] {items.Count} haber NewsMemoryService'e kaydediliyor");
                        NewsMemoryService.StoreNewsItems(items);
                        LogService.LogInfo($"[WebInfoCommand] Haberler başarıyla kaydedildi");
                    }
                    else
                    {
                        LogService.LogInfo("[WebInfoCommand] RSSItems null veya boş");
                    }
                }
                // Alternatif: Content'ten parse etmeye çalış
                else if (!string.IsNullOrEmpty(content.Content))
                {
                    LogService.LogInfo("[WebInfoCommand] RSSItems bulunamadı, content'ten parse deneniyor");
                    var parsedItems = ParseNewsItemsFromContent(content.Content);
                    if (parsedItems != null && parsedItems.Any())
                    {
                        LogService.LogInfo($"[WebInfoCommand] {parsedItems.Count} haber parse edildi ve kaydediliyor");
                        NewsMemoryService.StoreNewsItems(parsedItems);
                    }
                }
                else
                {
                    LogService.LogInfo("[WebInfoCommand] Ne RSSItems ne de Content bulundu");
                }
            }
            catch (Exception ex)
            {
                LogService.LogInfo($"[WebInfoCommand] Haber kaydetme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Content string'inden RSSItem listesi parse eder (fallback)
        /// </summary>
        private List<RSSItem> ParseNewsItemsFromContent(string content)
        {
            var items = new List<RSSItem>();
            
            try
            {
                var lines = content.Split('\n');
                RSSItem currentItem = null;
                
                foreach (var line in lines)
                {
                    // Haber numarası ile başlayan satırlar
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\d+)\.\s+(.+)$");
                    if (match.Success)
                    {
                        // Önceki haberi kaydet
                        if (currentItem != null)
                        {
                            items.Add(currentItem);
                        }
                        
                        // Yeni haber başlat
                        currentItem = new RSSItem
                        {
                            Title = match.Groups[2].Value,
                            PublishDate = DateTime.Now
                        };
                    }
                    else if (currentItem != null)
                    {
                        // Kaynak satırı
                        if (line.TrimStart().StartsWith("Kaynak:"))
                        {
                            var sourceMatch = System.Text.RegularExpressions.Regex.Match(line, @"Kaynak:\s*([^|]+)");
                            if (sourceMatch.Success)
                            {
                                currentItem.Source = sourceMatch.Groups[1].Value.Trim();
                            }
                        }
                        // Açıklama satırı
                        else if (!string.IsNullOrWhiteSpace(line) && !line.Contains("==="))
                        {
                            if (string.IsNullOrEmpty(currentItem.Description))
                            {
                                currentItem.Description = line.Trim();
                            }
                        }
                    }
                }
                
                // Son haberi kaydet
                if (currentItem != null)
                {
                    items.Add(currentItem);
                }
            }
            catch
            {
                // Error parsing news items
            }
            
            return items;
        }
    }
}