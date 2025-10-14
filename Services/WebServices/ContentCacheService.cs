using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using QuadroAIPilot.Models.Web;
using QuadroAIPilot.Services.WebServices.Interfaces;

namespace QuadroAIPilot.Services.WebServices
{
    /// <summary>
    /// Multi-level cache implementation (Memory L1 + File L2)
    /// </summary>
    public class ContentCacheService : IContentCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly string _cacheDirectory;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private readonly CacheStatistics _statistics;
        private readonly JsonSerializerOptions _jsonOptions;

        public ContentCacheService()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                // SizeLimit kaldırıldı - otomatik memory yönetimi kullanılacak
                CompactionPercentage = 0.25
            });

            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuadroAIPilot",
                "Cache",
                "WebContent"
            );

            Directory.CreateDirectory(_cacheDirectory);
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _statistics = new CacheStatistics();
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<(bool found, T content)> TryGetAsync<T>(string key) where T : class
        {
            _statistics.IncrementTotalRequests();

            // L1: Memory cache
            if (_memoryCache.TryGetValue<T>(key, out var cachedValue))
            {
                _statistics.IncrementCacheHits();
                return (true, cachedValue);
            }

            // L2: File cache
            var filePath = GetCacheFilePath(key);
            if (File.Exists(filePath))
            {
                var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync();

                try
                {
                    if (File.Exists(filePath)) // Double-check
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var cacheEntry = JsonSerializer.Deserialize<CacheEntry<T>>(json, _jsonOptions);

                        if (!cacheEntry.IsExpired)
                        {
                            // Promote to L1
                            var remainingTtl = cacheEntry.ExpiresAt - DateTime.UtcNow;
                            _memoryCache.Set(key, cacheEntry.Content, remainingTtl);

                            _statistics.IncrementCacheHits();
                            return (true, cacheEntry.Content);
                        }
                        else
                        {
                            // Expired, delete file
                            File.Delete(filePath);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            _statistics.IncrementCacheMisses();
            return (false, null);
        }

        public async Task SetAsync<T>(string key, T content, TimeSpan? ttl = null) where T : class
        {
            var effectiveTtl = ttl ?? GetDefaultTtl(content);
            var expiresAt = DateTime.UtcNow.Add(effectiveTtl);

            // L1: Set in memory cache
            _memoryCache.Set(key, content, effectiveTtl);

            // L2: Save to file (async)
            _ = Task.Run(async () =>
            {
                var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync();

                try
                {
                    var cacheEntry = new CacheEntry<T>
                    {
                        Content = content,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = expiresAt,
                        Key = key
                    };

                    var json = JsonSerializer.Serialize(cacheEntry, _jsonOptions);
                    var filePath = GetCacheFilePath(key);
                    
                    var directory = Path.GetDirectoryName(filePath);
                    Directory.CreateDirectory(directory);
                    
                    await File.WriteAllTextAsync(filePath, json);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public Task RemoveAsync(string key)
        {
            _memoryCache.Remove(key);
            
            var filePath = GetCacheFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            // Clear memory cache
            if (_memoryCache is MemoryCache cache)
            {
                cache.Compact(1.0); // Remove all
            }

            // Clear file cache
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
            }

            // Reset statistics
            _statistics.Reset();

            return Task.CompletedTask;
        }

        public Task<CacheStatistics> GetStatisticsAsync()
        {
            // Update item count and size
            _statistics.ItemCount = Directory.GetFiles(_cacheDirectory, "*.json", SearchOption.AllDirectories).Length;
            
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            _statistics.TotalSizeBytes = CalculateDirectorySize(directoryInfo);

            return Task.FromResult(_statistics);
        }

        private string GetCacheFilePath(string key)
        {
            // Create a safe filename from the key
            var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            var hash = Math.Abs(key.GetHashCode());
            var subDirectory = (hash % 256).ToString("X2"); // Distribute files across 256 subdirectories
            
            return Path.Combine(_cacheDirectory, subDirectory, $"{safeKey}.json");
        }

        private TimeSpan GetDefaultTtl<T>(T content)
        {
            // Dynamic TTL based on content type
            if (content is Models.Web.WebContent webContent)
            {
                return webContent.Type switch
                {
                    Models.Web.ContentType.Weather => TimeSpan.FromMinutes(30),
                    Models.Web.ContentType.TwitterTrend => TimeSpan.FromMinutes(5), // 15 dakikadan 5 dakikaya düşürüldü
                    Models.Web.ContentType.News => TimeSpan.FromMinutes(30), // 1 saatten 30 dakikaya düşürüldü
                    Models.Web.ContentType.Wikipedia => TimeSpan.FromDays(7),
                    _ => TimeSpan.FromHours(2)
                };
            }

            return TimeSpan.FromHours(1); // Default 1 hour
        }

        private long CalculateDirectorySize(DirectoryInfo directory)
        {
            long size = 0;
            
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
            
            return size;
        }

        private class CacheEntry<T>
        {
            public string Key { get; set; }
            public T Content { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }
    }
}