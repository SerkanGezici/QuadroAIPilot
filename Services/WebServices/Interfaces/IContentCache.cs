using System;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Services.WebServices.Interfaces
{
    /// <summary>
    /// Multi-level cache interface for web content
    /// </summary>
    public interface IContentCache
    {
        /// <summary>
        /// Try to get cached content
        /// </summary>
        Task<(bool found, T content)> TryGetAsync<T>(string key) where T : class;

        /// <summary>
        /// Set content in cache with TTL
        /// </summary>
        Task SetAsync<T>(string key, T content, TimeSpan? ttl = null) where T : class;

        /// <summary>
        /// Remove item from cache
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Clear all cache
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Get cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();
    }
}