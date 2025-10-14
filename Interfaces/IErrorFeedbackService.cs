using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroAIPilot.Models.AI;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Kullanıcı dostu hata geri bildirimi servisi interface'i
    /// </summary>
    public interface IErrorFeedbackService
    {
        /// <summary>
        /// Hata durumunda kullanıcıya anlaşılır geri bildirim sağlar
        /// </summary>
        Task<ErrorFeedback> GetFeedbackForError(string userCommand, IntentResult intentResult, Exception error = null);
        
        /// <summary>
        /// Komut keşfi için öneriler sağlar
        /// </summary>
        Task<CommandDiscovery> DiscoverCommands(string context = null);
        
        /// <summary>
        /// Anlık komut önerileri sağlar (autocomplete benzeri)
        /// </summary>
        Task<List<string>> GetRealtimeSuggestions(string partialCommand);
    }

    /// <summary>
    /// Komut keşfi servisi interface'i
    /// </summary>
    public interface ICommandDiscoveryService
    {
        /// <summary>
        /// En popüler komutları getirir
        /// </summary>
        Task<List<string>> GetPopularCommands(int count);
        
        /// <summary>
        /// Tüm mevcut komutları getirir
        /// </summary>
        Task<List<string>> GetAllAvailableCommands();
        
        /// <summary>
        /// Komut kullanım istatistiklerini getirir
        /// </summary>
        Task<Dictionary<string, int>> GetCommandUsageStats();
    }

    /// <summary>
    /// Hata geri bildirimi modeli
    /// </summary>
    public class ErrorFeedback
    {
        public string OriginalCommand { get; set; }
        public string Message { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
        public string LearningSuggestion { get; set; }
        public ErrorCategory Category { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Komut keşfi modeli
    /// </summary>
    public class CommandDiscovery
    {
        public string Context { get; set; }
        public List<string> PopularCommands { get; set; } = new List<string>();
        public Dictionary<string, List<string>> Categories { get; set; } = new();
        public List<string> ContextualSuggestions { get; set; } = new List<string>();
        public List<string> ShortcutTips { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Hata kategorileri
    /// </summary>
    public enum ErrorCategory
    {
        LowConfidence,
        UnknownCommand,
        SystemError,
        PermissionError,
        NotFound,
        Timeout,
        GeneralFailure
    }
}