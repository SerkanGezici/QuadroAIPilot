using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Açılan web sitelerinin geçmişini yöneten servis
    /// </summary>
    public static class SiteHistoryManager
    {
        private static readonly string _historyFilePath;
        private static SiteHistory _history;
        private static readonly object _lockObject = new object();

        static SiteHistoryManager()
        {
            // Geçmiş dosyasının yolunu belirle
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "QuadroAIPilot");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _historyFilePath = Path.Combine(appFolder, "sites_history.json");
            LoadHistory();
        }

        /// <summary>
        /// Site geçmişi veri modeli
        /// </summary>
        public class SiteHistory
        {
            public List<SiteEntry> Sites { get; set; } = new List<SiteEntry>();
        }

        /// <summary>
        /// Site girişi veri modeli
        /// </summary>
        public class SiteEntry
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public DateTime LastAccessed { get; set; }
            public int AccessCount { get; set; }
            public List<string> Aliases { get; set; } = new List<string>();
        }

        /// <summary>
        /// Geçmişten site URL'sini getirir
        /// </summary>
        public static async Task<string> GetUrlFromHistoryAsync(string siteName)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_history?.Sites == null)
                        return null;

                    var normalizedName = NormalizeName(siteName);

                    // Tam eşleşme kontrolü
                    var entry = _history.Sites.FirstOrDefault(s => 
                        NormalizeName(s.Name) == normalizedName ||
                        s.Aliases.Any(a => NormalizeName(a) == normalizedName));

                    if (entry != null)
                    {
                        // Erişim sayısını artır
                        entry.AccessCount++;
                        entry.LastAccessed = DateTime.Now;
                        SaveHistory();

                        Debug.WriteLine($"[SiteHistoryManager] Geçmişten bulundu: {entry.Name} -> {entry.Url}");
                        return entry.Url;
                    }

                    // Kısmi eşleşme kontrolü
                    entry = _history.Sites.FirstOrDefault(s =>
                        s.Name.Contains(siteName, StringComparison.OrdinalIgnoreCase) ||
                        siteName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

                    if (entry != null)
                    {
                        entry.AccessCount++;
                        entry.LastAccessed = DateTime.Now;
                        SaveHistory();

                        Debug.WriteLine($"[SiteHistoryManager] Geçmişten kısmi eşleşme: {entry.Name} -> {entry.Url}");
                        return entry.Url;
                    }

                    return null;
                }
            });
        }

        /// <summary>
        /// Yeni site veya güncelleme kaydeder
        /// </summary>
        public static async Task SaveToHistoryAsync(string siteName, string url)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_history == null)
                        _history = new SiteHistory();

                    var normalizedName = NormalizeName(siteName);

                    // Mevcut girişi bul
                    var existingEntry = _history.Sites.FirstOrDefault(s =>
                        NormalizeName(s.Name) == normalizedName ||
                        s.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

                    if (existingEntry != null)
                    {
                        // Mevcut girişi güncelle
                        existingEntry.LastAccessed = DateTime.Now;
                        existingEntry.AccessCount++;

                        // Yeni alias ekle
                        if (!existingEntry.Aliases.Contains(siteName, StringComparer.OrdinalIgnoreCase) &&
                            !existingEntry.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase))
                        {
                            existingEntry.Aliases.Add(siteName);
                        }

                        Debug.WriteLine($"[SiteHistoryManager] Güncellendi: {existingEntry.Name} (Erişim: {existingEntry.AccessCount})");
                    }
                    else
                    {
                        // Yeni giriş ekle
                        var newEntry = new SiteEntry
                        {
                            Name = siteName,
                            Url = url,
                            LastAccessed = DateTime.Now,
                            AccessCount = 1
                        };

                        _history.Sites.Add(newEntry);
                        Debug.WriteLine($"[SiteHistoryManager] Yeni eklendi: {siteName} -> {url}");
                    }

                    // En çok kullanılanları üstte tut
                    _history.Sites = _history.Sites
                        .OrderByDescending(s => s.AccessCount)
                        .ThenByDescending(s => s.LastAccessed)
                        .ToList();

                    // Maksimum 500 site tut
                    if (_history.Sites.Count > 500)
                    {
                        _history.Sites = _history.Sites.Take(500).ToList();
                    }

                    SaveHistory();
                }
            });
        }

        /// <summary>
        /// En çok kullanılan siteleri getirir
        /// </summary>
        public static List<SiteEntry> GetTopSites(int count = 10)
        {
            lock (_lockObject)
            {
                if (_history?.Sites == null)
                    return new List<SiteEntry>();

                return _history.Sites
                    .OrderByDescending(s => s.AccessCount)
                    .ThenByDescending(s => s.LastAccessed)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// Son kullanılan siteleri getirir
        /// </summary>
        public static List<SiteEntry> GetRecentSites(int count = 10)
        {
            lock (_lockObject)
            {
                if (_history?.Sites == null)
                    return new List<SiteEntry>();

                return _history.Sites
                    .OrderByDescending(s => s.LastAccessed)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// Geçmişi dosyadan yükler
        /// </summary>
        private static void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    _history = JsonSerializer.Deserialize<SiteHistory>(json);
                    Debug.WriteLine($"[SiteHistoryManager] {_history?.Sites?.Count ?? 0} site geçmişten yüklendi");
                }
                else
                {
                    _history = new SiteHistory();
                    Debug.WriteLine("[SiteHistoryManager] Yeni geçmiş dosyası oluşturuldu");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SiteHistoryManager] Geçmiş yükleme hatası: {ex.Message}");
                _history = new SiteHistory();
            }
        }

        /// <summary>
        /// Geçmişi dosyaya kaydeder
        /// </summary>
        private static void SaveHistory()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_history, options);
                File.WriteAllText(_historyFilePath, json);
                Debug.WriteLine($"[SiteHistoryManager] Geçmiş kaydedildi ({_history?.Sites?.Count ?? 0} site)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SiteHistoryManager] Geçmiş kaydetme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Site adını normalize eder
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            return name.ToLowerInvariant()
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Trim();
        }

        /// <summary>
        /// Belirli bir siteyi geçmişten siler
        /// </summary>
        public static async Task RemoveFromHistoryAsync(string siteName)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_history?.Sites == null)
                        return;

                    var normalizedName = NormalizeName(siteName);
                    _history.Sites.RemoveAll(s =>
                        NormalizeName(s.Name) == normalizedName ||
                        s.Aliases.Any(a => NormalizeName(a) == normalizedName));

                    SaveHistory();
                    Debug.WriteLine($"[SiteHistoryManager] '{siteName}' geçmişten silindi");
                }
            });
        }

        /// <summary>
        /// Tüm geçmişi temizler
        /// </summary>
        public static async Task ClearHistoryAsync()
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _history = new SiteHistory();
                    SaveHistory();
                    Debug.WriteLine("[SiteHistoryManager] Tüm geçmiş temizlendi");
                }
            });
        }

        /// <summary>
        /// Site önerilerini getirir (fuzzy matching)
        /// </summary>
        public static List<SiteEntry> GetSuggestions(string query, int maxResults = 5)
        {
            lock (_lockObject)
            {
                if (_history?.Sites == null || string.IsNullOrWhiteSpace(query))
                    return new List<SiteEntry>();

                var normalizedQuery = NormalizeName(query);
                var results = new List<(SiteEntry site, double score)>();

                foreach (var site in _history.Sites)
                {
                    var score = CalculateSimilarity(normalizedQuery, NormalizeName(site.Name));
                    
                    // Alias'ları da kontrol et
                    foreach (var alias in site.Aliases)
                    {
                        var aliasScore = CalculateSimilarity(normalizedQuery, NormalizeName(alias));
                        if (aliasScore > score)
                            score = aliasScore;
                    }

                    if (score > 0.3) // %30 benzerlik eşiği
                    {
                        results.Add((site, score));
                    }
                }

                return results
                    .OrderByDescending(r => r.score)
                    .ThenByDescending(r => r.site.AccessCount)
                    .Take(maxResults)
                    .Select(r => r.site)
                    .ToList();
            }
        }

        /// <summary>
        /// İki string arasındaki benzerliği hesaplar (Levenshtein distance)
        /// </summary>
        private static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            if (source == target)
                return 1.0;

            int sourceLength = source.Length;
            int targetLength = target.Length;
            int[,] distance = new int[sourceLength + 1, targetLength + 1];

            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) ;
            for (int j = 0; j <= targetLength; distance[0, j] = j++) ;

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            int levenshteinDistance = distance[sourceLength, targetLength];
            int maxLength = Math.Max(sourceLength, targetLength);
            
            return 1.0 - (double)levenshteinDistance / maxLength;
        }
    }
}