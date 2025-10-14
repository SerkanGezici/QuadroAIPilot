using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Commands;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Komut keşfi ve kullanım istatistikleri servisi
    /// Kullanıcıların mevcut komutları keşfetmesine yardımcı olur
    /// </summary>
    public class CommandDiscoveryService : ICommandDiscoveryService
    {
        private readonly ILogger<CommandDiscoveryService> _logger;
        private readonly CommandRegistry _commandRegistry;
        private readonly Dictionary<string, int> _commandUsageStats;
        private readonly List<string> _commandHistory;

        public CommandDiscoveryService(ILogger<CommandDiscoveryService> logger)
        {
            _logger = logger;
            _commandRegistry = CommandRegistry.Instance;
            _commandUsageStats = new Dictionary<string, int>();
            _commandHistory = new List<string>();
        }

        /// <summary>
        /// En popüler komutları getirir
        /// </summary>
        public async Task<List<string>> GetPopularCommands(int count)
        {
            await Task.CompletedTask; // Async uyumluluk için

            // Varsayılan popüler komutlar
            var defaultPopular = new List<string>
            {
                "outlook'u aç",
                "maillerimi oku",
                "haberleri oku",
                "dosya aç",
                "excel'i başlat",
                "ses aç",
                "pencereyi kapat",
                "yeni mail yaz",
                "google'da ara",
                "ekran kilitle"
            };

            // Kullanım istatistiklerine göre sırala
            if (_commandUsageStats.Any())
            {
                var topCommands = _commandUsageStats
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Varsayılanlarla birleştir
                return topCommands
                    .Union(defaultPopular)
                    .Take(count)
                    .ToList();
            }

            return defaultPopular.Take(count).ToList();
        }

        /// <summary>
        /// Tüm mevcut komutları getirir
        /// </summary>
        public async Task<List<string>> GetAllAvailableCommands()
        {
            await Task.CompletedTask; // Async uyumluluk için

            var allCommands = new HashSet<string>();

            // Registry'den komutları al
            var registeredCommands = _commandRegistry.GetAllCommands();
            
            foreach (var cmd in registeredCommands)
            {
                // Tüm trigger'ları ekle
                foreach (var trigger in cmd.CommandTriggers)
                {
                    allCommands.Add(trigger);
                }
            }

            // Sabit komutlar ekle (Registry'de olmayabilir)
            var additionalCommands = new[]
            {
                // Email komutları
                "maillerimi oku",
                "mail gönder",
                "yeni mail yaz",
                "e-posta ara",
                
                // Haber komutları
                "haberleri oku",
                "son haberleri göster",
                "gündem nedir",
                "teknoloji haberleri",
                
                // Dosya komutları
                "dosya aç",
                "dosya ara",
                "yeni dosya oluştur",
                "dosya kaydet",
                
                // Uygulama komutları
                "outlook'u aç",
                "excel'i başlat",
                "word'ü çalıştır",
                "chrome'u aç",
                
                // Sistem komutları
                "ses aç",
                "sesi kapat",
                "ekran kilitle",
                "pencereyi kapat",
                "[dikte_durduruldu]",
                "[dikte_başlatıldı]"
            };

            foreach (var cmd in additionalCommands)
            {
                allCommands.Add(cmd);
            }

            return allCommands.OrderBy(c => c).ToList();
        }

        /// <summary>
        /// Komut kullanım istatistiklerini getirir
        /// </summary>
        public async Task<Dictionary<string, int>> GetCommandUsageStats()
        {
            await Task.CompletedTask; // Async uyumluluk için
            
            return new Dictionary<string, int>(_commandUsageStats);
        }

        /// <summary>
        /// Komut kullanımını kaydeder
        /// </summary>
        public void RecordCommandUsage(string command)
        {
            try
            {
                var normalizedCommand = command.ToLowerInvariant().Trim();
                
                // İstatistikleri güncelle
                if (_commandUsageStats.ContainsKey(normalizedCommand))
                {
                    _commandUsageStats[normalizedCommand]++;
                }
                else
                {
                    _commandUsageStats[normalizedCommand] = 1;
                }

                // Geçmişe ekle
                _commandHistory.Add(normalizedCommand);
                
                // Geçmiş boyutunu sınırla (son 100 komut)
                if (_commandHistory.Count > 100)
                {
                    _commandHistory.RemoveAt(0);
                }

                _logger.LogDebug($"Command usage recorded: {normalizedCommand}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording command usage: {command}");
            }
        }

        /// <summary>
        /// Son kullanılan komutları getirir
        /// </summary>
        public List<string> GetRecentCommands(int count = 10)
        {
            return _commandHistory
                .AsEnumerable()
                .Reverse()
                .Distinct()
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Kategori bazlı komutları getirir
        /// </summary>
        public async Task<Dictionary<string, List<string>>> GetCommandsByCategory()
        {
            await Task.CompletedTask; // Async uyumluluk için

            return new Dictionary<string, List<string>>
            {
                ["E-posta İşlemleri"] = new List<string>
                {
                    "maillerimi oku",
                    "yeni mail yaz",
                    "mail gönder",
                    "mail ara",
                    "gelen kutusuna git",
                    "taslakları göster",
                    "gönderilenler"
                },
                ["Dosya ve Klasör"] = new List<string>
                {
                    "dosya aç",
                    "dosya ara",
                    "yeni dosya oluştur",
                    "dosya kaydet",
                    "belgeler klasörüne git",
                    "indirilenler aç",
                    "masaüstü göster"
                },
                ["Uygulama Kontrolü"] = new List<string>
                {
                    "outlook'u aç",
                    "excel'i başlat",
                    "word'ü çalıştır",
                    "chrome'u aç",
                    "uygulamayı kapat",
                    "pencereyi kapat"
                },
                ["Sistem Kontrolleri"] = new List<string>
                {
                    "ses aç",
                    "sesi kapat",
                    "ses seviyesini artır",
                    "ekran kilitle",
                    "bilgisayarı kapat",
                    "yeniden başlat"
                },
                ["Web ve Bilgi"] = new List<string>
                {
                    "haberleri oku",
                    "son haberleri göster",
                    "hava durumu",
                    "google'da ara",
                    "wikipedia'da bul",
                    "twitter gündem"
                },
                ["Ses Tanıma"] = new List<string>
                {
                    "[dikte_başlatıldı]",
                    "[dikte_durduruldu]",
                    "metni oku",
                    "seslendirme başlat"
                }
            };
        }

        /// <summary>
        /// Komut ipuçları getirir
        /// </summary>
        public List<CommandTip> GetCommandTips()
        {
            return new List<CommandTip>
            {
                new CommandTip
                {
                    Title = "Hızlı E-posta",
                    Description = "Outlook'u açıp yeni mail yazmak için",
                    Example = "outlook'u aç ve yeni mail yaz",
                    Category = "E-posta"
                },
                new CommandTip
                {
                    Title = "Zincirleme Komutlar",
                    Description = "Birden fazla işlemi tek komutta yapabilirsiniz",
                    Example = "chrome'u aç ve google'da hava durumu ara",
                    Category = "Gelişmiş"
                },
                new CommandTip
                {
                    Title = "Kısayollar",
                    Description = "Uzun komutlar yerine kısa alternatifler kullanabilirsiniz",
                    Example = "'maillerimi oku' yerine sadece 'mail oku'",
                    Category = "Kısayol"
                },
                new CommandTip
                {
                    Title = "Türkçe/İngilizce",
                    Description = "Her iki dilde de komut verebilirsiniz",
                    Example = "'dosya aç' veya 'open file'",
                    Category = "Dil"
                },
                new CommandTip
                {
                    Title = "Sistem Komutları",
                    Description = "Özel sistem komutları [] içinde kullanılır",
                    Example = "[dikte_durduruldu] - Ses tanımayı durdurur",
                    Category = "Sistem"
                }
            };
        }

        /// <summary>
        /// Belirli bir komut için yardım bilgisi getirir
        /// </summary>
        public CommandHelp GetCommandHelp(string command)
        {
            var normalizedCommand = command.ToLowerInvariant().Trim();
            var metadata = _commandRegistry.FindCommand(normalizedCommand);

            if (metadata != null)
            {
                return new CommandHelp
                {
                    Command = normalizedCommand,
                    Description = metadata.Description,
                    Aliases = metadata.CommandTriggers,
                    Examples = GetCommandExamples(metadata.CommandId),
                    RelatedCommands = GetRelatedCommands(metadata.CommandId)
                };
            }

            // Varsayılan yardım
            return new CommandHelp
            {
                Command = normalizedCommand,
                Description = "Bu komut hakkında detaylı bilgi bulunamadı.",
                Aliases = new List<string> { normalizedCommand },
                Examples = new List<string> { normalizedCommand },
                RelatedCommands = GetSimilarCommandNames(normalizedCommand)
            };
        }

        #region Private Helper Methods

        private List<string> GetCommandExamples(string commandId)
        {
            var examples = new Dictionary<string, List<string>>
            {
                ["read_emails"] = new List<string> 
                { 
                    "maillerimi oku",
                    "e-postaları göster",
                    "gelen kutusunu aç"
                },
                ["compose_email"] = new List<string> 
                { 
                    "yeni mail yaz",
                    "mail oluştur",
                    "e-posta yaz"
                },
                ["read_news"] = new List<string> 
                { 
                    "haberleri oku",
                    "son haberleri göster",
                    "gündem nedir"
                }
            };

            return examples.ContainsKey(commandId) 
                ? examples[commandId] 
                : new List<string>();
        }

        private List<string> GetRelatedCommands(string commandId)
        {
            var relatedMap = new Dictionary<string, List<string>>
            {
                ["read_emails"] = new List<string> 
                { 
                    "yeni mail yaz", 
                    "mail gönder", 
                    "mail ara" 
                },
                ["open_file"] = new List<string> 
                { 
                    "dosya kaydet", 
                    "dosya ara", 
                    "yeni dosya" 
                },
                ["read_news"] = new List<string> 
                { 
                    "hava durumu", 
                    "gündem", 
                    "twitter trend" 
                }
            };

            return relatedMap.ContainsKey(commandId) 
                ? relatedMap[commandId] 
                : new List<string>();
        }

        private List<string> GetSimilarCommandNames(string command)
        {
            var allCommands = _commandRegistry.GetAllCommands()
                .SelectMany(c => c.CommandTriggers)
                .ToList();

            return allCommands
                .Where(c => c.Contains(command, StringComparison.OrdinalIgnoreCase) ||
                           command.Contains(c, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
        }

        #endregion
    }

    #region Models

    public class CommandTip
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Example { get; set; }
        public string Category { get; set; }
    }

    public class CommandHelp
    {
        public string Command { get; set; }
        public string Description { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> Examples { get; set; }
        public List<string> RelatedCommands { get; set; }
    }

    #endregion
}