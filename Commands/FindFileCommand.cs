// FindFileCommand.cs – dosya-türü/uzantı haritası genişletildi (2025-05-06)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    public class FindFileCommand : ICommand
    {
        private readonly string _fileName;
        private readonly string _fileType;
        private readonly FileSearchService _fileService;
        public string CommandText { get; }

        public FindFileCommand(
            string commandText,
            string fileName,
            string fileType,
            FileSearchService fileService)
        {
            CommandText = commandText;
            _fileName = fileName;
            _fileType = fileType;
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[FindFileCommand] Dosya aranıyor: {_fileName}, Tür: {_fileType}");
                string extList = DetermineFileExtension(_fileType);
                
                // Önce tam eşleşme dene
                var path = await _fileService.FindFileAsync(_fileName, extList);

                // Tam eşleşme bulunamazsa, içeren arama dene
                if (string.IsNullOrEmpty(path))
                {
                    Debug.WriteLine($"[FindFileCommand] Tam eşleşme bulunamadı, içeren arama deneniyor...");
                    path = await _fileService.FindFileAsyncContains(_fileName, extList);
                }

                // İçeren arama da bulunamazsa, fuzzy arama dene
                if (string.IsNullOrEmpty(path))
                {
                    Debug.WriteLine($"[FindFileCommand] İçeren arama bulunamadı, fuzzy arama deneniyor...");
                    path = await _fileService.FindFileAsyncFuzzy(_fileName, extList);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    Debug.WriteLine($"[FindFileCommand] Dosya bulundu: {path}");
                    await _fileService.OpenFileAsync(path);
                    return true;
                }

                Debug.WriteLine($"[FindFileCommand] Dosya bulunamadı: {_fileName}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindFileCommand] Hata: {ex.Message}");
                return false;
            }
        }

        private string DetermineFileExtension(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return string.Empty;
            
            var normalizedType = type.ToLowerInvariant();
            
            // Önce tam eşleşme kontrolü
            var result = normalizedType switch
            {
                "excel" => "xls,xlsx,csv,xlsm",
                "word" => "doc,docx,rtf,odt",
                "powerpoint" or "sunum" => "ppt,pptx,pps,ppsx",
                "pdf" => "pdf,xps",
                "metin" => "txt",
                "fotoğraf" or "resim" or "görsel" => "jpg,jpeg,png,gif,bmp",
                "video" => "mp4,mkv,avi,mov,wmv",
                "müzik" or "ses" => "mp3,wav,m4a",
                "zip" or "sıkıştırılmış" => "zip,rar,7z,tar,gz",
                _ => null
            };
            
            // Tam eşleşme bulunamazsa fuzzy matching dene
            if (result == null)
            {
                var fileTypes = new Dictionary<string, string>
                {
                    ["excel"] = "xls,xlsx,csv,xlsm",
                    ["word"] = "doc,docx,rtf,odt",
                    ["powerpoint"] = "ppt,pptx,pps,ppsx",
                    ["sunum"] = "ppt,pptx,pps,ppsx",
                    ["pdf"] = "pdf,xps",
                    ["metin"] = "txt",
                    ["fotoğraf"] = "jpg,jpeg,png,gif,bmp",
                    ["resim"] = "jpg,jpeg,png,gif,bmp",
                    ["görsel"] = "jpg,jpeg,png,gif,bmp",
                    ["video"] = "mp4,mkv,avi,mov,wmv",
                    ["müzik"] = "mp3,wav,m4a",
                    ["ses"] = "mp3,wav,m4a",
                    ["zip"] = "zip,rar,7z,tar,gz",
                    ["sıkıştırılmış"] = "zip,rar,7z,tar,gz"
                };
                
                // Fuzzy matching ile en benzer türü bul
                var bestMatch = fileTypes.Keys
                    .Select(key => new { Key = key, Similarity = CalculateSimilarity(normalizedType, key) })
                    .Where(x => x.Similarity >= 0.7) // %70 benzerlik eşiği
                    .OrderByDescending(x => x.Similarity)
                    .FirstOrDefault();
                
                if (bestMatch != null)
                {
                    Debug.WriteLine($"[FindFileCommand] Dosya türü fuzzy match: '{type}' -> '{bestMatch.Key}' (benzerlik: {bestMatch.Similarity:P})");
                    result = fileTypes[bestMatch.Key];
                }
            }
            
            return result ?? string.Empty;
        }
        
        // Basit benzerlik hesaplama (FileSearchService'ten kopyalanabilir)
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;
                
            source = source.ToLowerInvariant();
            target = target.ToLowerInvariant();
            
            if (source.Equals(target))
                return 1.0;
                
            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            return maxLength == 0 ? 1.0 : 1.0 - (double)distance / maxLength;
        }
        
        private int LevenshteinDistance(string source, string target)
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
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[source.Length, target.Length];
        }
    }
}
