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
            }
        }
    }
}