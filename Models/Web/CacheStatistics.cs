using System.Threading;

namespace QuadroAIPilot.Models.Web
{
    /// <summary>
    /// Cache statistics model
    /// </summary>
    public class CacheStatistics
    {
        private long _totalRequests;
        private long _cacheHits;
        private long _cacheMisses;

        public long TotalRequests => _totalRequests;
        public long CacheHits => _cacheHits;
        public long CacheMisses => _cacheMisses;
        public long ItemCount { get; set; }
        public long TotalSizeBytes { get; set; }

        public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;

        public void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);
        public void IncrementCacheHits() => Interlocked.Increment(ref _cacheHits);
        public void IncrementCacheMisses() => Interlocked.Increment(ref _cacheMisses);

        public void Reset()
        {
            _totalRequests = 0;
            _cacheHits = 0;
            _cacheMisses = 0;
            ItemCount = 0;
            TotalSizeBytes = 0;
        }
    }
}