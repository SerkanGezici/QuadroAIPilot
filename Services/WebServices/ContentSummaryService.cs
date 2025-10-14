using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Services.WebServices
{
    /// <summary>
    /// Content summarization service with local and cloud options
    /// </summary>
    public class ContentSummaryService : IContentSummaryService
    {
        private readonly Dictionary<string, double> _turkishStopWords;
        private readonly Dictionary<string, double> _importantWords;

        public ContentSummaryService()
        {
            _turkishStopWords = InitializeTurkishStopWords();
            _importantWords = InitializeImportantWords();
        }

        public async Task<string> SummarizeAsync(string content, SummaryOptions options = null)
        {
            options ??= new SummaryOptions();

            try
            {
                Debug.WriteLine($"[ContentSummaryService] Summarizing content, length: {content.Length}");

                // Clean and prepare content
                var cleanedContent = CleanContent(content);

                // Choose summarization method based on options
                string summary;
                if (options.Type == SummaryType.Extractive)
                {
                    summary = await ExtractiveSummarize(cleanedContent, options);
                }
                else if (options.Type == SummaryType.Abstractive && options.UseCloudServices)
                {
                    // For now, fall back to extractive
                    // In production, this would call an AI API
                    summary = await ExtractiveSummarize(cleanedContent, options);
                }
                else
                {
                    // Hybrid approach
                    summary = await HybridSummarize(cleanedContent, options);
                }

                return summary;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContentSummaryService] Error: {ex.Message}");
                // Return first few sentences as fallback
                return GetFirstSentences(content, options.MaxSentences);
            }
        }

        public async Task<Dictionary<string, int>> ExtractKeywordsAsync(string content, int maxKeywords = 10)
        {
            await Task.CompletedTask; // Make async for future cloud integration

            var words = ExtractWords(content);
            var wordFrequency = new Dictionary<string, int>();

            foreach (var word in words)
            {
                var lowerWord = word.ToLowerInvariant();
                
                // Skip stop words and short words
                if (_turkishStopWords.ContainsKey(lowerWord) || word.Length < 3)
                    continue;

                wordFrequency[word] = wordFrequency.GetValueOrDefault(word, 0) + 1;
            }

            // Apply TF-IDF-like scoring
            var scoredWords = new Dictionary<string, double>();
            foreach (var kvp in wordFrequency)
            {
                double score = kvp.Value; // Term frequency
                
                // Boost important words
                if (_importantWords.ContainsKey(kvp.Key.ToLowerInvariant()))
                {
                    score *= _importantWords[kvp.Key.ToLowerInvariant()];
                }

                scoredWords[kvp.Key] = score;
            }

            // Return top keywords
            return scoredWords
                .OrderByDescending(kvp => kvp.Value)
                .Take(maxKeywords)
                .ToDictionary(kvp => kvp.Key, kvp => (int)Math.Round(kvp.Value));
        }

        public Task<bool> IsAvailableAsync()
        {
            // Local summarization is always available
            return Task.FromResult(true);
        }

        private async Task<string> ExtractiveSummarize(string content, SummaryOptions options)
        {
            await Task.CompletedTask; // Make async for consistency

            var sentences = SplitIntoSentences(content);
            if (sentences.Count <= options.MaxSentences)
            {
                return content;
            }

            // Score sentences
            var sentenceScores = new Dictionary<string, double>();
            var keywords = await ExtractKeywordsAsync(content, 20);

            foreach (var sentence in sentences)
            {
                var score = 0.0;
                var words = ExtractWords(sentence);

                foreach (var word in words)
                {
                    if (keywords.ContainsKey(word))
                    {
                        score += keywords[word];
                    }
                }

                // Normalize by sentence length
                if (words.Count > 0)
                {
                    score /= Math.Sqrt(words.Count);
                }

                // Boost first and last sentences
                if (sentences.IndexOf(sentence) == 0)
                    score *= 1.2;
                else if (sentences.IndexOf(sentence) == sentences.Count - 1)
                    score *= 1.1;

                sentenceScores[sentence] = score;
            }

            // Select top sentences while maintaining order
            var selectedSentences = sentenceScores
                .OrderByDescending(kvp => kvp.Value)
                .Take(options.MaxSentences)
                .Select(kvp => kvp.Key)
                .ToList();

            // Reorder sentences to maintain narrative flow
            var orderedSentences = sentences
                .Where(s => selectedSentences.Contains(s))
                .ToList();

            return string.Join(" ", orderedSentences);
        }

        private async Task<string> HybridSummarize(string content, SummaryOptions options)
        {
            // Start with extractive summary
            var extractiveSummary = await ExtractiveSummarize(content, options);

            // Apply post-processing for better readability
            var improvedSummary = ImproveReadability(extractiveSummary);

            return improvedSummary;
        }

        private string CleanContent(string content)
        {
            // Remove HTML tags if any
            content = Regex.Replace(content, @"<[^>]+>", " ");
            
            // Remove URLs
            content = Regex.Replace(content, @"https?://\S+", " ");
            
            // Remove extra whitespace
            content = Regex.Replace(content, @"\s+", " ");
            
            // Remove special formatting characters
            content = content.Replace("*", "").Replace("#", "").Replace("_", " ");

            return content.Trim();
        }

        private List<string> SplitIntoSentences(string text)
        {
            // Turkish sentence splitting
            var sentences = new List<string>();
            var pattern = @"[.!?]+\s+";
            var parts = Regex.Split(text, pattern);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 20) // Minimum sentence length
                {
                    sentences.Add(trimmed);
                }
            }

            return sentences;
        }

        private List<string> ExtractWords(string text)
        {
            // Extract words (handles Turkish characters)
            var pattern = @"\b[\wçğıöşüÇĞİÖŞÜ]+\b";
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            
            return matches.Cast<Match>().Select(m => m.Value).ToList();
        }

        private string GetFirstSentences(string content, int count)
        {
            var sentences = SplitIntoSentences(content);
            return string.Join(" ", sentences.Take(count));
        }

        private string ImproveReadability(string summary)
        {
            // Ensure proper sentence endings
            if (!summary.EndsWith(".") && !summary.EndsWith("!") && !summary.EndsWith("?"))
            {
                summary += ".";
            }

            // Fix spacing issues
            summary = Regex.Replace(summary, @"\s+([,.!?])", "$1");
            summary = Regex.Replace(summary, @"([,.!?])(\w)", "$1 $2");

            return summary;
        }

        private Dictionary<string, double> InitializeTurkishStopWords()
        {
            var stopWords = new[]
            {
                "ve", "ile", "bir", "bu", "da", "de", "için", "olarak", "olan",
                "gibi", "daha", "çok", "en", "ne", "var", "yok", "ama", "ancak",
                "ki", "mi", "mu", "mı", "mü", "ya", "veya", "hem", "değil",
                "her", "şey", "ben", "sen", "o", "biz", "siz", "onlar", "bunu",
                "şu", "bunlar", "şunlar", "kim", "kime", "kimin", "neden", "nasıl",
                "kadar", "sonra", "önce", "bile", "diye", "yani", "fakat", "lakin"
            };

            return stopWords.ToDictionary(w => w, w => 0.1);
        }

        private Dictionary<string, double> InitializeImportantWords()
        {
            var importantWords = new Dictionary<string, double>
            {
                // News-related
                ["son"] = 2.0,
                ["dakika"] = 2.0,
                ["önemli"] = 1.8,
                ["açıklama"] = 1.7,
                ["kritik"] = 1.9,
                ["gündem"] = 1.6,
                
                // Technology
                ["teknoloji"] = 1.5,
                ["yenilik"] = 1.6,
                ["geliştirme"] = 1.4,
                ["yazılım"] = 1.5,
                ["donanım"] = 1.5,
                
                // Economy
                ["ekonomi"] = 1.5,
                ["piyasa"] = 1.6,
                ["borsa"] = 1.7,
                ["dolar"] = 1.8,
                ["yatırım"] = 1.5,
                
                // General importance markers
                ["ilk"] = 1.5,
                ["yeni"] = 1.4,
                ["büyük"] = 1.3,
                ["başarı"] = 1.5,
                ["rekor"] = 1.7,
                ["tarih"] = 1.4
            };

            return importantWords;
        }
    }
}