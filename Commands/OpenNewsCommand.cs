using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Models;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Haber detaylarını tarayıcıda açan komut
    /// </summary>
    public class OpenNewsCommand : ISystemCommand
    {
        public bool CanHandle(string command)
        {
            var lowerCommand = command.ToLowerInvariant();
            
            // "haberi aç", "haberini göster", "haberini tarayıcıda aç" gibi pattern'ler
            return (lowerCommand.Contains("haberi") || lowerCommand.Contains("haberini")) &&
                   (lowerCommand.Contains("aç") || lowerCommand.Contains("göster") || 
                    lowerCommand.Contains("tarayıcı") || lowerCommand.Contains("detay"));
        }

        public async Task<CommandResponse> ExecuteAsync(CommandContext context)
        {
            try
            {
                Debug.WriteLine($"[OpenNewsCommand] Processing: {context.RawCommand}");
                
                var lowerCommand = context.RawCommand.ToLowerInvariant();
                
                // Önce index bazlı arama dene (örn: "3. haberi aç", "5 numaralı haberi göster")
                var indexMatch = System.Text.RegularExpressions.Regex.Match(lowerCommand, @"(\d+)[\s.]*(numaralı|\.)?.*haber");
                if (indexMatch.Success && int.TryParse(indexMatch.Groups[1].Value, out int index))
                {
                    var newsItem = NewsMemoryService.GetNewsByIndex(index);
                    if (newsItem != null)
                    {
                        return await OpenNewsInBrowser(newsItem.Link, newsItem.Title);
                    }
                    else
                    {
                        return new CommandResponse
                        {
                            IsSuccess = false,
                            Message = $"{index} numaralı haber bulunamadı.",
                            VoiceOutput = $"{index} numaralı haber bulunamadı."
                        };
                    }
                }
                
                // "Son haberi aç" kontrolü
                if (lowerCommand.Contains("son haber"))
                {
                    var latestNews = NewsMemoryService.GetLatestNews();
                    if (latestNews != null)
                    {
                        return await OpenNewsInBrowser(latestNews.Link, latestNews.Title);
                    }
                    else
                    {
                        return new CommandResponse
                        {
                            IsSuccess = false,
                            Message = "Son haber bulunamadı.",
                            VoiceOutput = "Son haber bulunamadı."
                        };
                    }
                }
                
                // Başlık bazlı arama
                var searchText = ExtractSearchText(context.RawCommand);
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var newsItem = NewsMemoryService.FindNewsByTitle(searchText);
                    if (newsItem != null)
                    {
                        return await OpenNewsInBrowser(newsItem.Link, newsItem.Title);
                    }
                    else
                    {
                        return new CommandResponse
                        {
                            IsSuccess = false,
                            Message = $"'{searchText}' ile ilgili haber bulunamadı.",
                            VoiceOutput = $"{searchText} ile ilgili haber bulunamadı."
                        };
                    }
                }
                
                // Genel durum
                var newsCount = NewsMemoryService.GetNewsCount();
                if (newsCount == 0)
                {
                    return new CommandResponse
                    {
                        IsSuccess = false,
                        Message = "Henüz haber listesi yüklenmemiş. Önce 'son haberleri göster' komutunu kullanın.",
                        VoiceOutput = "Henüz haber listesi yüklenmemiş. Önce son haberleri göster komutunu kullanın."
                    };
                }
                else
                {
                    return new CommandResponse
                    {
                        IsSuccess = false,
                        Message = $"Hangi haberi açmak istediğinizi belirtin. {newsCount} haber mevcut.",
                        VoiceOutput = $"Hangi haberi açmak istediğinizi belirtin. {newsCount} haber mevcut."
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenNewsCommand] Error: {ex.Message}");
                return new CommandResponse
                {
                    IsSuccess = false,
                    Message = $"Haber açılırken hata oluştu: {ex.Message}",
                    VoiceOutput = "Haber açılırken bir hata oluştu."
                };
            }
        }

        /// <summary>
        /// Komuttan arama metnini çıkarır
        /// </summary>
        private string ExtractSearchText(string command)
        {
            var lowerCommand = command.ToLowerInvariant();
            
            // Gereksiz kelimeleri kaldır
            var wordsToRemove = new[] { "haberi", "haberini", "aç", "göster", "tarayıcıda", "tarayıcı", "detay", "detayını", "ile", "ilgili" };
            
            var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var filteredWords = words.Where(w => !wordsToRemove.Contains(w.ToLowerInvariant())).ToList();
            
            var searchText = string.Join(" ", filteredWords).Trim();
            
            // Eğer çok kısa kaldıysa (sadece "aç" gibi) boş döndür
            if (searchText.Length < 3)
                return string.Empty;
                
            return searchText;
        }

        /// <summary>
        /// Haberi varsayılan tarayıcıda açar
        /// </summary>
        private async Task<CommandResponse> OpenNewsInBrowser(string url, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return new CommandResponse
                    {
                        IsSuccess = false,
                        Message = "Haber linki bulunamadı.",
                        VoiceOutput = "Haber linki bulunamadı."
                    };
                }
                
                // URL'yi tarayıcıda aç
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                
                Debug.WriteLine($"[OpenNewsCommand] Opened news: {title} - {url}");
                
                // Kısa bir bekleme
                await Task.Delay(500);
                
                return new CommandResponse
                {
                    IsSuccess = true,
                    Message = $"'{title}' haberi tarayıcıda açıldı.",
                    VoiceOutput = $"{title} haberi tarayıcıda açıldı.",
                    ActionType = CommandActionType.None // HTML gösterme
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenNewsCommand] Error opening browser: {ex.Message}");
                return new CommandResponse
                {
                    IsSuccess = false,
                    Message = $"Tarayıcı açılırken hata oluştu: {ex.Message}",
                    VoiceOutput = "Tarayıcı açılırken hata oluştu."
                };
            }
        }
    }
}