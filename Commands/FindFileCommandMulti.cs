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
    /// Çoklu dosya arama ve listeleme komutu
    /// </summary>
    public class FindFileCommandMulti : ICommand
    {
        private readonly string _fileName;
        private readonly string _fileType;
        private readonly FileSearchService _fileService;
        private readonly IWebViewManager _webViewManager;
        private readonly int _maxResults;
        public string CommandText { get; }

        public FindFileCommandMulti(
            string commandText,
            string fileName,
            string fileType,
            FileSearchService fileService,
            IWebViewManager webViewManager,
            int maxResults = 10)
        {
            CommandText = commandText;
            _fileName = fileName;
            _fileType = fileType;
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _webViewManager = webViewManager;
            _maxResults = maxResults;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[FindFileCommandMulti] Çoklu dosya aranıyor: {_fileName}, Tür: {_fileType}");
                
                string extList = DetermineFileExtension(_fileType);
                
                // Çoklu dosya ara
                var results = await _fileService.FindMultipleFilesAsync(_fileName, extList, _maxResults);
                
                if (results != null && results.Any())
                {
                    Debug.WriteLine($"[FindFileCommandMulti] {results.Count} dosya bulundu");
                    
                    // WebView varsa HTML çıktı oluştur
                    if (_webViewManager != null)
                    {
                        var htmlContent = GenerateSearchResultsHtml(results);
                        await _webViewManager.AppendOutput(htmlContent);
                    }
                    
                    // Sesli geri bildirim
                    var message = results.Count == 1 
                        ? "1 dosya bulundu" 
                        : $"{results.Count} dosya bulundu. Listeden seçim yapabilirsiniz.";
                    await TextToSpeechService.SpeakTextAsync(message);
                    
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[FindFileCommandMulti] Dosya bulunamadı: {_fileName}");
                    
                    if (_webViewManager != null)
                    {
                        var noResultHtml = GenerateNoResultHtml(_fileName, _fileType);
                        await _webViewManager.AppendOutput(noResultHtml);
                    }
                    
                    await TextToSpeechService.SpeakTextAsync($"{_fileName} dosyası bulunamadı");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindFileCommandMulti] Hata: {ex.Message}");
                return false;
            }
        }

        private string GenerateSearchResultsHtml(List<FileSearchResult> results)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<div class='search-results' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 12px; margin: 15px 0; box-shadow: 0 10px 30px rgba(0,0,0,0.2);'>");
            sb.AppendLine($"<h3 style='color: white; margin-bottom: 20px; font-size: 18px; font-weight: 600;'>🔍 Bulunan Dosyalar ({results.Count} adet)</h3>");
            sb.AppendLine("<div class='file-list' style='max-height: 500px; overflow-y: auto;'>");
            
            for (int i = 0; i < results.Count; i++)
            {
                var file = results[i];
                var icon = file.GetFileIcon();
                
                // Öncelik rengini belirle
                string priorityColor = file.SearchPriority switch
                {
                    3 => "#4ade80", // Yeşil - Yüksek öncelik (Recent/MRU)
                    2 => "#60a5fa", // Mavi - Orta öncelik (Office MRU)
                    _ => "#f59e0b"  // Sarı - Normal öncelik
                };
                
                sb.AppendLine($@"
                    <div class='file-item' style='
                        background: rgba(255, 255, 255, 0.95); 
                        padding: 12px; 
                        margin: 8px 0; 
                        border-radius: 8px; 
                        border-left: 4px solid {priorityColor};
                        cursor: pointer; 
                        transition: all 0.3s ease;
                        display: flex;
                        align-items: center;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.1);'
                         onmouseover='this.style.transform=""translateX(5px)""; this.style.boxShadow=""0 4px 12px rgba(0,0,0,0.15)""' 
                         onmouseout='this.style.transform=""translateX(0)""; this.style.boxShadow=""0 2px 8px rgba(0,0,0,0.1)""'
                         onclick='window.openFileFromSearch(""{file.FilePath.Replace("\\", "\\\\")}"")'
                         title='Açmak için tıklayın'>
                        
                        <span style='font-size: 24px; margin-right: 15px;'>{icon}</span>
                        
                        <div style='flex: 1; min-width: 0;'>
                            <div style='font-weight: 600; color: #1e293b; font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;'>
                                {file.FileName}
                            </div>
                            <div style='font-size: 12px; color: #64748b; margin-top: 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;'>
                                📁 {file.Directory}
                            </div>
                        </div>
                        
                        <div style='text-align: right; margin-left: 15px; min-width: 100px;'>
                            <div style='font-size: 11px; color: #94a3b8;'>
                                {file.LastModified:dd.MM.yyyy HH:mm}
                            </div>
                            <div style='font-size: 11px; color: #94a3b8;'>
                                {file.GetFormattedSize()}
                            </div>");
                
                // Eşleşme skoru göstergesi
                if (file.MatchScore > 0)
                {
                    int scorePercent = (int)(file.MatchScore * 100);
                    string scoreColor = scorePercent >= 90 ? "#10b981" : scorePercent >= 70 ? "#3b82f6" : "#f59e0b";
                    sb.AppendLine($@"
                            <div style='margin-top: 4px;'>
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
                        💡 <strong>İpucu:</strong> Dosyaya tıklayarak açabilirsiniz. 
                        <span style='margin-left: 10px;'>🟢 Son kullanılan</span>
                        <span style='margin-left: 10px;'>🔵 Office geçmişi</span>
                        <span style='margin-left: 10px;'>🟡 Normal arama</span>
                    </p>
                </div>
            ");
            
            sb.AppendLine("</div>");
            
            return sb.ToString();
        }

        private string GenerateNoResultHtml(string fileName, string fileType)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<div class='no-results' style='background: linear-gradient(135deg, #f87171 0%, #ef4444 100%); padding: 20px; border-radius: 12px; margin: 15px 0; box-shadow: 0 10px 30px rgba(0,0,0,0.2);'>");
            sb.AppendLine("<div style='text-align: center; color: white;'>");
            sb.AppendLine("<div style='font-size: 48px; margin-bottom: 15px;'>😔</div>");
            sb.AppendLine($"<h3 style='margin-bottom: 10px;'>Dosya Bulunamadı</h3>");
            
            if (!string.IsNullOrEmpty(fileType))
            {
                sb.AppendLine($"<p style='opacity: 0.9;'>'{fileName}' adında {fileType} dosyası bulunamadı.</p>");
            }
            else
            {
                sb.AppendLine($"<p style='opacity: 0.9;'>'{fileName}' adında dosya bulunamadı.</p>");
            }
            
            sb.AppendLine(@"
                <div style='margin-top: 20px; padding: 15px; background: rgba(255,255,255,0.1); border-radius: 8px;'>
                    <p style='margin: 5px 0; font-size: 14px;'><strong>Öneriler:</strong></p>
                    <ul style='text-align: left; margin: 10px 0; padding-left: 20px; font-size: 13px;'>
                        <li>Dosya adını kontrol edin</li>
                        <li>Farklı bir kelime ile aramayı deneyin</li>
                        <li>Dosyanın bilgisayarınızda olduğundan emin olun</li>
                        <li>Son kullanılan dosyalar arasında olmayabilir</li>
                    </ul>
                </div>
            ");
            
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            
            return sb.ToString();
        }

        private string DetermineFileExtension(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return string.Empty;
            
            var normalizedType = type.ToLowerInvariant();
            
            var result = normalizedType switch
            {
                "excel" => "xls,xlsx,csv,xlsm",
                "word" => "doc,docx,rtf,odt",
                "powerpoint" or "sunum" => "ppt,pptx,pps,ppsx",
                "pdf" => "pdf,xps",
                "metin" or "text" => "txt,log,md",
                "fotoğraf" or "resim" or "görsel" => "jpg,jpeg,png,gif,bmp,svg",
                "video" => "mp4,mkv,avi,mov,wmv",
                "müzik" or "ses" => "mp3,wav,m4a,flac",
                "zip" or "sıkıştırılmış" => "zip,rar,7z,tar,gz",
                _ => string.Empty
            };
            
            return result;
        }
    }
}