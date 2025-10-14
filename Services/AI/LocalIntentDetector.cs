using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Models.AI;

namespace QuadroAIPilot.Services.AI
{
    /// <summary>
    /// Lokal AI tabanlı niyet algılama servisi
    /// </summary>
    public class LocalIntentDetector
    {
        private readonly SynonymDictionary _synonymDictionary;
        private readonly IntentPatterns _intentPatterns;
        private readonly UserLearningService _learningService;
        private readonly ILogger<LocalIntentDetector> _logger;
        
        public LocalIntentDetector(
            UserLearningService learningService,
            ILogger<LocalIntentDetector> logger)
        {
            _synonymDictionary = new SynonymDictionary();
            _intentPatterns = new IntentPatterns(_synonymDictionary);
            _learningService = learningService;
            _logger = logger;
        }
        
        /// <summary>
        /// Kullanıcı komutunu analiz eder ve niyeti algılar
        /// </summary>
        public async Task<IntentResult> DetectIntentAsync(string userInput)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    return CreateEmptyResult();
                }
                
                _logger.LogInformation("Intent detection başlatıldı: {Input}", userInput);
                
                // 1. Önce kullanıcının özel komutlarını kontrol et
                var customCommand = await _learningService.GetCustomCommandAsync(userInput);
                if (!string.IsNullOrEmpty(customCommand))
                {
                    _logger.LogInformation("Özel komut bulundu: {Input} -> {Custom}", userInput, customCommand);
                    userInput = customCommand;
                }
                
                // 2. Kısaltmaları genişlet
                var expanded = await _learningService.ExpandAbbreviationAsync(userInput);
                if (expanded != userInput)
                {
                    _logger.LogInformation("Kısaltma genişletildi: {Input} -> {Expanded}", userInput, expanded);
                    userInput = expanded;
                }
                
                // 3. Pattern matching ile intent tespiti
                var result = _intentPatterns.MatchPattern(userInput);
                
                // 4. Düşük güvenli sonuçlar için learning system'den yardım al
                if (result.Confidence < 0.7)
                {
                    var learnedIntent = await _learningService.GetLearnedIntentAsync(userInput);
                    if (learnedIntent != null)
                    {
                        _logger.LogInformation("Öğrenilmiş intent kullanıldı: {Intent}", learnedIntent.Intent.Name);
                        result = learnedIntent;
                    }
                }
                
                // 5. Hala düşük güvenliyse, benzer komutları öner
                if (result.Confidence < 0.5)
                {
                    var suggestions = await _learningService.GetSimilarCommandsAsync(userInput);
                    if (suggestions.Any())
                    {
                        result.Alternatives = suggestions
                            .Select(s => (new Intent(IntentType.Custom, s), 0.6))
                            .ToList();
                    }
                }
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.ElapsedMilliseconds;
                
                _logger.LogInformation("Intent detection tamamlandı: {Intent} ({Confidence:P}) - {Time}ms", 
                    result.Intent.Name, result.Confidence, result.ProcessingTime);
                
                // 6. Sonucu öğrenme sistemine kaydet
                await _learningService.RecordCommandAsync(userInput, result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Intent detection hatası: {Input}", userInput);
                stopwatch.Stop();
                
                return new IntentResult
                {
                    Intent = new Intent(IntentType.Unknown, "Error"),
                    Confidence = 0.0,
                    OriginalText = userInput,
                    ProcessedText = userInput,
                    ProcessingTime = stopwatch.ElapsedMilliseconds
                };
            }
        }
        
        /// <summary>
        /// Komut sonucunu öğrenme sistemine bildirir
        /// </summary>
        public async Task RecordCommandResultAsync(string command, bool success, string feedback = null)
        {
            try
            {
                await _learningService.UpdateCommandSuccessAsync(command, success, feedback);
                _logger.LogInformation("Komut sonucu kaydedildi: {Command} - Success: {Success}", command, success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Komut sonucu kaydetme hatası");
            }
        }
        
        /// <summary>
        /// Kullanıcıya komut önerileri sunar
        /// </summary>
        public async Task<string[]> GetCommandSuggestionsAsync(string partialCommand = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partialCommand))
                {
                    // Saat bazlı öneriler
                    return await _learningService.GetTimeBasedSuggestionsAsync(DateTime.Now);
                }
                else
                {
                    // Partial match önerileri
                    return await _learningService.GetAutocompleteSuggestionsAsync(partialCommand);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öneri alma hatası");
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Kullanıcı tanımlı komut ekler
        /// </summary>
        public async Task AddCustomCommandAsync(string userCommand, string systemCommand)
        {
            try
            {
                await _learningService.AddCustomMappingAsync(userCommand, systemCommand);
                _logger.LogInformation("Özel komut eklendi: {User} -> {System}", userCommand, systemCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özel komut ekleme hatası");
            }
        }
        
        /// <summary>
        /// İki kelime arasındaki benzerlik skorunu hesaplar (Levenshtein distance)
        /// </summary>
        public static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 1.0 : 0.0;
            if (string.IsNullOrEmpty(target)) return 0.0;
            
            source = source.ToLowerInvariant();
            target = target.ToLowerInvariant();
            
            if (source == target) return 1.0;
            
            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            return 1.0 - ((double)distance / maxLength);
        }
        
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;
            
            int[,] distance = new int[source.Length + 1, target.Length + 1];
            
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
        
        private IntentResult CreateEmptyResult()
        {
            return new IntentResult
            {
                Intent = new Intent(IntentType.Unknown, "Empty"),
                Confidence = 0.0,
                OriginalText = string.Empty,
                ProcessedText = string.Empty,
                ProcessingTime = 0
            };
        }
    }
}