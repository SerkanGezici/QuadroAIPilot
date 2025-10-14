using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Services.WebServices.Providers
{
    /// <summary>
    /// Wikipedia content provider for Turkish Wikipedia
    /// </summary>
    public class WikipediaProvider : IContentProvider
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://tr.wikipedia.org/w/api.php";
        private const string UserAgent = "QuadroAIPilot/1.0 (https://quadroai.com; contact@quadroai.com)";

        public string Name => "Wikipedia Provider";
        public int Priority => 3;

        public WikipediaProvider(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public async Task<bool> CanHandleAsync(ContentRequest request)
        {
            if (request.PreferredType == ContentType.Wikipedia)
                return true;

            var keywords = new[] { "wikipedia", "vikipedi", "nedir", "kimdir", "ne demek", "açıklama", "tanım" };
            return keywords.Any(k => request.Query.ToLowerInvariant().Contains(k));
        }

        public async Task<WebContent> GetContentAsync(ContentRequest request)
        {
            try
            {
                Debug.WriteLine($"[WikipediaProvider] Getting content for: {request.Query}");

                // Extract the search term from the query
                var searchTerm = ExtractSearchTerm(request.Query);
                
                // First, search for the page
                var searchResults = await SearchWikipedia(searchTerm);
                
                if (searchResults == null || !searchResults.Any())
                {
                    throw new Exception($"'{searchTerm}' ile ilgili Wikipedia sayfası bulunamadı");
                }

                // Get the full page content for the first result
                var pageTitle = searchResults.First();
                var pageContent = await GetPageContent(pageTitle);

                if (pageContent == null)
                {
                    throw new Exception($"'{pageTitle}' sayfası içeriği alınamadı");
                }

                return pageContent;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WikipediaProvider] Error: {ex.Message}");
                throw new Exception("Wikipedia içeriği alınamadı", ex);
            }
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        private async Task<List<string>> SearchWikipedia(string query)
        {
            var parameters = new Dictionary<string, string>
            {
                ["action"] = "opensearch",
                ["search"] = query,
                ["limit"] = "5",
                ["namespace"] = "0",
                ["format"] = "json"
            };

            var url = BuildUrl(parameters);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            var results = new List<string>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 1)
            {
                var titlesArray = doc.RootElement[1];
                foreach (var title in titlesArray.EnumerateArray())
                {
                    results.Add(title.GetString());
                }
            }

            return results;
        }

        private async Task<WebContent> GetPageContent(string pageTitle)
        {
            // Get page extract and basic info
            var parameters = new Dictionary<string, string>
            {
                ["action"] = "query",
                ["prop"] = "extracts|pageimages|info|categories",
                ["exintro"] = "true",
                ["explaintext"] = "true",
                ["exsectionformat"] = "plain",
                ["inprop"] = "url",
                ["pithumbsize"] = "500",
                ["titles"] = pageTitle,
                ["format"] = "json"
            };

            var url = BuildUrl(parameters);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
            var page = pages.EnumerateObject().FirstOrDefault();

            if (page.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            var pageData = page.Value;
            
            // Extract content
            var title = GetJsonString(pageData, "title");
            var extract = GetJsonString(pageData, "extract");
            var fullUrl = GetJsonString(pageData, "fullurl");
            var pageId = GetJsonString(pageData, "pageid");

            // Get categories
            var categories = new List<string>();
            if (pageData.TryGetProperty("categories", out var categoriesElement))
            {
                foreach (var cat in categoriesElement.EnumerateArray())
                {
                    var catTitle = GetJsonString(cat, "title");
                    if (!string.IsNullOrEmpty(catTitle) && catTitle.StartsWith("Kategori:"))
                    {
                        categories.Add(catTitle.Replace("Kategori:", "").Trim());
                    }
                }
            }

            // Format content with sections
            var formattedContent = FormatWikipediaContent(title, extract, categories);

            // Get additional summary
            var summary = await GetPageSummary(pageTitle);

            return new WebContent
            {
                Title = title,
                Content = formattedContent,
                Summary = summary ?? extract.Substring(0, Math.Min(extract.Length, 200)) + "...",
                Source = "Wikipedia Türkçe",
                SourceUrl = fullUrl,
                Type = ContentType.Wikipedia,
                PublishedDate = DateTime.Now, // Wikipedia doesn't provide publication date easily
                Language = "tr",
                Tags = categories,
                Metadata = new Dictionary<string, object>
                {
                    ["PageId"] = pageId,
                    ["Categories"] = categories,
                    ["HasImage"] = pageData.TryGetProperty("thumbnail", out _)
                }
            };
        }

        private async Task<string> GetPageSummary(string pageTitle)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["action"] = "query",
                    ["prop"] = "extracts",
                    ["exintro"] = "true",
                    ["explaintext"] = "true",
                    ["exsentences"] = "3",
                    ["titles"] = pageTitle,
                    ["format"] = "json"
                };

                var url = BuildUrl(parameters);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
                var page = pages.EnumerateObject().FirstOrDefault();

                if (page.Value.ValueKind != JsonValueKind.Undefined)
                {
                    return GetJsonString(page.Value, "extract");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WikipediaProvider] Error getting summary: {ex.Message}");
            }

            return null;
        }

        private string ExtractSearchTerm(string query)
        {
            var lowerQuery = query.ToLowerInvariant();
            
            // Remove common question words
            var removeWords = new[] { "nedir", "kimdir", "ne demek", "vikipedi", "wikipedia", 
                                     "açıklama", "tanım", "hakkında", "ile ilgili", "nedir?", "kimdir?" };
            
            foreach (var word in removeWords)
            {
                lowerQuery = lowerQuery.Replace(word, "");
            }

            // Extract the main term
            var terms = lowerQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // If query contains quotes, extract quoted term
            if (query.Contains("\""))
            {
                var match = System.Text.RegularExpressions.Regex.Match(query, "\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            // Return cleaned up terms
            return string.Join(" ", terms).Trim();
        }

        private string FormatWikipediaContent(string title, string extract, List<string> categories)
        {
            var content = new System.Text.StringBuilder();
            
            content.AppendLine($"# {title}\n");
            
            // Add category badges
            if (categories.Any())
            {
                content.Append("📂 **Kategoriler:** ");
                content.AppendLine(string.Join(", ", categories.Take(5)));
                content.AppendLine();
            }

            // Split extract into paragraphs for better readability
            var paragraphs = extract.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraph in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    content.AppendLine(paragraph.Trim());
                    content.AppendLine();
                }
            }

            // Add source attribution
            content.AppendLine("\n---");
            content.AppendLine("*Kaynak: Wikipedia Türkçe*");

            return content.ToString();
        }

        private string BuildUrl(Dictionary<string, string> parameters)
        {
            var query = string.Join("&", parameters.Select(kvp => 
                $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
            
            return $"{BaseUrl}?{query}";
        }

        private string GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && 
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
            return string.Empty;
        }
    }
}