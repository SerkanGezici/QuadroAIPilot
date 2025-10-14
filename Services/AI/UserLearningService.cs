using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Models.AI;

namespace QuadroAIPilot.Services.AI
{
    /// <summary>
    /// Kullanıcı davranışlarını öğrenen ve tahminlerde bulunan servis
    /// </summary>
    public class UserLearningService
    {
        private readonly string _profilePath;
        private readonly ILogger<UserLearningService> _logger;
        private UserProfile _userProfile;
        private readonly object _lockObject = new object();
        
        // Command history için circular buffer
        private readonly Queue<(string Command, DateTime Time)> _recentCommands;
        private const int MaxRecentCommands = 100;
        
        public UserLearningService(ILogger<UserLearningService> logger)
        {
            _logger = logger;
            _profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuadroAIPilot",
                "user_profile.json"
            );
            
            _recentCommands = new Queue<(string, DateTime)>();
            LoadOrCreateProfile();
        }
        
        /// <summary>
        /// Kullanıcı profilini yükler veya yeni oluşturur
        /// </summary>
        private void LoadOrCreateProfile()
        {
            try
            {
                var directory = Path.GetDirectoryName(_profilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                if (File.Exists(_profilePath))
                {
                    var json = File.ReadAllText(_profilePath);
                    _userProfile = JsonSerializer.Deserialize<UserProfile>(json) ?? new UserProfile();
                    _logger.LogInformation("Kullanıcı profili yüklendi. Toplam komut: {Count}", _userProfile.TotalCommands);
                }
                else
                {
                    _userProfile = new UserProfile();
                    SaveProfile();
                    _logger.LogInformation("Yeni kullanıcı profili oluşturuldu");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil yükleme hatası");
                _userProfile = new UserProfile();
            }
        }
        
        /// <summary>
        /// Profili diske kaydeder
        /// </summary>
        private async Task SaveProfileAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    _userProfile.LastUpdated = DateTime.Now;
                }
                
                var json = JsonSerializer.Serialize(_userProfile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(_profilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil kaydetme hatası");
            }
        }
        
        private void SaveProfile()
        {
            SaveProfileAsync().GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Komutu kayıt eder
        /// </summary>
        public async Task RecordCommandAsync(string command, IntentResult result)
        {
            try
            {
                lock (_lockObject)
                {
                    // Komut sıklığını güncelle
                    var normalizedCommand = command.ToLowerInvariant().Trim();
                    if (_userProfile.CommandFrequency.ContainsKey(normalizedCommand))
                    {
                        _userProfile.CommandFrequency[normalizedCommand]++;
                    }
                    else
                    {
                        _userProfile.CommandFrequency[normalizedCommand] = 1;
                    }
                    
                    // Saat bazlı pattern kaydet
                    var timeKey = GetTimeKey(DateTime.Now);
                    if (!_userProfile.TimeBasedPatterns.ContainsKey(timeKey))
                    {
                        _userProfile.TimeBasedPatterns[timeKey] = new List<string>();
                    }
                    _userProfile.TimeBasedPatterns[timeKey].Add(normalizedCommand);
                    
                    // Recent commands'e ekle
                    _recentCommands.Enqueue((normalizedCommand, DateTime.Now));
                    if (_recentCommands.Count > MaxRecentCommands)
                    {
                        _recentCommands.Dequeue();
                    }
                    
                    // Ardışık komutları tespit et
                    DetectCommandSequences();
                    
                    _userProfile.TotalCommands++;
                }
                
                // Her 10 komutta bir kaydet
                if (_userProfile.TotalCommands % 10 == 0)
                {
                    await SaveProfileAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Komut kaydetme hatası");
            }
        }
        
        /// <summary>
        /// Komut başarı durumunu günceller
        /// </summary>
        public async Task UpdateCommandSuccessAsync(string command, bool success, string feedback = null)
        {
            try
            {
                lock (_lockObject)
                {
                    if (success)
                    {
                        _userProfile.SuccessRate = (_userProfile.SuccessRate * (_userProfile.TotalCommands - 1) + 1) / _userProfile.TotalCommands;
                    }
                    else
                    {
                        _userProfile.SuccessRate = (_userProfile.SuccessRate * (_userProfile.TotalCommands - 1)) / _userProfile.TotalCommands;
                        
                        // Hatalı komutu kaydet
                        if (!string.IsNullOrEmpty(feedback))
                        {
                            _userProfile.ErrorCorrections[command] = feedback;
                        }
                    }
                }
                
                await SaveProfileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başarı durumu güncelleme hatası");
            }
        }
        
        /// <summary>
        /// Özel komut eşlemesi ekler
        /// </summary>
        public async Task AddCustomMappingAsync(string userCommand, string systemCommand)
        {
            try
            {
                lock (_lockObject)
                {
                    _userProfile.CustomMappings[userCommand.ToLowerInvariant()] = systemCommand;
                }
                
                await SaveProfileAsync();
                _logger.LogInformation("Özel eşleme eklendi: {User} -> {System}", userCommand, systemCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özel eşleme ekleme hatası");
            }
        }
        
        /// <summary>
        /// Özel komut varsa döndürür
        /// </summary>
        public Task<string> GetCustomCommandAsync(string userInput)
        {
            var normalized = userInput.ToLowerInvariant().Trim();
            
            lock (_lockObject)
            {
                if (_userProfile.CustomMappings.TryGetValue(normalized, out var customCommand))
                {
                    return Task.FromResult(customCommand);
                }
            }
            
            return Task.FromResult<string>(null);
        }
        
        /// <summary>
        /// Kısaltmaları genişletir (örn: "m" -> "mail aç")
        /// </summary>
        public Task<string> ExpandAbbreviationAsync(string input)
        {
            if (input.Length > 3) return Task.FromResult(input);
            
            var normalized = input.ToLowerInvariant();
            
            lock (_lockObject)
            {
                // En sık kullanılan ve input ile başlayan komutları bul
                var matches = _userProfile.CommandFrequency
                    .Where(kvp => kvp.Key.StartsWith(normalized))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(1)
                    .Select(kvp => kvp.Key)
                    .FirstOrDefault();
                    
                if (!string.IsNullOrEmpty(matches))
                {
                    _logger.LogInformation("Kısaltma genişletildi: {Input} -> {Expanded}", input, matches);
                    return Task.FromResult(matches);
                }
            }
            
            return Task.FromResult(input);
        }
        
        /// <summary>
        /// Öğrenilmiş intent'i döndürür
        /// </summary>
        public Task<IntentResult> GetLearnedIntentAsync(string input)
        {
            var normalized = input.ToLowerInvariant().Trim();
            
            lock (_lockObject)
            {
                // Hata düzeltmelerini kontrol et
                if (_userProfile.ErrorCorrections.TryGetValue(normalized, out var corrected))
                {
                    return Task.FromResult(new IntentResult
                    {
                        Intent = new Intent(IntentType.Custom, "Learned"),
                        Confidence = 0.8,
                        OriginalText = input,
                        ProcessedText = corrected
                    });
                }
            }
            
            return Task.FromResult<IntentResult>(null);
        }
        
        /// <summary>
        /// Benzer komutları döndürür
        /// </summary>
        public Task<List<string>> GetSimilarCommandsAsync(string input)
        {
            var normalized = input.ToLowerInvariant().Trim();
            var similar = new List<(string Command, double Similarity)>();
            
            lock (_lockObject)
            {
                foreach (var command in _userProfile.CommandFrequency.Keys)
                {
                    var similarity = LocalIntentDetector.CalculateSimilarity(normalized, command);
                    if (similarity > 0.6 && similarity < 1.0)
                    {
                        similar.Add((command, similarity));
                    }
                }
            }
            
            var results = similar
                .OrderByDescending(s => s.Similarity)
                .Take(3)
                .Select(s => s.Command)
                .ToList();
                
            return Task.FromResult(results);
        }
        
        /// <summary>
        /// Saat bazlı komut önerileri
        /// </summary>
        public Task<string[]> GetTimeBasedSuggestionsAsync(DateTime time)
        {
            var timeKey = GetTimeKey(time);
            var suggestions = new List<string>();
            
            lock (_lockObject)
            {
                if (_userProfile.TimeBasedPatterns.TryGetValue(timeKey, out var patterns))
                {
                    suggestions = patterns
                        .GroupBy(p => p)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList();
                }
                
                // Eğer saat bazlı öneri yoksa, genel popüler komutları öner
                if (!suggestions.Any())
                {
                    suggestions = _userProfile.CommandFrequency
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(3)
                        .Select(kvp => kvp.Key)
                        .ToList();
                }
            }
            
            return Task.FromResult(suggestions.ToArray());
        }
        
        /// <summary>
        /// Otomatik tamamlama önerileri
        /// </summary>
        public Task<string[]> GetAutocompleteSuggestionsAsync(string partial)
        {
            var normalized = partial.ToLowerInvariant().Trim();
            
            lock (_lockObject)
            {
                var matches = _userProfile.CommandFrequency
                    .Where(kvp => kvp.Key.StartsWith(normalized))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5)
                    .Select(kvp => kvp.Key)
                    .ToArray();
                    
                return Task.FromResult(matches);
            }
        }

        /// <summary>
        /// Kullanıcı tarafından öğretilen özel komut ekler
        /// </summary>
        public async Task AddCustomCommandAsync(string command, string action)
        {
            try
            {
                var normalizedCommand = command.ToLowerInvariant().Trim();
                
                lock (_lockObject)
                {
                    // Komutu sıklık listesine ekle
                    if (!_userProfile.CommandFrequency.ContainsKey(normalizedCommand))
                    {
                        _userProfile.CommandFrequency[normalizedCommand] = 1;
                    }
                    
                    _logger.LogInformation($"Custom command learned: {normalizedCommand} -> {action}");
                }
                
                // Profili kaydet
                await SaveProfileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom command");
            }
        }
        
        /// <summary>
        /// Ardışık komut pattern'lerini tespit eder
        /// </summary>
        private void DetectCommandSequences()
        {
            if (_recentCommands.Count < 2) return;
            
            var commandList = _recentCommands.ToList();
            
            // Son 2-3 komutu sequence olarak kontrol et
            for (int seqLength = 2; seqLength <= Math.Min(3, commandList.Count); seqLength++)
            {
                var sequence = commandList
                    .Skip(commandList.Count - seqLength)
                    .Select(c => c.Command)
                    .ToList();
                    
                var existingSeq = _userProfile.CommandSequences
                    .FirstOrDefault(s => s.Commands.SequenceEqual(sequence));
                    
                if (existingSeq != null)
                {
                    existingSeq.Frequency++;
                }
                else if (seqLength == 2) // Sadece 2'li sequence'leri yeni ekle
                {
                    _userProfile.CommandSequences.Add(new CommandSequence
                    {
                        Commands = sequence,
                        Frequency = 1
                    });
                }
            }
            
            // En fazla 20 sequence tut
            if (_userProfile.CommandSequences.Count > 20)
            {
                _userProfile.CommandSequences = _userProfile.CommandSequences
                    .OrderByDescending(s => s.Frequency)
                    .Take(20)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Saat bazlı time key oluşturur
        /// </summary>
        private string GetTimeKey(DateTime time)
        {
            var hour = time.Hour;
            return $"{hour:00}:00-{(hour + 1) % 24:00}:00";
        }
        
        /// <summary>
        /// Kullanıcı istatistiklerini döndürür
        /// </summary>
        public UserStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new UserStatistics
                {
                    TotalCommands = _userProfile.TotalCommands,
                    SuccessRate = _userProfile.SuccessRate,
                    MostUsedCommands = _userProfile.CommandFrequency
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(10)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    CustomCommandCount = _userProfile.CustomMappings.Count,
                    ProfileAge = DateTime.Now - _userProfile.CreatedAt
                };
            }
        }
    }
    
    /// <summary>
    /// Kullanıcı istatistikleri
    /// </summary>
    public class UserStatistics
    {
        public int TotalCommands { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, int> MostUsedCommands { get; set; }
        public int CustomCommandCount { get; set; }
        public TimeSpan ProfileAge { get; set; }
    }
}