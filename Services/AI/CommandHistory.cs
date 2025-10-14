using System;
using System.Collections.Generic;
using System.Linq;

namespace QuadroAIPilot.Services.AI
{
    /// <summary>
    /// Komut geçmişi yönetimi
    /// </summary>
    public class CommandHistory
    {
        private readonly Queue<CommandHistoryEntry> _history;
        private readonly int _maxSize;
        private readonly object _lockObject = new object();
        
        public CommandHistory(int maxSize = 1000)
        {
            _maxSize = maxSize;
            _history = new Queue<CommandHistoryEntry>();
        }
        
        /// <summary>
        /// Yeni komut ekler
        /// </summary>
        public void AddCommand(string command, bool success, string result = null)
        {
            lock (_lockObject)
            {
                var entry = new CommandHistoryEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Command = command,
                    Timestamp = DateTime.Now,
                    Success = success,
                    Result = result
                };
                
                _history.Enqueue(entry);
                
                // Max size kontrolü
                while (_history.Count > _maxSize)
                {
                    _history.Dequeue();
                }
            }
        }
        
        /// <summary>
        /// Son N komutu getirir
        /// </summary>
        public List<CommandHistoryEntry> GetRecentCommands(int count = 10)
        {
            lock (_lockObject)
            {
                return _history
                    .Reverse()
                    .Take(count)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Belirli bir zaman aralığındaki komutları getirir
        /// </summary>
        public List<CommandHistoryEntry> GetCommandsInTimeRange(DateTime start, DateTime end)
        {
            lock (_lockObject)
            {
                return _history
                    .Where(h => h.Timestamp >= start && h.Timestamp <= end)
                    .ToList();
            }
        }
        
        /// <summary>
        /// En sık kullanılan komutları getirir
        /// </summary>
        public Dictionary<string, int> GetMostFrequentCommands(int topN = 10)
        {
            lock (_lockObject)
            {
                return _history
                    .GroupBy(h => h.Command.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(topN)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }
        
        /// <summary>
        /// Başarı oranını hesaplar
        /// </summary>
        public double GetSuccessRate()
        {
            lock (_lockObject)
            {
                if (_history.Count == 0) return 0;
                
                var successCount = _history.Count(h => h.Success);
                return (double)successCount / _history.Count;
            }
        }
        
        /// <summary>
        /// Belirli bir komutun başarı oranını hesaplar
        /// </summary>
        public double GetCommandSuccessRate(string command)
        {
            lock (_lockObject)
            {
                var commandHistory = _history
                    .Where(h => h.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                    
                if (!commandHistory.Any()) return 0;
                
                var successCount = commandHistory.Count(h => h.Success);
                return (double)successCount / commandHistory.Count;
            }
        }
        
        /// <summary>
        /// Geçmişi temizler
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _history.Clear();
            }
        }
        
        /// <summary>
        /// İstatistikleri döndürür
        /// </summary>
        public CommandHistoryStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var entries = _history.ToList();
                
                return new CommandHistoryStatistics
                {
                    TotalCommands = entries.Count,
                    SuccessfulCommands = entries.Count(e => e.Success),
                    FailedCommands = entries.Count(e => !e.Success),
                    SuccessRate = GetSuccessRate(),
                    UniqueCommands = entries.Select(e => e.Command.ToLowerInvariant()).Distinct().Count(),
                    OldestCommand = entries.FirstOrDefault()?.Timestamp,
                    NewestCommand = entries.LastOrDefault()?.Timestamp
                };
            }
        }
    }
    
    /// <summary>
    /// Komut geçmişi girişi
    /// </summary>
    public class CommandHistoryEntry
    {
        public string Id { get; set; }
        public string Command { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string Result { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
    }
    
    /// <summary>
    /// Komut geçmişi istatistikleri
    /// </summary>
    public class CommandHistoryStatistics
    {
        public int TotalCommands { get; set; }
        public int SuccessfulCommands { get; set; }
        public int FailedCommands { get; set; }
        public double SuccessRate { get; set; }
        public int UniqueCommands { get; set; }
        public DateTime? OldestCommand { get; set; }
        public DateTime? NewestCommand { get; set; }
    }
}