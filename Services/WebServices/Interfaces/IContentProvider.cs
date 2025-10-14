using System;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Services.WebServices.Interfaces
{
    /// <summary>
    /// Base interface for all content providers (Wikipedia, RSS, Twitter, etc.)
    /// </summary>
    public interface IContentProvider
    {
        /// <summary>
        /// Provider name for logging and identification
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Priority order (lower = higher priority)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Check if this provider can handle the given request
        /// </summary>
        Task<bool> CanHandleAsync(ContentRequest request);

        /// <summary>
        /// Get content from this provider
        /// </summary>
        Task<WebContent> GetContentAsync(ContentRequest request);

        /// <summary>
        /// Check if provider is available (online, API limits, etc.)
        /// </summary>
        Task<bool> IsAvailableAsync();
    }
}