using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuadroAIPilot.Services;
using QuadroAIPilot.Models;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Klasör arama ve listeleme komutu
    /// </summary>
    public class FindFolderCommand : ICommand
    {
        private readonly string _folderName;
        private readonly FileSearchService _fileService;
        private readonly IWebViewManager _webViewManager;
        private readonly int _maxResults;
        public string CommandText { get; }

        public FindFolderCommand(
            string commandText,
            string folderName,
            FileSearchService fileService,
            IWebViewManager webViewManager,
            int maxResults = 10)
        {
            CommandText = commandText;
            _folderName = folderName;
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _webViewManager = webViewManager;
            _maxResults = maxResults;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[FindFolderCommand] Klasör aranıyor: {_folderName}");
                
                // Çoklu klasör ara
                var results = await _fileService.FindMultipleFoldersAsync(_folderName, _maxResults);
                
                if (results != null && results.Any())
                {
                    Debug.WriteLine($"[FindFolderCommand] {results.Count} klasör bulundu");
                    
                    // WebView varsa HTML çıktı oluştur
                    if (_webViewManager != null)
                    {
                        var htmlContent = GenerateSearchResultsHtml(results);
                        await _webViewManager.AppendOutput(htmlContent);
                    }
                    
                    // Sesli geri bildirim
                    var message = results.Count == 1 
                        ? "1 klasör bulundu" 
                        : $"{results.Count} klasör bulundu. Listeden seçim yapabilirsiniz.";
                    await TextToSpeechService.SpeakTextAsync(message);
                    
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[FindFolderCommand] Klasör bulunamadı: {_folderName}");
                    
                    if (_webViewManager != null)
                    {
                        var noResultHtml = GenerateNoResultHtml(_folderName);
                        await _webViewManager.AppendOutput(noResultHtml);
                    }
                    
                    await TextToSpeechService.SpeakTextAsync($"{_folderName} klasörü bulunamadı");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindFolderCommand] Hata: {ex.Message}");
                return false;
            }
        }

        private string GenerateSearchResultsHtml(List<FolderSearchResult> results)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<div class='folder-search-results' style='background: linear-gradient(135deg, #06b6d4 0%, #0891b2 100%); padding: 20px; border-radius: 12px; margin: 15px 0; box-shadow: 0 10px 30px rgba(0,0,0,0.2);'>");
            sb.AppendLine($"<h3 style='color: white; margin-bottom: 20px; font-size: 18px; font-weight: 600;'>📁 Bulunan Klasörler ({results.Count} adet)</h3>");
            sb.AppendLine("<div class='folder-list' style='max-height: 500px; overflow-y: auto;'>");
            
            foreach (var folder in results)
            {
                var icon = folder.GetFolderIcon();
                
                // Eşleşme skoruna göre renk
                string scoreColor = folder.MatchScore >= 0.9 ? "#10b981" : 
                                   folder.MatchScore >= 0.7 ? "#3b82f6" : "#f59e0b";
                
                sb.AppendLine($@"
                    <div class='folder-item' style='
                        background: rgba(255, 255, 255, 0.95); 
                        padding: 12px; 
                        margin: 8px 0; 
                        border-radius: 8px; 
                        border-left: 4px solid {scoreColor};
                        cursor: pointer; 
                        transition: all 0.3s ease;
                        display: flex;
                        align-items: center;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.1);'
                         onmouseover='this.style.transform=""translateX(5px)""; this.style.boxShadow=""0 4px 12px rgba(0,0,0,0.15)""' 
                         onmouseout='this.style.transform=""translateX(0)""; this.style.boxShadow=""0 2px 8px rgba(0,0,0,0.1)""'
                         onclick='window.openFolderFromSearch(""{folder.FolderPath.Replace("\\", "\\\\")}"")'
                         title='Açmak için tıklayın'>
                        
                        <span style='font-size: 28px; margin-right: 15px;'>{icon}</span>
                        
                        <div style='flex: 1; min-width: 0;'>
                            <div style='font-weight: 600; color: #1e293b; font-size: 15px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;'>
                                {folder.FolderName}
                            </div>
                            <div style='font-size: 12px; color: #64748b; margin-top: 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;'>
                                📍 {folder.FolderPath}
                            </div>
                            <div style='font-size: 11px; color: #94a3b8; margin-top: 4px;'>
                                📄 {folder.FileCount} dosya • 📁 {folder.SubFolderCount} alt klasör");
                
                if (folder.TotalSize > 0)
                {
                    sb.AppendLine($" • 💾 {folder.GetFormattedSize()}");
                }
                
                sb.AppendLine(@"
                            </div>
                        </div>
                        
                        <div style='text-align: right; margin-left: 15px; min-width: 100px;'>
                            <div style='font-size: 11px; color: #94a3b8;'>
                                Son değişiklik
                            </div>
                            <div style='font-size: 11px; color: #64748b; font-weight: 500;'>
                                {folder.LastModified:dd.MM.yyyy}
                            </div>
                            <div style='font-size: 11px; color: #94a3b8;'>
                                {folder.LastModified:HH:mm}
                            </div>");
                
                // Eşleşme skoru göstergesi
                if (folder.MatchScore > 0)
                {
                    int scorePercent = (int)(folder.MatchScore * 100);
                    sb.AppendLine($@"
                            <div style='margin-top: 8px;'>
                                <div style='font-size: 10px; color: #94a3b8;'>Eşleşme</div>
                                <div style='width: 60px; height: 4px; background: #e2e8f0; border-radius: 2px; overflow: hidden;'>
                                    <div style='width: {scorePercent}%; height: 100%; background: {scoreColor};'></div>
                                </div>
                            </div>");
                }
                
                sb.AppendLine(@"
                        </div>
                    </div>
                ");
            }
            
            sb.AppendLine("</div>");
            
            // İpucu
            sb.AppendLine(@"
                <div style='margin-top: 15px; padding: 10px; background: rgba(255,255,255,0.1); border-radius: 6px;'>
                    <p style='color: rgba(255,255,255,0.9); font-size: 12px; margin: 0;'>
                        💡 <strong>İpucu:</strong> Klasöre tıklayarak Windows Explorer'da açabilirsiniz.
                        <span style='margin-left: 10px;'>🟢 Tam eşleşme</span>
                        <span style='margin-left: 10px;'>🔵 İyi eşleşme</span>
                        <span style='margin-left: 10px;'>🟡 Kısmi eşleşme</span>
                    </p>
                </div>
            ");
            
            sb.AppendLine("</div>");
            
            return sb.ToString();
        }

        private string GenerateNoResultHtml(string folderName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<div class='no-results' style='background: linear-gradient(135deg, #fbbf24 0%, #f59e0b 100%); padding: 20px; border-radius: 12px; margin: 15px 0; box-shadow: 0 10px 30px rgba(0,0,0,0.2);'>");
            sb.AppendLine("<div style='text-align: center; color: white;'>");
            sb.AppendLine("<div style='font-size: 48px; margin-bottom: 15px;'>🔍</div>");
            sb.AppendLine($"<h3 style='margin-bottom: 10px;'>Klasör Bulunamadı</h3>");
            sb.AppendLine($"<p style='opacity: 0.9;'>'{folderName}' adında klasör bulunamadı.</p>");
            
            sb.AppendLine(@"
                <div style='margin-top: 20px; padding: 15px; background: rgba(255,255,255,0.1); border-radius: 8px;'>
                    <p style='margin: 5px 0; font-size: 14px;'><strong>Öneriler:</strong></p>
                    <ul style='text-align: left; margin: 10px 0; padding-left: 20px; font-size: 13px;'>
                        <li>Klasör adını kontrol edin</li>
                        <li>Farklı bir kelime ile aramayı deneyin</li>
                        <li>Klasör Belgeler, Masaüstü veya İndirilenler'de olabilir</li>
                        <li>Klasör adı Türkçe karakter içeriyorsa İngilizce deneyebilirsiniz</li>
                    </ul>
                </div>
            ");
            
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            
            return sb.ToString();
        }
    }
}