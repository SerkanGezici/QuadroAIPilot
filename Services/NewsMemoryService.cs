using System;
using System.Collections.Generic;
using System.Linq;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Son gösterilen haberleri hafızada tutan servis
    /// </summary>
    public static class NewsMemoryService
    {
        private static readonly object _lock = new object();
        private static List<RSSItem> _lastNewsItems = new List<RSSItem>();
        private static DateTime _lastUpdateTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);
        
        // Zaman damgası özellikleri
        private static DateTime _lastCommandTime = DateTime.MinValue;
        private static HashSet<string> _lastShownNewsGuids = new HashSet<string>();
        
        // Kategori bazlı zaman takibi
        private static Dictionary<string, DateTime> _lastCategoryCommandTimes = new Dictionary<string, DateTime>();
        private static Dictionary<string, HashSet<string>> _lastCategoryShownGuids = new Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Haber listesini saklar
        /// </summary>
        public static void StoreNewsItems(List<RSSItem> items)
        {
            lock (_lock)
            {
                if (items != null && items.Any())
                {
                    _lastNewsItems = new List<RSSItem>(items);
                    _lastUpdateTime = DateTime.Now;
                    LogService.LogInfo($"[NewsMemoryService] {items.Count} haber saklandı");
                }
                else
                {
                    LogService.LogInfo("[NewsMemoryService] Boş veya null haber listesi geldi");
                }
            }
        }
        
        /// <summary>
        /// Son komut zamanını günceller ve gösterilen haberleri işaretler
        /// </summary>
        public static void UpdateLastCommandTime(List<RSSItem> shownItems)
        {
            lock (_lock)
            {
                _lastCommandTime = DateTime.Now;
                
                if (shownItems != null && shownItems.Any())
                {
                    // Gösterilen haberlerin GUID'lerini sakla
                    _lastShownNewsGuids.Clear();
                    foreach (var item in shownItems)
                    {
                        if (!string.IsNullOrEmpty(item.Guid))
                        {
                            _lastShownNewsGuids.Add(item.Guid);
                        }
                    }
                    LogService.LogInfo($"[NewsMemoryService] Son komut zamanı güncellendi, {_lastShownNewsGuids.Count} haber işaretlendi");
                }
            }
        }

        /// <summary>
        /// Başlık içinde arama yaparak haber bulur
        /// </summary>
        public static RSSItem FindNewsByTitle(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            lock (_lock)
            {
                // Cache süresi dolmuşsa null dön
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    _lastNewsItems.Clear();
                    return null;
                }

                var searchLower = searchText.ToLowerInvariant();
                
                // Önce tam eşleşme ara
                var exactMatch = _lastNewsItems.FirstOrDefault(item => 
                    item.Title.ToLowerInvariant().Equals(searchLower));
                    
                if (exactMatch != null)
                    return exactMatch;

                // Sonra başlıkta geçenleri ara
                var partialMatch = _lastNewsItems.FirstOrDefault(item => 
                    item.Title.ToLowerInvariant().Contains(searchLower));
                    
                if (partialMatch != null)
                    return partialMatch;

                // Son olarak kelimeleri ayrı ayrı ara
                var words = searchLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 1)
                {
                    // Tüm kelimeler geçiyorsa
                    var allWordsMatch = _lastNewsItems.FirstOrDefault(item =>
                    {
                        var titleLower = item.Title.ToLowerInvariant();
                        return words.All(word => titleLower.Contains(word));
                    });
                    
                    if (allWordsMatch != null)
                        return allWordsMatch;

                    // En az yarısı geçiyorsa
                    var halfWordsMatch = _lastNewsItems.FirstOrDefault(item =>
                    {
                        var titleLower = item.Title.ToLowerInvariant();
                        var matchCount = words.Count(word => titleLower.Contains(word));
                        return matchCount >= (words.Length + 1) / 2;
                    });
                    
                    return halfWordsMatch;
                }

                return null;
            }
        }

        /// <summary>
        /// Index'e göre haber getirir (1-based index)
        /// </summary>
        public static RSSItem GetNewsByIndex(int index)
        {
            lock (_lock)
            {
                // Cache süresi dolmuşsa null dön
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    _lastNewsItems.Clear();
                    return null;
                }

                // 1-based index'i 0-based'e çevir
                var zeroBasedIndex = index - 1;
                
                if (zeroBasedIndex >= 0 && zeroBasedIndex < _lastNewsItems.Count)
                {
                    return _lastNewsItems[zeroBasedIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// Son haberi getirir
        /// </summary>
        public static RSSItem GetLatestNews()
        {
            lock (_lock)
            {
                // Cache süresi dolmuşsa null dön
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    _lastNewsItems.Clear();
                    return null;
                }

                return _lastNewsItems.FirstOrDefault();
            }
        }

        /// <summary>
        /// Toplam haber sayısını döndürür
        /// </summary>
        public static int GetNewsCount()
        {
            lock (_lock)
            {
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    LogService.LogInfo("[NewsMemoryService] Cache süresi dolmuş, haberler temizlendi");
                    _lastNewsItems.Clear();
                    return 0;
                }

                LogService.LogInfo($"[NewsMemoryService] GetNewsCount: {_lastNewsItems.Count}");
                return _lastNewsItems.Count;
            }
        }

        /// <summary>
        /// Tüm haberleri döndürür
        /// </summary>
        public static List<RSSItem> GetAllNews()
        {
            lock (_lock)
            {
                // Cache süresi dolmuşsa boş liste dön
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    LogService.LogInfo("[NewsMemoryService] GetAllNews: Cache süresi dolmuş, boş liste dönülüyor");
                    _lastNewsItems.Clear();
                    return new List<RSSItem>();
                }
                
                LogService.LogInfo($"[NewsMemoryService] GetAllNews: {_lastNewsItems.Count} haber döndürülüyor");

                return new List<RSSItem>(_lastNewsItems);
            }
        }

        /// <summary>
        /// Cache'i temizler
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _lastNewsItems.Clear();
                _lastUpdateTime = DateTime.MinValue;
                _lastCommandTime = DateTime.MinValue;
                _lastShownNewsGuids.Clear();
            }
        }
        
        /// <summary>
        /// Belirli bir zamandan sonraki haberleri getirir
        /// </summary>
        public static List<RSSItem> GetNewsSince(DateTime sinceTime)
        {
            lock (_lock)
            {
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    _lastNewsItems.Clear();
                    return new List<RSSItem>();
                }
                
                // PublishDate'i sinceTime'dan sonra olan haberleri filtrele
                var newItems = _lastNewsItems.Where(item => 
                    item.PublishDate > sinceTime && 
                    !_lastShownNewsGuids.Contains(item.Guid))
                    .OrderByDescending(item => item.PublishDate)
                    .ToList();
                    
                LogService.LogInfo($"[NewsMemoryService] {sinceTime:HH:mm:ss} sonrası {newItems.Count} yeni haber bulundu");
                return newItems;
            }
        }
        
        /// <summary>
        /// Son komuttan sonra yeni haber var mı kontrol eder
        /// </summary>
        public static bool HasNewNews()
        {
            lock (_lock)
            {
                if (_lastCommandTime == DateTime.MinValue)
                    return true; // İlk komut
                    
                return GetNewsSince(_lastCommandTime).Any();
            }
        }
        
        /// <summary>
        /// Son komut zamanını getirir
        /// </summary>
        public static DateTime GetLastCommandTime()
        {
            lock (_lock)
            {
                return _lastCommandTime;
            }
        }
        
        /// <summary>
        /// Tüm haberleri yeni olarak işaretle (zaman damgasını sıfırla)
        /// </summary>
        public static void ResetTimeFilter()
        {
            lock (_lock)
            {
                _lastCommandTime = DateTime.MinValue;
                _lastShownNewsGuids.Clear();
                LogService.LogInfo("[NewsMemoryService] Zaman filtresi sıfırlandı");
            }
        }
        
        /// <summary>
        /// Kategori bazlı son komut zamanını günceller
        /// </summary>
        public static void UpdateLastCommandTimeForCategory(string category, List<RSSItem> shownItems)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(category))
                    category = "genel";
                    
                category = category.ToLowerInvariant();
                _lastCategoryCommandTimes[category] = DateTime.Now;
                
                if (shownItems != null && shownItems.Any())
                {
                    if (!_lastCategoryShownGuids.ContainsKey(category))
                        _lastCategoryShownGuids[category] = new HashSet<string>();
                        
                    _lastCategoryShownGuids[category].Clear();
                    foreach (var item in shownItems)
                    {
                        if (!string.IsNullOrEmpty(item.Guid))
                        {
                            _lastCategoryShownGuids[category].Add(item.Guid);
                        }
                    }
                    LogService.LogInfo($"[NewsMemoryService] {category} kategorisi için son komut zamanı güncellendi, {_lastCategoryShownGuids[category].Count} haber işaretlendi");
                }
            }
        }
        
        /// <summary>
        /// Kategori için son komut zamanını getirir
        /// </summary>
        public static DateTime GetLastCommandTimeForCategory(string category)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(category))
                    category = "genel";
                    
                category = category.ToLowerInvariant();
                return _lastCategoryCommandTimes.ContainsKey(category) ? _lastCategoryCommandTimes[category] : DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// Belirli bir kategori ve zamandan sonraki haberleri getirir
        /// </summary>
        public static List<RSSItem> GetNewsSinceForCategory(string category, DateTime sinceTime)
        {
            lock (_lock)
            {
                if (DateTime.Now - _lastUpdateTime > CacheDuration)
                {
                    _lastNewsItems.Clear();
                    return new List<RSSItem>();
                }
                
                if (string.IsNullOrEmpty(category))
                    category = "genel";
                    
                category = category.ToLowerInvariant();
                var categoryGuids = _lastCategoryShownGuids.ContainsKey(category) ? _lastCategoryShownGuids[category] : new HashSet<string>();
                
                // PublishDate'i sinceTime'dan sonra olan VE daha önce gösterilmemiş haberleri filtrele
                var newItems = _lastNewsItems.Where(item => 
                    item.PublishDate > sinceTime && 
                    !categoryGuids.Contains(item.Guid))
                    .OrderByDescending(item => item.PublishDate)
                    .ToList();
                    
                LogService.LogInfo($"[NewsMemoryService] {category} kategorisi için {sinceTime:HH:mm:ss} sonrası {newItems.Count} yeni haber bulundu");
                return newItems;
            }
        }
        
        /// <summary>
        /// Kategori için yeni haber var mı kontrol eder
        /// </summary>
        public static bool HasNewNewsForCategory(string category)
        {
            lock (_lock)
            {
                var lastTime = GetLastCommandTimeForCategory(category);
                if (lastTime == DateTime.MinValue)
                    return true; // İlk komut
                    
                return GetNewsSinceForCategory(category, lastTime).Any();
            }
        }
    }
}